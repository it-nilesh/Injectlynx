using System.Collections.Immutable;
using Injectlynx.Core.Models;

namespace Injectlynx.Core.Tests;

public sealed class ModelTests
{
    [Fact]
    public void ServiceTypeIdentity_OrdersByFullNameOrdinal()
    {
        var values = new[]
        {
            new ServiceTypeIdentity("Shop.Z", "ZService", "Shop.Z.ZService", false, 0),
            new ServiceTypeIdentity("Shop.A", "AService", "Shop.A.AService", false, 0)
        };

        Array.Sort(values, ServiceTypeIdentity.FullNameComparer);

        Assert.Equal("Shop.A.AService", values[0].FullName);
        Assert.Equal("Shop.Z.ZService", values[1].FullName);
    }

    [Fact]
    public void ServiceRegistrationModel_IsImmutableValueModel()
    {
        var contract = new ServiceTypeIdentity("Shop.Application", "IOrderService", "Shop.Application.IOrderService", false, 0);
        var implementation = new ServiceTypeIdentity("Shop.Application", "OrderService", "Shop.Application.OrderService", false, 0);
        var module = new ModuleIdentity("Application");
        var reason = RegistrationReason.Create(
            RegistrationReasonKind.MatchingInterface,
            "Matched IOrderService by convention.");

        var first = new ServiceRegistrationModel(
            contract,
            implementation,
            ServiceLifetimeModel.Scoped,
            module,
            RegistrationStrategy.MatchingInterface,
            reason,
            ImmutableArray<DependencyModel>.Empty,
            ImmutableArray<DecoratorModel>.Empty,
            SourceReference.None,
            RegistrationStatus.Valid);

        var second = first with { Lifetime = ServiceLifetimeModel.Singleton };

        Assert.Equal(ServiceLifetimeModel.Scoped, first.Lifetime);
        Assert.Equal(ServiceLifetimeModel.Singleton, second.Lifetime);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DiagnosticState_None_HasNoSourceOrSeverity()
    {
        Assert.Equal(DiagnosticSeverityModel.Hidden, DiagnosticState.None.Severity);
        Assert.Equal(SourceReference.None, DiagnosticState.None.Source);
        Assert.False(DiagnosticState.None.IsUnsafeToSuppress);
    }
}
