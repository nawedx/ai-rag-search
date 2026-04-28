using OpenAI.Chat;

namespace SearchSprint.Indexer.Services;

public class RagChatService : IRagChatService
{
    private readonly ChatClient _chatClient;
    private readonly ISearchService _searchService;

    public RagChatService(ChatClient chatClient, ISearchService searchService)
    {
        _chatClient = chatClient;
        _searchService = searchService;
    }

    public async Task<string> AnswerQuestionAsync(string question)
    {
        // 1. Retrieve
        var movies = await _searchService.HybridSearchAsync(question, topK: 3);
        
        if (!movies.Any())
        {
            return "I couldn't find any relevant movies in our database.";
        }

        // 2. Augment (Build Context)
        var contextText = string.Join("\n---\n", movies.Select(m => $"Title: {m.Title}\nPlot: {m.Overview}"));

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a helpful movie expert. Answer the user's question using ONLY the provided context. If a movie in the context is relevant to the question — even if the topic is just mentioned in the plot rather than the central theme — include it in your answer. Only say 'I don't know' if the context contains absolutely no relevant information."),
            new UserChatMessage($@"
Context:
{contextText}

Question: 
{question}")
        };

        // 3. Generate
        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages);
        return completion.Content[0].Text;
    }
}
