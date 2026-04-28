namespace SearchSprint.Indexer.Services;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
    Task<float[][]> GenerateEmbeddingsAsync(string[] texts);
}
