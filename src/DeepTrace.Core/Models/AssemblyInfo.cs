namespace DeepTrace.Core.Models;

/// <summary>
/// Metadata for a discovered managed .NET assembly.
/// </summary>
/// <param name="Name">Assembly name (e.g., "TestLib").</param>
/// <param name="Location">Full file path to the assembly DLL.</param>
/// <param name="LastWriteTimeUtcTicks">Last write time as .NET ticks.</param>
/// <param name="Hash">SHA256 hash of the assembly file.</param>
/// <param name="IndexedAt">Timestamp when this assembly was last indexed.</param>
public record AssemblyInfo(
    string Name,
    string Location,
    long LastWriteTimeUtcTicks,
    string Hash,
    DateTime IndexedAt
);
