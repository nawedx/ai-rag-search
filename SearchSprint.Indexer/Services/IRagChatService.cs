using System.Threading.Tasks;

namespace SearchSprint.Indexer.Services;

public interface IRagChatService
{
    Task<string> AnswerQuestionAsync(string question);
}
