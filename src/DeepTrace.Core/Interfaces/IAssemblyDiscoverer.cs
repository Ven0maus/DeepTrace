using DeepTrace.Core.Models;

namespace DeepTrace.Core.Interfaces;

/// <summary>
/// Discovers managed .NET assemblies in a directory.
/// </summary>
public interface IAssemblyDiscoverer
{
    /// <summary>
    /// Discover all managed .NET assemblies in a directory (recursively).
    /// </summary>
    /// <param name="directory">Directory path to search.</param>
    IReadOnlyList<AssemblyInfo> Discover(string directory);
}
