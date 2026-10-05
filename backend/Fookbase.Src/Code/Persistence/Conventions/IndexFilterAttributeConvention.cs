using System.Reflection;
using Fookbase.Api.Persistence.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Fookbase.Api.Persistence.Conventions;

internal sealed class IndexFilterAttributeConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (var attribute in entityType.ClrType.GetCustomAttributes<IndexFilterAttribute>(inherit: true))
            {
                var index = entityType.GetIndexes().SingleOrDefault(candidate =>
                    candidate.Properties.Select(property => property.Name).SequenceEqual(attribute.PropertyNames))
                    ?? throw new InvalidOperationException(
                        $"IndexFilter on '{entityType.DisplayName()}' does not match an index for '{string.Join(", ", attribute.PropertyNames)}'.");

                index.SetFilter(attribute.Filter, fromDataAnnotation: true);
            }
        }
    }
}
