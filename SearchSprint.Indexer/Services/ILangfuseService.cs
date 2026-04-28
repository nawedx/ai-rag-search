namespace SearchSprint.Indexer.Services;

public interface ILangfuseService
{
    string StartTrace(string name, object? input = null);
    void AddGeneration(string traceId, string name, string model,
                       object? input, string output,
                       int inputTokens, int outputTokens,
                       DateTime startTime, DateTime endTime);
    void EndTrace(string traceId, object? output = null);
    Task FlushAsync();
}
