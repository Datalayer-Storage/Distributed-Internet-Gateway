using chia.dotnet;

namespace dig;

/// <summary>
/// Represents a store entity.
/// </summary>
public record Store
{
    /// <summary>
    /// Gets or sets the verified name of the store.
    /// </summary>
    public string? verified_name { get; init; }

    /// <summary>
    /// Gets or sets the singleton ID of the store.
    /// </summary>
    public string singleton_id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of bytes in the store.
    /// </summary>
    public long bytes { get; init; }

    /// <summary>
    /// Gets the display name of the store.
    /// If the verified name is available, it is used.
    /// Otherwise, the first 10 characters of the singleton ID followed by "..." are used.
    /// </summary>
    public string display_name => verified_name ?? singleton_id[..10] + "...";

    /// <summary>
    /// Gets a value indicating whether the store is verified.
    /// </summary>
    public bool is_verified => verified_name is not null;

    /// <summary>
    /// Gets the formatted string representation of the number of bytes in the store.
    /// </summary>
    public string bytes_display => bytes.ToBytesString("N0");
};
