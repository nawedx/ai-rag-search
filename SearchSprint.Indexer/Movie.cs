namespace SearchSprint.Indexer;

public class Movie
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Overview { get; set; } // The plot
    public required string[] Genres { get; set; }
    public float Rating { get; set; }
    public float[]? Vector { get; set; }
    public DateTime ReleaseDate { get; set; }
}
