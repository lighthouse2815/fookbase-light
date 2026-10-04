using System.ComponentModel;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Fookbase.Api.Persistence.Conventions;

internal sealed class DefaultValueAttributeConvention(ProviderConventionSetBuilderDependencies dependencies)
    : PropertyAttributeConventionBase<DefaultValueAttribute>(dependencies)
{
    protected override void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        DefaultValueAttribute attribute,
        MemberInfo clrMember,
        IConventionContext context) =>
        propertyBuilder.HasDefaultValue(attribute.Value, fromDataAnnotation: true);
}
