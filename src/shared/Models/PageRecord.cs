namespace dig;

internal record PageRecord
{
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public IEnumerable<Store> Mirrors { get; init; } = new List<Store>();
}
