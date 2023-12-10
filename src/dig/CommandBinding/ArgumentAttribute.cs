[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class ArgumentAttribute(int index) : Attribute
{
    public int Index { get; } = index;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public object? Default { get; init; }
}
