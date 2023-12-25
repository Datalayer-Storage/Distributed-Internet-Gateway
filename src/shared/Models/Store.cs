using chia.dotnet;

public record Store
{
    public string? verified_name { get; init; }
    public string singleton_id { get; init; } = string.Empty;
    public long bytes { get; init; }
    public string display_name => verified_name ?? singleton_id[..10] + "...";
    public bool is_verified => verified_name is not null;
    public string bytes_display => bytes.ToBytesString("N0");
};
