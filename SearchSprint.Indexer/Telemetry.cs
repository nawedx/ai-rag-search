using System.Diagnostics;

namespace SearchSprint.Indexer;

public static class Telemetry
{
    public const string SourceName = "SearchSprint.Indexer";
    public static readonly ActivitySource Source = new(SourceName);
}
