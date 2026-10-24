using Assembly = System.Reflection.Assembly;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Meridian.ArchitectureTests;

/// <summary>
/// Enforces the dependency rule: Domain ← Application ← Infrastructure ← hosts (Api, Worker).
/// See docs/adr/0005-solution-structure.md.
/// </summary>
public sealed class LayeringTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.AssemblyMarker).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly ApiAssembly = Assembly.Load("Meridian.Api");
    private static readonly Assembly WorkerAssembly = Assembly.Load("Meridian.Worker");

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly, WorkerAssembly)
        .Build();

    private static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInAssembly(DomainAssembly).As("Domain layer");
    private static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInAssembly(ApplicationAssembly).As("Application layer");
    private static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInAssembly(InfrastructureAssembly).As("Infrastructure layer");
    private static readonly IObjectProvider<IType> HostLayer =
        Types().That().ResideInAssembly(ApiAssembly).Or().ResideInAssembly(WorkerAssembly).As("Host layer (Api, Worker)");

    [Fact]
    public void DomainDoesNotDependOnOuterLayers() =>
        Types().That().Are(DomainLayer).Should()
            .NotDependOnAny(ApplicationLayer).AndShould()
            .NotDependOnAny(InfrastructureLayer).AndShould()
            .NotDependOnAny(HostLayer)
            .Check(Architecture);

    [Fact]
    public void ApplicationDoesNotDependOnInfrastructureOrHosts() =>
        Types().That().Are(ApplicationLayer).Should()
            .NotDependOnAny(InfrastructureLayer).AndShould()
            .NotDependOnAny(HostLayer)
            .Check(Architecture);

    [Fact]
    public void InfrastructureDoesNotDependOnHosts() =>
        Types().That().Are(InfrastructureLayer).Should()
            .NotDependOnAny(HostLayer)
            .Check(Architecture);

    [Fact]
    public void InfrastructureExposesOnlyItsCompositionEntryPoint() =>
        Types().That().Are(InfrastructureLayer).And().ArePublic().Should()
            .HaveFullName(typeof(Infrastructure.DependencyInjection).FullName!)
            .Check(Architecture);

    [Fact]
    public void DomainReferencesOnlyTheBaseClassLibrary() =>
        ReferencedAssemblyNames(DomainAssembly)
            .Where(name => !IsBaseClassLibrary(name))
            .ShouldBeEmpty();

    [Fact]
    public void ApplicationReferencesOnlyDomainAndAbstractions() =>
        ReferencedAssemblyNames(ApplicationAssembly)
            .Where(name => !IsBaseClassLibrary(name))
            .Except(["Meridian.Domain", "Microsoft.Extensions.DependencyInjection.Abstractions"])
            .ShouldBeEmpty();

    private static IEnumerable<string> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!);

    private static bool IsBaseClassLibrary(string name) =>
        name is "netstandard" or "mscorlib" || name.StartsWith("System", StringComparison.Ordinal);
}
