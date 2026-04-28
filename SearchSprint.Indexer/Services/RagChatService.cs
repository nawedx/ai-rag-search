using System.Diagnostics;
using OpenAI.Chat;

namespace SearchSprint.Indexer.Services;

public class RagChatService : IRagChatService
{
    private readonly ChatClient _chatClient;
    private readonly ISearchService _searchService;
    private readonly ILangfuseService _langfuse;

    public RagChatService(ChatClient chatClient, ISearchService searchService, ILangfuseService langfuse)
    {
        _chatClient = chatClient;
        _searchService = searchService;
        _langfuse = langfuse;
    }

    public async Task<string> AnswerQuestionAsync(string question)
    {
        using var activity = Telemetry.Source.StartActivity("rag.answer_question", ActivityKind.Server);
        activity?.SetTag("input.question", question);

        var traceId = _langfuse.StartTrace("rag.answer_question", new { question });

        // 1. Retrieve
        var movies = await _searchService.HybridSearchAsync(question, topK: 3);

        if (!movies.Any())
        {
            const string noResults = "I couldn't find any relevant movies in our database.";
            activity?.SetTag("output.answer", noResults);
            _langfuse.EndTrace(traceId, new { answer = noResults });
            await _langfuse.FlushAsync();
            return noResults;
        }

        activity?.SetTag("retrieval.count", movies.Count());

        // 2. Augment (Build Context)
        var contextText = string.Join("\n---\n", movies.Select(m => $"Title: {m.Title}\nPlot: {m.Overview}"));

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a helpful movie expert. Answer the user's question using ONLY the provided context. If a movie in the context is relevant to the question — even if the topic is just mentioned in the plot rather than the central theme — include it in your answer. Only say 'I don't know' if the context contains absolutely no relevant information."),
            new UserChatMessage($"Context:\n{contextText}\n\nQuestion:\n{question}")
        };

        // 3. Generate
        var genStart = DateTime.UtcNow;
        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages);
        var genEnd = DateTime.UtcNow;

        var answer = completion.Content[0].Text;

        _langfuse.AddGeneration(
            traceId,
            name: "chat",
            model: "gpt-4o-mini",
            input: messages.Select(m => new
            {
                role = m is SystemChatMessage ? "system" : "user",
                content = m.Content[0].Text
            }).ToList(),
            output: answer,
            inputTokens: completion.Usage.InputTokenCount,
            outputTokens: completion.Usage.OutputTokenCount,
            startTime: genStart,
            endTime: genEnd);

        _langfuse.EndTrace(traceId, new { answer });
        await _langfuse.FlushAsync();

        activity?.SetTag("output.answer", answer);
        return answer;
    }
}
