namespace dig;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal class CommandAttribute(string name) : Attribute
{
    public string Name { get; init; } = name;

    public string? Description { get; init; }
}
