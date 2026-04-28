using SearchSprint.Indexer;

namespace SearchSprint.Indexer.Services;

public interface IDocumentIndexerService
{
    Task<Movie?> GetMovieAsync(int id);
    Task IndexMovieAsync(Movie movie);
    Task IndexMoviesAsync(IEnumerable<Movie> movies);
    Task UpdateMovieAsync(int id, Movie movie);
    Task DeleteMovieAsync(int id);
}
