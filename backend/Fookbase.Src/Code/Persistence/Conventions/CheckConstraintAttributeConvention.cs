using System.Reflection;
using Fookbase.Api.Persistence.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Fookbase.Api.Persistence.Conventions;

internal sealed class CheckConstraintAttributeConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (var attribute in entityType.ClrType.GetCustomAttributes<CheckConstraintAttribute>(inherit: true))
            {
                entityType.AddCheckConstraint(attribute.Name, attribute.Sql, fromDataAnnotation: true);
            }
        }
    }
}
