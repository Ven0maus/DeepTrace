namespace DeepTrace.Core.Models;

public record AssemblyInfo(
    string Name,
    string Location,
    long LastWriteTimeUtcTicks,
    string Hash,
    DateTime IndexedAt
);
