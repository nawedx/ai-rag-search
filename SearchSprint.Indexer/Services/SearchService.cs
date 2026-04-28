using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using SearchSprint.Indexer;

namespace SearchSprint.Indexer.Services;

public class SearchService : ISearchService
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly IEmbeddingService _embeddingService;
    private readonly string _indexName;

    public SearchService(ElasticsearchClient elasticClient, IEmbeddingService embeddingService, IConfiguration config)
    {
        _elasticClient = elasticClient;
        _embeddingService = embeddingService;
        _indexName = config["Elasticsearch:IndexName"] ?? "movies";
    }

    public async Task<IEnumerable<Movie>> HybridSearchAsync(string query, int topK = 3)
    {
        // 1. Get the vector for the user query
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query);

        // 2. Perform Hybrid Search
        var response = await _elasticClient.SearchAsync<Movie>(s => s
            .Indices(_indexName)
            .Knn(k => k
                .Field(f => f.Vector)
                .QueryVector(queryVector)
                .K(5)
                .NumCandidates(100)
            )
            .Query(q => q
                .MultiMatch(mm => mm
                    .Fields(new[] { "title^3", "overview" }) 
                    .Query(query)
                )
            )
            .Rank(r => r.Rrf(rrf => rrf.RankConstant(60)))
        );

        if (!response.IsValidResponse)
        {
            // In production, log this error instead of throwing a generic exception
            throw new Exception($"Elasticsearch query failed: {response.DebugInformation}");
        }

        return response.Hits.Take(topK).Select(h => h.Source!);
    }
}
