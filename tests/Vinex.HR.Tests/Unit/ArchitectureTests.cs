using System.Reflection;

namespace Vinex.HR.Tests.Unit;

/// <summary>Enforces the dependency rules from the design (Domain/Contracts stay independent; no upward references).</summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Vinex.HR.Domain.Entities.User).Assembly;
    private static readonly Assembly Contracts = typeof(Vinex.HR.Contracts.Security.Permissions).Assembly;
    private static readonly Assembly Application = typeof(Vinex.HR.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Vinex.HR.Infrastructure.DependencyInjection).Assembly;

    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

    [Fact]
    public void Domain_references_only_the_base_class_library()
    {
        References(Domain).ShouldAllBe(n => n.StartsWith("System") || n == "netstandard" || n == "mscorlib");
    }

    [Fact]
    public void Contracts_only_reference_FluentValidation_and_the_base_class_library()
    {
        References(Contracts).ShouldAllBe(n => n.StartsWith("System") || n == "netstandard" || n == "FluentValidation");
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_or_API()
    {
        References(Application).ShouldNotContain("Vinex.HR.Infrastructure");
        References(Application).ShouldNotContain("Vinex.HR.API");
    }

    [Fact]
    public void Infrastructure_does_not_reference_API()
    {
        References(Infrastructure).ShouldNotContain("Vinex.HR.API");
    }
}
