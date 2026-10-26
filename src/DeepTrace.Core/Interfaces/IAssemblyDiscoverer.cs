using DeepTrace.Core.Models;

namespace DeepTrace.Core.Interfaces;

public interface IAssemblyDiscoverer
{
    IReadOnlyList<AssemblyInfo> Discover(string directory);
}
