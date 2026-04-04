using System.Collections.Generic;
using System.Threading.Tasks;

namespace SearchSprint.Indexer.Services;

public interface ISearchService
{
    Task<IEnumerable<Movie>> HybridSearchAsync(string query, int topK = 3);
}
