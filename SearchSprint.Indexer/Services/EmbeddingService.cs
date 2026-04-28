using OpenAI.Embeddings;

namespace SearchSprint.Indexer.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _embeddingClient;

    public EmbeddingService(EmbeddingClient embeddingClient)
    {
        _embeddingClient = embeddingClient;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var result = await _embeddingClient.GenerateEmbeddingAsync(text);
        return result.Value.ToFloats().ToArray();
    }

    public async Task<float[][]> GenerateEmbeddingsAsync(string[] texts)
    {
        var result = await _embeddingClient.GenerateEmbeddingsAsync(texts);
        return result.Value.Select(e => e.ToFloats().ToArray()).ToArray();
    }
}
