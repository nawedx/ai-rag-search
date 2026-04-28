using System.Net.Http.Json;

namespace SearchSprint.Indexer.Services;

public class LangfuseService : ILangfuseService
{
    private readonly HttpClient _http;
    private readonly List<object> _events = [];

    public LangfuseService(HttpClient http) => _http = http;

    public string StartTrace(string name, object? input = null)
    {
        var id = Guid.NewGuid().ToString();
        _events.Add(new
        {
            id = Guid.NewGuid().ToString(),
            type = "trace-create",
            timestamp = DateTime.UtcNow.ToString("o"),
            body = new { id, name, input }
        });
        return id;
    }

    public void AddGeneration(string traceId, string name, string model,
                              object? input, string output,
                              int inputTokens, int outputTokens,
                              DateTime startTime, DateTime endTime)
    {
        _events.Add(new
        {
            id = Guid.NewGuid().ToString(),
            type = "generation-create",
            timestamp = DateTime.UtcNow.ToString("o"),
            body = new
            {
                id = Guid.NewGuid().ToString(),
                traceId,
                name,
                model,
                input,
                output,
                usage = new { input = inputTokens, output = outputTokens },
                startTime = startTime.ToString("o"),
                endTime = endTime.ToString("o")
            }
        });
    }

    // Sends a second trace-create with the same id — Langfuse merges it (same as Python trace.update())
    public void EndTrace(string traceId, object? output = null)
    {
        _events.Add(new
        {
            id = Guid.NewGuid().ToString(),
            type = "trace-create",
            timestamp = DateTime.UtcNow.ToString("o"),
            body = new { id = traceId, output }
        });
    }

    public async Task FlushAsync()
    {
        if (_events.Count == 0) return;
        var batch = _events.ToList();
        _events.Clear();
        await _http.PostAsJsonAsync("/api/public/ingestion", new { batch });
    }
}
