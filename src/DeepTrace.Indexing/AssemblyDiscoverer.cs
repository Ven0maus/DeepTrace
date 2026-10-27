using System.Security.Cryptography;
using System.Text;
using DeepTrace.Core.Interfaces;
using DeepTrace.Core.Models;
using Mono.Cecil;

namespace DeepTrace.Indexing;

/// <summary>
/// Discovers managed .NET assemblies in a directory, excluding system and framework assemblies.
/// </summary>
public sealed class AssemblyDiscoverer : IAssemblyDiscoverer
{
    private static readonly HashSet<string> ExcludedAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "mscorlib", "Microsoft.VisualBasic", "Microsoft.CSharp",
        "netstandard", "System.Core", "System.Runtime", "System.Collections",
        "System.Linq", "System.Threading", "System.Diagnostics",
        "System.Configuration", "System.Xml", "System.Xml.Linq",
        "System.Data", "System.Drawing", "System.Windows",
        "PresentationCore", "PresentationFramework", "WindowsBase",
        "UIAutomation", "ReachFramework", "WindowsForms",
        "ICSharpCode", "Mono.Cecil", "Microsoft.Bcl",
        "System.Memory", "System.Runtime.CompilerServices",
        "System.Diagnostics.Tools", "System.Console",
        "System.IO", "System.Text", "System.Linq.Expressions",
        "System.Reflection", "System.ComponentModel",
        "System.ComponentModel.TypeConverter",
        "System.Threading.Tasks", "System.Net.Http",
        "System.Private.CoreLib",
    };

    public IReadOnlyList<AssemblyInfo> Discover(string directory)
    {
        var assemblies = new List<AssemblyInfo>();

        foreach (var filePath in Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(directory, "*.exe", SearchOption.AllDirectories)))
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            if (IsExcluded(fileName) || !IsManagedAssembly(filePath))
                continue;

            try
            {
                var lastWrite = File.GetLastWriteTimeUtc(filePath);
                var hash = ComputeHash(filePath);
                assemblies.Add(new AssemblyInfo(
                    Name: fileName,
                    Location: filePath,
                    LastWriteTimeUtcTicks: lastWrite.Ticks,
                    Hash: hash,
                    IndexedAt: DateTime.UtcNow
                ));
            }
            catch (IOException)
            {
                // Skip locked files
            }
        }

        return [.. assemblies.OrderBy(a => a.Name)];
    }

    private static bool IsExcluded(string name)
    {
        return ExcludedAssemblies.Contains(name)
            || name.StartsWith("System.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.", StringComparison.Ordinal)
            || name.StartsWith("Windows.", StringComparison.Ordinal);
    }

    private static bool IsManagedAssembly(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            var header = new byte[512];
            var read = fs.Read(header, 0, header.Length);
            if (read < 2) return false;

            // MZ header (DOS header)
            if (header[0] != 'M' || header[1] != 'Z')
                return false;

            // PE signature at offset 0x3C
            if (read < 0x40) return false;
            var peOffset = BitConverter.ToUInt16(header, 0x3C);
            if (peOffset + 2 > (uint)read) return false;
            var peSig = BitConverter.ToUInt32(header, (int)peOffset);
            if (peSig != 0x00004550) // "PE\0\0"
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var fs = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(fs);
        return Convert.ToHexString(hash);
    }
}
