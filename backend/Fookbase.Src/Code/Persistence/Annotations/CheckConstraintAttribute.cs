namespace Fookbase.Api.Persistence.Annotations;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class CheckConstraintAttribute(string name, string sql) : Attribute
{
    public string Name { get; } = name;

    public string Sql { get; } = sql;
}
