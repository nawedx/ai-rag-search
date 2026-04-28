using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Embeddings;
using OpenAI.Chat;
using SearchSprint.Indexer;
using SearchSprint.Indexer.Services;

var builder = WebApplication.CreateBuilder(args);

var elasticConfig = builder.Configuration.GetSection("Elasticsearch");
var openAiConfig = builder.Configuration.GetSection("OpenAI");

builder.Services.AddSingleton<ElasticsearchClient>(sp =>
{
    var settings = new ElasticsearchClientSettings(new Uri(elasticConfig["Uri"]!))
        .Authentication(new BasicAuthentication(elasticConfig["Username"]!, elasticConfig["Password"]!));
    return new ElasticsearchClient(settings);
});

var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? openAiConfig["ApiKey"]
    ?? throw new InvalidOperationException("OpenAI API key not set. Set OPENAI_API_KEY env var or OpenAI:ApiKey in appsettings.");

builder.Services.AddSingleton<EmbeddingClient>(sp =>
    new EmbeddingClient(openAiConfig["EmbeddingModel"]!, openAiApiKey));

builder.Services.AddSingleton<ChatClient>(sp =>
    new ChatClient(openAiConfig["ChatModel"]!, openAiApiKey));

builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IRagChatService, RagChatService>();
builder.Services.AddScoped<IDocumentIndexerService, DocumentIndexerService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// --- CRUD ---

app.MapPost("/movies", async ([FromBody] Movie movie, IDocumentIndexerService indexer) =>
{
    await indexer.IndexMovieAsync(movie);
    return Results.Created($"/movies/{movie.Id}", movie);
})
.WithName("CreateMovie")
.WithSummary("Create and index a single movie");

app.MapGet("/movies/{id:int}", async (int id, IDocumentIndexerService indexer) =>
{
    var movie = await indexer.GetMovieAsync(id);
    return movie is not null ? Results.Ok(movie) : Results.NotFound();
})
.WithName("GetMovie")
.WithSummary("Get a movie by ID");

app.MapPut("/movies/{id:int}", async (int id, [FromBody] Movie movie, IDocumentIndexerService indexer) =>
{
    await indexer.UpdateMovieAsync(id, movie);
    return Results.Ok(movie);
})
.WithName("UpdateMovie")
.WithSummary("Update a movie and re-embed its overview");

app.MapDelete("/movies/{id:int}", async (int id, IDocumentIndexerService indexer) =>
{
    await indexer.DeleteMovieAsync(id);
    return Results.NoContent();
})
.WithName("DeleteMovie")
.WithSummary("Delete a movie by ID");

app.MapPost("/movies/bulk", async ([FromBody] List<Movie> movies, IDocumentIndexerService indexer) =>
{
    await indexer.IndexMoviesAsync(movies);
    return Results.Ok(new { indexed = movies.Count });
})
.WithName("BulkIndexMovies")
.WithSummary("Embed and bulk-index a list of movies");

app.MapPost("/movies/seed", async (IDocumentIndexerService indexer) =>
{
    await indexer.IndexMoviesAsync(SeedData.Movies);
    return Results.Ok(new { seeded = SeedData.Movies.Count });
})
.WithName("SeedMovies")
.WithSummary("Populate Elasticsearch with 20 pre-defined test movies");

// --- Search & RAG ---

app.MapGet("/movies/search", async (string q, ISearchService search, int topK = 3) =>
    Results.Ok(await search.HybridSearchAsync(q, topK)))
.WithName("SearchMovies")
.WithSummary("Hybrid semantic + keyword search over indexed movies");

app.MapPost("/movies/ask", async ([FromBody] AskRequest request, IRagChatService rag) =>
{
    var answer = await rag.AnswerQuestionAsync(request.Question);
    return Results.Ok(new { answer });
})
.WithName("AskQuestion")
.WithSummary("Answer a natural-language question using RAG over indexed movies");

app.Run();

record AskRequest(string Question);
