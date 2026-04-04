using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using OpenAI.Embeddings;
using OpenAI.Chat;
using Elastic.Transport;

// --- 1. SETUP CLIENTS ---

// A. Setup Elasticsearch
var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"))
    .Authentication(new BasicAuthentication("elastic", "password"));
var client = new ElasticsearchClient(settings);

// B. Setup OpenAI
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
             ?? throw new InvalidOperationException("Missing OPENAI_API_KEY environment variable");

// In the new OpenAI v2 library, we create a specific client for Embeddings
var embeddingGenerator = new EmbeddingClient("text-embedding-3-small", apiKey);

// We use "gpt-4o-mini" because it's cheap, fast, and smart enough.
var chatClient = new ChatClient("gpt-4o-mini", apiKey);

// --- 2. DEFINE THE INDEX (The "Schema") ---

var indexName = "movies-v2-vectors"; // New name to avoid conflicts

// Always start fresh for this learning sprint
if ((await client.Indices.ExistsAsync(indexName)).Exists)
{
    await client.Indices.DeleteAsync(indexName);
    Console.WriteLine($"Deleted old index: {indexName}");
}

Console.WriteLine("Creating Index with Vector Mapping...");

// CRITICAL: We must tell Elastic that 'Vector' is a DenseVector, not just a number array.
await client.Indices.CreateAsync<Movie>(indexName, c => c
    .Mappings(m => m
        .Properties(p => p
            .Text(t => t.Title)
            .Text(t => t.Overview)
            .Keyword(k => k.Genres)
            .DenseVector(s => s.Vector, dv => dv
                .Dims(1536)        // Must match OpenAI model dimensions
                .Index(true)       // Allow searching (HNSW graph)
                .Similarity(DenseVectorSimilarity.Cosine) // Best for text similarity
            )
        )
    )
);


// --- 3. PREPARE DATA ---

var movies = new List<Movie>
{
    new()
    {
        Id = 1, Title = "Inception",
        Overview = "A thief who steals corporate secrets through the use of dream-sharing technology. The main character in this is Leo.", 
        Genres = ["Sci-Fi", "Thriller"],
        Rating = 8.8f, ReleaseDate = new DateTime(2010, 7, 16)
    },
    new()
    {
        Id = 2, Title = "The Matrix",
        Overview = "A computer hacker learns from mysterious rebels about the true nature of his reality.", 
        Genres = ["Sci-Fi", "Action"],
        Rating = 8.7f, ReleaseDate = new DateTime(1999, 3, 31)
    },
    new()
    {
        Id = 3, Title = "Interstellar",
        Overview = "A team of explorers travel through a wormhole in space in an attempt to ensure humanity's survival.",
        Genres = ["Sci-Fi", "Drama"],
        Rating = 8.6f, ReleaseDate = new DateTime(2014, 11, 7)
    },
    new()
    {
        Id = 4, Title = "The Godfather",
        Overview = "The aging patriarch of an organized crime dynasty transfers control of his clandestine empire to his reluctant son.",
        Genres = ["Crime", "Drama"],
        Rating = 9.2f, ReleaseDate = new DateTime(1972, 3, 24)
    }
};


// --- 4. GENERATE EMBEDDINGS (The "AI" Step) ---

Console.WriteLine("Generating Embeddings via OpenAI...");

foreach (var movie in movies)
{
    // Call OpenAI API
    // Note: In a real app, you'd batch this to save network calls.
    var result = 
        await embeddingGenerator.GenerateEmbeddingAsync(movie.Overview);
    
    // Convert ReadOnlyMemory<float> to float[]
    movie.Vector = result.Value.ToFloats().ToArray();
    
    Console.WriteLine($" -> Generated vector for '{movie.Title}'");
}

// --- 5. INDEX DATA ---

Console.WriteLine("Indexing data into Elasticsearch...");
var response = await client.IndexManyAsync(movies, indexName);

await client.Indices.RefreshAsync(indexName);

if (response.IsValidResponse)
{
    Console.WriteLine($"Successfully indexed {movies.Count} movies!");
}
else
{
    Console.WriteLine($"Failed to index: {response.DebugInformation}");
}

