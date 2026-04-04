using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using OpenAI.Embeddings;
using OpenAI.Chat;
using SearchSprint.Indexer.Services;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// 1. Register Configuration Options (Optional but recommended for strict typing)
var elasticConfig = builder.Configuration.GetSection("Elasticsearch");
var openAiConfig = builder.Configuration.GetSection("OpenAI");

// 2. Register External Clients as SINGLETONS (They manage their own HTTP connections)
builder.Services.AddSingleton<ElasticsearchClient>(sp => 
{
    var settings = new ElasticsearchClientSettings(new Uri(elasticConfig["Uri"]!))
        .Authentication(new BasicAuthentication(elasticConfig["Username"]!, elasticConfig["Password"]!));
    return new ElasticsearchClient(settings);
});

builder.Services.AddSingleton<EmbeddingClient>(sp => 
    new EmbeddingClient(openAiConfig["EmbeddingModel"]!, openAiConfig["ApiKey"]!));

builder.Services.AddSingleton<ChatClient>(sp => 
    new ChatClient(openAiConfig["ChatModel"]!, openAiConfig["ApiKey"]!));

// 3. Register our Custom Services
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IRagChatService, RagChatService>();

var host = builder.Build();

// Example Usage (If running as a console app or worker)
using var scope = host.Services.CreateScope();
var ragService = scope.ServiceProvider.GetRequiredService<IRagChatService>();

var answer = await ragService.AnswerQuestionAsync("Is there any movie about organized crime?");
Console.WriteLine($"\nAI Answer:\n{answer}");

await host.RunAsync();

