namespace Fookbase.Api.Persistence.Annotations;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class IndexFilterAttribute(
    string filter,
    string propertyName,
    params string[] additionalPropertyNames) : Attribute
{
    public string Filter { get; } = filter;

    public IReadOnlyList<string> PropertyNames { get; } = [propertyName, .. additionalPropertyNames];
}