// // --- 7. THE FINAL BOSS: HYBRID SEARCH (RRF) ---
// Console.WriteLine("\n--- STARTING HYBRID SEARCH (RRF) ---");
//
// string userQuery = "Neo"; 
// Console.WriteLine($"\nUser Query: \"{userQuery}\"");
//
// // 1. Generate Vector
// var queryEmbeddingResult = await embeddingGenerator.GenerateEmbeddingAsync(userQuery);
// float[] queryVector = queryEmbeddingResult.Value.ToFloats().ToArray();
//
// // 2. Execute Hybrid Search using RRF
// // We pass a 'Query' (Lexical) AND a 'Knn' (Vector)
// // We use 'Rank' to tell ES how to merge them using RRF
// var hybridResponse = await client.SearchAsync<Movie>(s => s
//     .Indices(indexName)
//     // A. The Vector Search Part
//     .Knn(k => k
//         .Field(f => f.Vector)
//         .QueryVector(queryVector)
//         .K(5)             // Get top 5 vector matches
//         .NumCandidates(100)
//     )
//     // B. The Keyword Search Part
//     .Query(q => q
//         .MultiMatch(mm => mm
//             // USE STRING SYNTAX: It's cleaner and less error-prone
//             .Fields(new[] { "title^3", "overview" }) 
//             .Query(userQuery)
//         )
//     )
//     // C. The Fusion Algorithm (RRF)
//     // This tells ES: "Don't just add scores. Rank them by position."
//     .Rank(r => r
//         .Rrf(rrf => rrf
//                 .RankConstant(60) // Standard tuning parameter
//         )
//     )
// );
//
// Console.WriteLine($"\nHybrid Results:");
// var rankCounter = 1;
// if (!hybridResponse.IsValidResponse)
// {
//     Console.WriteLine($"Error: {hybridResponse.DebugInformation}");
// }
// else 
// {
//     foreach (var hit in hybridResponse.Hits)
//     {
//         // Notice we don't look at absolute score anymore, just the rank order
//         Console.WriteLine($"[#{rankCounter++}] {hit.Source?.Title}");
//     }
// }

// --- 7. THE RAG PIPELINE ---

Console.WriteLine($"\n--- STARTING RAG (Chat with Data) ---");
// string userQuery = "Recommend a movie about space survival"; // Try this later
string userQuery = "Is there any movie about organized crime?"; 
Console.WriteLine($"User Question: \"{userQuery}\"");

// --- STEP A: RETRIEVAL (The "Search" Part) ---

// 1. Generate Vector
var queryEmbeddingResult = await embeddingGenerator.GenerateEmbeddingAsync(userQuery);
float[] queryVector = queryEmbeddingResult.Value.ToFloats().ToArray();

// 2. Hybrid Search
var searchResponse = await client.SearchAsync<Movie>(s => s
    .Indices(indexName)
    .Knn(k => k
        .Field(f => f.Vector)
        .QueryVector(queryVector)
        .K(5)
        .NumCandidates(100)
    )
    .Query(q => q
        .MultiMatch(mm => mm
            .Fields(new[] { "title^3", "overview" }) 
            .Query(userQuery)
        )
    )
    .Rank(r => r
        .Rrf(rrf => rrf
            .RankConstant(60)
        )
    )
);


// --- STEP B: AUGMENTATION (The "Context" Part) ---

if (!searchResponse.IsValidResponse || searchResponse.Hits.Count == 0)
{
    Console.WriteLine("No relevant movies found in our database.");
    return;
}

// We take the Top 3 results and glue them together into a single string.
// This is the "Knowledge" we are giving the AI.
var topDocs = searchResponse.Hits.Take(3).Select(h => 
    $"Title: {h.Source.Title}\nPlot: {h.Source.Overview}\n"
);
string contextText = string.Join("\n---\n", topDocs);

Console.WriteLine($"\n[System] Retrieved {searchResponse.Hits.Count} documents. Sending Top 3 to LLM...");


// --- STEP C: GENERATION (The "AI" Part) ---

// This is the Prompt Engineering part.
var messages = new List<ChatMessage>
{
    // 1. System Prompt: Set the personality and rules.
    new SystemChatMessage("You are a helpful movie expert. Answer the user's question using ONLY the provided context. If the answer isn't in the context, say 'I don't know'."),
    
    // 2. User Prompt: Combine the Question + The Retrieved Data
    new UserChatMessage($@"
Context:
{contextText}

Question: 
{userQuery}
")
};

Console.WriteLine("[System] Generating answer...");
ChatCompletion completion = await chatClient.CompleteChatAsync(messages);

Console.WriteLine($"\nAI Answer:\n{completion.Content[0].Text}");