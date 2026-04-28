using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using SearchSprint.Indexer;

namespace SearchSprint.Indexer.Services;

public class DocumentIndexerService : IDocumentIndexerService
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly IEmbeddingService _embeddingService;
    private readonly string _indexName;

    public DocumentIndexerService(ElasticsearchClient elasticClient, IEmbeddingService embeddingService, IConfiguration config)
    {
        _elasticClient = elasticClient;
        _embeddingService = embeddingService;
        _indexName = config["Elasticsearch:IndexName"] ?? "movies";
    }

    public async Task<Movie?> GetMovieAsync(int id)
    {
        var response = await _elasticClient.GetAsync<Movie>(id, g => g.Index(_indexName));
        return response.Found ? response.Source : null;
    }

    public async Task IndexMovieAsync(Movie movie)
    {
        movie.Vector = await _embeddingService.GenerateEmbeddingAsync(movie.Overview);
        var response = await _elasticClient.IndexAsync(movie, i => i.Index(_indexName).Id(movie.Id));
        if (!response.IsValidResponse)
            throw new Exception($"Failed to index movie: {response.DebugInformation}");
    }

    public async Task IndexMoviesAsync(IEnumerable<Movie> movies)
    {
        var moviesList = movies.ToList();
        var overviews = moviesList.Select(m => m.Overview).ToArray();
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(overviews);

        for (int i = 0; i < moviesList.Count; i++)
            moviesList[i].Vector = embeddings[i];

        var response = await _elasticClient.IndexManyAsync(moviesList, _indexName);
        if (!response.IsValidResponse)
            throw new Exception($"Failed to index movies: {response.DebugInformation}");
    }

    public async Task UpdateMovieAsync(int id, Movie movie)
    {
        movie.Id = id;
        movie.Vector = await _embeddingService.GenerateEmbeddingAsync(movie.Overview);
        var response = await _elasticClient.IndexAsync(movie, i => i.Index(_indexName).Id(id));
        if (!response.IsValidResponse)
            throw new Exception($"Failed to update movie: {response.DebugInformation}");
    }

    public async Task DeleteMovieAsync(int id)
    {
        var response = await _elasticClient.DeleteAsync<Movie>(id, d => d.Index(_indexName));
        if (!response.IsValidResponse)
            throw new Exception($"Failed to delete movie: {response.DebugInformation}");
    }
}
