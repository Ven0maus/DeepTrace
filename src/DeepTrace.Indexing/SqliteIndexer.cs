using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Mono.Cecil;
using DeepTrace.Core.Interfaces;
using DeepTrace.Core.Models;

namespace DeepTrace.Indexing;

public sealed class SqliteIndexer : IIndexer, IDisposable
{
    private readonly string _databasePath;
    private readonly IDecompiler _decompiler;
    private readonly AssemblyDiscoverer _discoverer;
    private readonly object _lock = new();

    public SqliteIndexer(string databasePath, IDecompiler decompiler)
    {
        _databasePath = databasePath;
        _decompiler = decompiler;
        _discoverer = new AssemblyDiscoverer();
        InitializeDatabase();
    }

    public IReadOnlyList<AssemblyInfo> DiscoverAssemblies(string directory)
        => _discoverer.Discover(directory);

    public void IndexAssembly(AssemblyInfo assembly)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

            var cmd = conn.Cmd("SELECT hash FROM assemblies WHERE name = @name")
                .P("@name", assembly.Name);
            var hash = cmd.ExecuteScalar() as string;
            if (hash == assembly.Hash)
                return;

            using var tx = conn.BeginTransaction();

            conn.Cmd("DELETE FROM assemblies WHERE name = @name", tx)
                .P("@name", assembly.Name).ExecuteNonQuery();

            var insertCmd = conn.Cmd(@"INSERT INTO assemblies (name, location, last_write_time, hash, indexed_at)
                VALUES (@name, @location, @lastWrite, @hash, @indexedAt)", tx)
                .P("@name", assembly.Name)
                .P("@location", assembly.Location)
                .P("@lastWrite", assembly.LastWriteTimeUtcTicks)
                .P("@hash", assembly.Hash)
                .P("@indexedAt", assembly.IndexedAt.ToString("o"));
            insertCmd.ExecuteNonQuery();

            IndexAssemblyMetadata(conn, assembly, tx);
            tx.Commit();
        }
    }

    public void IndexAssemblyAsync(AssemblyInfo assembly, CancellationToken cancellationToken = default)
        => IndexAssembly(assembly);

    public IReadOnlyList<TypeInfo> GetTypes(string assemblyName)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

        var types = new List<TypeInfo>();
        var symCmd = conn.Cmd(@"SELECT id, full_name, namespace, name, base_type
                FROM symbols WHERE assembly = @assembly AND kind = 'Type'")
            .P("@assembly", assemblyName);
        using var symReader = symCmd.ExecuteReader();

        while (symReader.Read())
        {
            var typeId = symReader.GetString(0);
            var fullName = symReader.GetString(1);
            var ns = symReader.GetString(2);
            var name = symReader.GetString(3);
            var baseType = symReader.IsDBNull(4) ? null : symReader.GetString(4);

            var type = new TypeInfo
            {
                SymbolId = typeId,
                FullName = fullName,
                Namespace = ns,
                Name = name,
                BaseTypeFullName = baseType,
                AssemblyName = assemblyName,
            };

            // Inline ReadStrings for interfaces
            var ifaceCmd = conn.Cmd("SELECT interface_name FROM type_interfaces WHERE type_id = @id")
                .P("@id", typeId);
            using var ifaceReader = ifaceCmd.ExecuteReader();
            var ifaceList = new List<string>();
            while (ifaceReader.Read())
                ifaceList.Add(ifaceReader.GetString(0));
            type.Interfaces = [.. ifaceList];

            // Inline ReadMethods
            var methodCmd = conn.Cmd(@"SELECT m.symbol_id, s.signature, m.is_static, m.is_virtual, m.is_abstract, m.is_override
                    FROM methods m JOIN symbols s ON m.symbol_id = s.id
                    WHERE m.symbol_id LIKE @prefix")
                .P("@prefix", $"{assemblyName}::{fullName}::%");
            using var methodReader = methodCmd.ExecuteReader();
            var methodList = new List<MethodInfo>();
            while (methodReader.Read())
            {
                var id = methodReader.GetString(0);
                var assembly = id.Split("::")[0];
                methodList.Add(new MethodInfo
                {
                    SymbolId = id,
                    Signature = methodReader.GetString(1),
                    DeclaringTypeFullName = fullName,
                    AssemblyName = assembly,
                    IsStatic = methodReader.GetInt32(2) == 1,
                    IsVirtual = methodReader.GetInt32(3) == 1,
                    IsAbstract = methodReader.GetInt32(4) == 1,
                    IsOverride = methodReader.GetInt32(5) == 1,
                });
            }
            type.Methods = [.. methodList];

            // Inline ReadFields
            var fieldCmd = conn.Cmd(@"SELECT id, type, is_static, is_literal
                    FROM fields WHERE symbol_id LIKE @prefix")
                .P("@prefix", $"{typeId}::%");
            using var fieldReader = fieldCmd.ExecuteReader();
            var fieldList = new List<FieldInfo>();
            while (fieldReader.Read())
            {
                var id = fieldReader.GetString(0);
                var parts = id.Split("::");
                var fieldName = parts.Length > 2 ? parts[2] : id;
                var fieldAssembly = parts.Length > 0 ? parts[0] : string.Empty;
                fieldList.Add(new FieldInfo
                {
                    SymbolId = id,
                    FullName = $"{fullName}.{fieldName}",
                    DeclaringTypeFullName = fullName,
                    Name = fieldName,
                    Type = fieldReader.GetString(1),
                    AssemblyName = fieldAssembly,
                    IsStatic = fieldReader.GetInt32(2) == 1,
                    IsLiteral = fieldReader.GetInt32(3) == 1,
                });
            }
            type.Fields = [.. fieldList];

            types.Add(type);
        }

        return [.. types];
        }
    }

    public MethodInfo? GetMethod(string symbolId)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

        var methodCmd = conn.Cmd(@"SELECT s.full_name, s.signature, s.summary, m.declaring_type, m.body,
                m.source_file, m.start_line, m.start_column, m.end_line, m.end_column,
                m.is_static, m.is_virtual, m.is_abstract, m.is_override,
                s2.name AS assembly_name
                FROM methods m
                JOIN symbols s ON m.symbol_id = s.id
                JOIN assemblies s2 ON s.assembly = s2.name
                WHERE m.symbol_id = @id")
            .P("@id", symbolId);
        using var reader = methodCmd.ExecuteReader();

        if (!reader.Read())
            return null;

        var method = new MethodInfo
        {
            SymbolId = symbolId,
            FullName = reader.GetString(0),
            Signature = reader.GetString(1),
            Summary = reader.IsDBNull(2) ? null : reader.GetString(2),
            DeclaringTypeFullName = reader.GetString(3),
            Body = reader.IsDBNull(4) ? null : reader.GetString(4),
            SourceLocation = reader.IsDBNull(5) ? null : new SourceLocation(
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
                reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9)
            ),
            IsStatic = reader.GetInt32(10) == 1,
            IsVirtual = reader.GetInt32(11) == 1,
            IsAbstract = reader.GetInt32(12) == 1,
            IsOverride = reader.GetInt32(13) == 1,
            AssemblyName = reader.GetString(14),
        };

        // Inline ReadStrings for attributes
        var attrCmd = conn.Cmd("SELECT attribute FROM method_attributes WHERE method_id = @id")
            .P("@id", symbolId);
        using var attrReader = attrCmd.ExecuteReader();
        var attrList = new List<string>();
        while (attrReader.Read())
            attrList.Add(attrReader.GetString(0));
        method.Attributes = [.. attrList];

        var paramList = new List<string>();
        var paramCmd = conn.Cmd(@"SELECT param_name, param_type FROM method_parameters
                WHERE method_id = @id ORDER BY param_index")
            .P("@id", symbolId);
        using var paramReader = paramCmd.ExecuteReader();
        while (paramReader.Read())
            paramList.Add($"{paramReader.GetString(1)} {paramReader.GetString(0)}");
        method.Parameters = paramList;

        return method;
        }
    }

    public IReadOnlyList<MethodInfo> GetMethods(string assemblyName)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

        var methods = new List<MethodInfo>();
        var cmd = conn.Cmd(@"SELECT m.symbol_id, s.full_name, s.signature, s.summary, m.declaring_type,
                m.is_static, m.is_virtual, m.is_abstract, m.is_override
                FROM methods m
                JOIN symbols s ON m.symbol_id = s.id
                WHERE s.assembly = @assembly AND s.kind = 'Method'")
            .P("@assembly", assemblyName);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            methods.Add(new MethodInfo
            {
                SymbolId = reader.GetString(0),
                FullName = reader.GetString(1),
                Signature = reader.GetString(2),
                Summary = reader.IsDBNull(3) ? null : reader.GetString(3),
                DeclaringTypeFullName = reader.GetString(4),
                AssemblyName = assemblyName,
                IsStatic = reader.GetInt32(5) == 1,
                IsVirtual = reader.GetInt32(6) == 1,
                IsAbstract = reader.GetInt32(7) == 1,
                IsOverride = reader.GetInt32(8) == 1,
            });
        }

        return [.. methods];
        }
    }

    public IReadOnlyList<SearchResult> Search(string query, int limit = 50)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

        var results = new List<SearchResult>();

        var symCmd = conn.Cmd(@"SELECT s.id, s.full_name, s.name, s.kind, s.signature,
                s2.name AS assembly
                FROM symbols s
                JOIN assemblies s2 ON s.assembly = s2.name
                WHERE s.kind = 'Method' AND (
                    s.full_name LIKE '%' || @query || '%'
                    OR s.name LIKE '%' || @query || '%'
                    OR s.signature LIKE '%' || @query || '%'
                )
                ORDER BY CASE WHEN s.name = @exactName THEN 0 ELSE 1 END, s.full_name
                LIMIT @limit")
            .P("@query", query)
            .P("@exactName", query)
            .P("@limit", limit);
        using var symReader = symCmd.ExecuteReader();

        while (symReader.Read())
        {
            results.Add(new SearchResult
            {
                SymbolId = symReader.GetString(0),
                Name = symReader.GetString(2),
                Kind = Enum.Parse<SymbolKind>(symReader.GetString(3)),
                FullyQualifiedName = symReader.GetString(1),
                Signature = symReader.IsDBNull(4) ? null : symReader.GetString(4),
                Assembly = symReader.GetString(5),
            });
        }

        var methCmd = conn.Cmd(@"SELECT m.symbol_id, s.full_name, s.name, s2.name AS assembly, m.body
                FROM methods m
                JOIN symbols s ON m.symbol_id = s.id
                JOIN assemblies s2 ON s.assembly = s2.name
                WHERE m.body LIKE '%' || @query || '%'
                LIMIT @limit")
            .P("@query", query)
            .P("@limit", limit);
        using var methReader = methCmd.ExecuteReader();

        while (methReader.Read())
        {
            var symbolId = methReader.GetString(0);
            if (!results.Any(r => r.SymbolId == symbolId))
            {
                results.Add(new SearchResult
                {
                    SymbolId = symbolId,
                    Name = methReader.GetString(2),
                    Kind = SymbolKind.Method,
                    FullyQualifiedName = methReader.GetString(1),
                    Assembly = methReader.GetString(3),
                    Excerpt = GetExcerpt(methReader.GetString(4)),
                });
            }
        }

        return [.. results.Take(limit)];
        }
    }

    public TypeInfo? GetTypeInfo(string assemblyName, string typeFullName)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

        var symCmd = conn.Cmd(@"SELECT id, full_name, namespace, name, base_type
                FROM symbols WHERE assembly = @assembly AND kind = 'Type' AND full_name = @fullName")
            .P("@assembly", assemblyName)
            .P("@fullName", typeFullName);
        using var symReader = symCmd.ExecuteReader();

        if (!symReader.Read())
            return null;

        var typeId = symReader.GetString(0);
        var fullName = symReader.GetString(1);
        var ns = symReader.GetString(2);
        var name = symReader.GetString(3);
        var baseType = symReader.IsDBNull(4) ? null : symReader.GetString(4);

        var type = new TypeInfo
        {
            SymbolId = typeId,
            FullName = fullName,
            Namespace = ns,
            Name = name,
            BaseTypeFullName = baseType,
            AssemblyName = assemblyName,
        };

        // Interfaces
        var ifaceCmd = conn.Cmd("SELECT interface_name FROM type_interfaces WHERE type_id = @id")
            .P("@id", typeId);
        using var ifaceReader = ifaceCmd.ExecuteReader();
        var ifaceList = new List<string>();
        while (ifaceReader.Read())
            ifaceList.Add(ifaceReader.GetString(0));
        type.Interfaces = [.. ifaceList];

        // Attributes
        var attrCmd = conn.Cmd("SELECT attribute FROM type_attributes WHERE type_id = @id")
            .P("@id", typeId);
        using var attrReader = attrCmd.ExecuteReader();
        var attrList = new List<string>();
        while (attrReader.Read())
            attrList.Add(attrReader.GetString(0));
        type.Attributes = [.. attrList];

        // Methods
        var methodCmd = conn.Cmd(@"SELECT m.symbol_id, s.signature, m.is_static, m.is_virtual, m.is_abstract, m.is_override
                FROM methods m JOIN symbols s ON m.symbol_id = s.id
                WHERE m.symbol_id LIKE @prefix")
            .P("@prefix", $"{assemblyName}::{fullName}::%");
        using var methodReader = methodCmd.ExecuteReader();
        var methodList = new List<MethodInfo>();
        while (methodReader.Read())
        {
            var id = methodReader.GetString(0);
            var assembly = id.Split("::")[0];
            var methodParts = id.Split("::");
            var methodName = methodParts.Length > 2 ? methodParts[2] : id;
            methodList.Add(new MethodInfo
            {
                SymbolId = id,
                Name = methodName,
                Signature = methodReader.GetString(1),
                DeclaringTypeFullName = fullName,
                AssemblyName = assembly,
                IsStatic = methodReader.GetInt32(2) == 1,
                IsVirtual = methodReader.GetInt32(3) == 1,
                IsAbstract = methodReader.GetInt32(4) == 1,
                IsOverride = methodReader.GetInt32(5) == 1,
            });
        }
        type.Methods = [.. methodList];

        // Load method parameters
        foreach (var method in type.Methods)
        {
            var paramCmd = conn.Cmd(@"SELECT param_name, param_type FROM method_parameters
                    WHERE method_id = @id ORDER BY param_index")
                .P("@id", method.SymbolId);
            using var paramReader = paramCmd.ExecuteReader();
            var paramList = new List<string>();
            while (paramReader.Read())
                paramList.Add($"{paramReader.GetString(1)} {paramReader.GetString(0)}");
            method.Parameters = paramList;
        }

        // Fields
        var fieldCmd = conn.Cmd(@"SELECT id, type, is_static, is_literal
                FROM fields WHERE symbol_id LIKE @prefix")
            .P("@prefix", $"{typeId}::%");
        using var fieldReader = fieldCmd.ExecuteReader();
        var fieldList = new List<FieldInfo>();
        while (fieldReader.Read())
        {
            var id = fieldReader.GetString(0);
            var parts = id.Split("::");
            var fieldName = parts.Length > 2 ? parts[2] : id;
            var fieldAssembly = parts.Length > 0 ? parts[0] : string.Empty;
            fieldList.Add(new FieldInfo
            {
                SymbolId = id,
                FullName = $"{fullName}.{fieldName}",
                DeclaringTypeFullName = fullName,
                Name = fieldName,
                Type = fieldReader.GetString(1),
                AssemblyName = fieldAssembly,
                IsStatic = fieldReader.GetInt32(2) == 1,
                IsLiteral = fieldReader.GetInt32(3) == 1,
            });
        }
        type.Fields = [.. fieldList];

        // Derived types
        var derivedCmd = conn.Cmd(@"SELECT s.full_name FROM type_hierarchy h
                JOIN symbols s ON h.type_id = s.id
                WHERE h.base_type_id = @typeId")
            .P("@typeId", typeId);
        using var derivedReader = derivedCmd.ExecuteReader();
        var derivedList = new List<string>();
        while (derivedReader.Read())
            derivedList.Add(derivedReader.GetString(0));
        type.DerivedTypes = [.. derivedList];

        // Base types
        var baseCmd = conn.Cmd(@"SELECT s.full_name FROM type_hierarchy h
                JOIN symbols s ON h.base_type_id = s.id
                WHERE h.type_id = @typeId")
            .P("@typeId", typeId);
        using var baseReader = baseCmd.ExecuteReader();
        var baseList = new List<string>();
        while (baseReader.Read())
            baseList.Add(baseReader.GetString(0));
        type.BaseTypes = [.. baseList];

        // Type references
        var refCmd = conn.Cmd(@"SELECT s.full_name FROM type_references r
                JOIN symbols s ON r.target_type_id = s.id
                WHERE r.source_type_id = @typeId")
            .P("@typeId", typeId);
        using var refReader = refCmd.ExecuteReader();
        var refList = new List<string>();
        while (refReader.Read())
            refList.Add(refReader.GetString(0));
        type.TypeReferences = [.. refList];

        return type;
        }
    }

    public IReadOnlyList<string> GetCallers(string methodSymbolId)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

            var cmd = conn.Cmd(@"SELECT s.full_name FROM method_callers c
                    JOIN symbols s ON c.caller_id = s.id
                    WHERE c.callee_id = @methodId")
                .P("@methodId", methodSymbolId);
            using var reader = cmd.ExecuteReader();
            var callers = new List<string>();
            while (reader.Read())
                callers.Add(reader.GetString(0));
            return [.. callers];
        }
    }

    public IReadOnlyList<string> GetCallees(string methodSymbolId)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

            var cmd = conn.Cmd(@"SELECT s.full_name FROM method_callees c
                    JOIN symbols s ON c.called_id = s.id
                    WHERE c.method_id = @methodId")
                .P("@methodId", methodSymbolId);
            using var reader = cmd.ExecuteReader();
            var callees = new List<string>();
            while (reader.Read())
                callees.Add(reader.GetString(0));
            return [.. callees];
        }
    }

    public IReadOnlyList<string> GetDerivedTypes(string typeSymbolId)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

            var cmd = conn.Cmd(@"SELECT s.full_name FROM type_hierarchy h
                    JOIN symbols s ON h.type_id = s.id
                    WHERE h.base_type_id = @typeId")
                .P("@typeId", typeSymbolId);
            using var reader = cmd.ExecuteReader();
            var types = new List<string>();
            while (reader.Read())
                types.Add(reader.GetString(0));
            return [.. types];
        }
    }

    public IReadOnlyList<string> GetBaseTypes(string typeSymbolId)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

            var cmd = conn.Cmd(@"SELECT s.full_name FROM type_hierarchy h
                    JOIN symbols s ON h.base_type_id = s.id
                    WHERE h.type_id = @typeId")
                .P("@typeId", typeSymbolId);
            using var reader = cmd.ExecuteReader();
            var types = new List<string>();
            while (reader.Read())
                types.Add(reader.GetString(0));
            return [.. types];
        }
    }

    public IReadOnlyList<SearchResult> SearchReferences(string symbolId)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

            var results = new List<SearchResult>();

            // Callers
            var callersCmd = conn.Cmd(@"SELECT s.full_name, s.name, s.kind, s2.name AS assembly
                    FROM method_callers c
                    JOIN symbols s ON c.caller_id = s.id
                    JOIN assemblies s2 ON s.assembly = s2.name
                    WHERE c.callee_id = @symbolId AND s.kind = 'Method'")
                .P("@symbolId", symbolId);
            using var callersReader = callersCmd.ExecuteReader();
            while (callersReader.Read())
            {
                results.Add(new SearchResult
                {
                    SymbolId = callersReader.GetString(0),
                    Name = callersReader.GetString(1),
                    Kind = SymbolKind.Method,
                    FullyQualifiedName = callersReader.GetString(0),
                    Assembly = callersReader.GetString(3),
                    Excerpt = "caller",
                });
            }

            // Callees
            var calleesCmd = conn.Cmd(@"SELECT s.full_name, s.name, s.kind, s2.name AS assembly
                    FROM method_callees c
                    JOIN symbols s ON c.called_id = s.id
                    JOIN assemblies s2 ON s.assembly = s2.name
                    WHERE c.method_id = @symbolId AND s.kind = 'Method'")
                .P("@symbolId", symbolId);
            using var calleesReader = calleesCmd.ExecuteReader();
            while (calleesReader.Read())
            {
                results.Add(new SearchResult
                {
                    SymbolId = calleesReader.GetString(0),
                    Name = calleesReader.GetString(1),
                    Kind = SymbolKind.Method,
                    FullyQualifiedName = calleesReader.GetString(0),
                    Assembly = calleesReader.GetString(3),
                    Excerpt = "callee",
                });
            }

            // Type references
            var typeRefsCmd = conn.Cmd(@"SELECT s2.full_name, s2.name, s2.kind, s3.name AS assembly
                    FROM type_references r
                    JOIN symbols s2 ON r.target_type_id = s2.id
                    JOIN symbols s1 ON r.source_type_id = s1.id
                    JOIN assemblies s3 ON s1.assembly = s3.name
                    WHERE r.source_type_id = @typeId OR s1.full_name LIKE @prefix")
                .P("@typeId", symbolId)
                .P("@prefix", $"{symbolId}::%");
            using var typeRefsReader = typeRefsCmd.ExecuteReader();
            while (typeRefsReader.Read())
            {
                results.Add(new SearchResult
                {
                    SymbolId = typeRefsReader.GetString(0),
                    Name = typeRefsReader.GetString(1),
                    Kind = Enum.Parse<SymbolKind>(typeRefsReader.GetString(2)),
                    FullyQualifiedName = typeRefsReader.GetString(0),
                    Assembly = typeRefsReader.GetString(3),
                    Excerpt = "type reference",
                });
            }

            return [.. results];
        }
    }

    public bool IsAssemblyIndexed(string assemblyName)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection($"Data Source={_databasePath}");
            conn.Open();

        return (long)(conn.Cmd("SELECT COUNT(*) FROM assemblies WHERE name = @name")
            .P("@name", assemblyName).ExecuteScalar() ?? 0L) > 0;
        }
    }

    public long GetAssemblyLastWriteTime(string assemblyPath)
        => File.GetLastWriteTimeUtc(assemblyPath).Ticks;

    public string GetAssemblyHash(string assemblyPath)
    {
        using var sha256 = SHA256.Create();
        using var fs = File.OpenRead(assemblyPath);
        var hash = sha256.ComputeHash(fs);
        return Convert.ToHexString(hash);
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection($"Data Source={_databasePath}");
        conn.Open();
        conn.Cmd(DatabaseSchema.CreateTables).ExecuteNonQuery();
    }

    private void IndexAssemblyMetadata(SqliteConnection conn, AssemblyInfo assembly, SqliteTransaction? tx)
    {
        var assemblyPath = assembly.Location;
        var assemblyName = assembly.Name;

        try
        {
            using var assemblyDef = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters());
            IndexTypes(conn, assemblyDef, assemblyName, tx);
            IndexTypeMethods(conn, assemblyDef, assemblyName, tx);
            IndexTypeHierarchy(conn, assemblyDef, assemblyName, tx);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to index {assemblyName}: {ex.Message}");
        }
    }

    private void IndexTypes(SqliteConnection conn, AssemblyDefinition assemblyDef, string assemblyName, SqliteTransaction? tx)
    {
        var insertType = conn.Cmd(@"INSERT OR REPLACE INTO symbols (id, assembly, full_name, namespace, name, kind, signature)
                VALUES (@id, @assembly, @fullName, @ns, @name, 'Type', @signature)", tx);
        var insertBase = conn.Cmd("UPDATE symbols SET base_type = @baseType WHERE id = @id", tx);
        var insertIface = conn.Cmd("INSERT OR IGNORE INTO type_interfaces (type_id, interface_name) VALUES (@typeId, @iface)", tx);
        var insertAttr = conn.Cmd("INSERT OR IGNORE INTO type_attributes (type_id, attribute) VALUES (@typeId, @attr)", tx);

        foreach (var type in assemblyDef.MainModule.Types)
        {
            if (type.Namespace == "<>c" || type.Namespace == "<PrivateImplementationDetails>")
                continue;

            var typeId = $"{assemblyName}::{type.FullName}";

            insertType.P(new (string name, object value)[]
            {
                ("@id", typeId), ("@assembly", assemblyName),
                ("@fullName", type.FullName), ("@ns", type.Namespace),
                ("@name", type.Name), ("@signature", type.FullName),
            }).ExecuteNonQuery();

            if (type.BaseType != null && !type.BaseType.FullName.StartsWith("System."))
            {
                insertBase.P(new (string name, object value)[] { ("@baseType", type.BaseType.FullName), ("@id", typeId) }).ExecuteNonQuery();
            }

            foreach (var iface in type.Interfaces)
                insertIface.P(new (string name, object value)[] { ("@typeId", typeId), ("@iface", iface.InterfaceType.FullName) }).ExecuteNonQuery();

            foreach (var attr in type.CustomAttributes)
                insertAttr.P(new (string name, object value)[] { ("@typeId", typeId), ("@attr", attr.AttributeType.FullName) }).ExecuteNonQuery();
        }
    }

    private void IndexTypeMethods(SqliteConnection conn, AssemblyDefinition assemblyDef, string assemblyName, SqliteTransaction? tx)
    {
        var insertMethod = conn.Cmd(@"INSERT OR REPLACE INTO symbols (id, assembly, full_name, namespace, name, kind, signature, summary)
                VALUES (@id, @assembly, @fullName, @ns, @name, 'Method', @signature, @summary)", tx);
        var insertMethodBody = conn.Cmd(@"INSERT INTO methods (symbol_id, declaring_type, body, summary, source_file, start_line,
                start_column, end_line, end_column, is_static, is_virtual, is_abstract, is_override)
                VALUES (@id, @declaringType, @body, @summary, @sourceFile, @startLine, @startCol,
                @endLine, @endCol, @isStatic, @isVirtual, @isAbstract, @isOverride)", tx);
        var insertParam = conn.Cmd("INSERT OR REPLACE INTO method_parameters (method_id, param_index, param_name, param_type) VALUES (@methodId, @paramIndex, @paramName, @paramType)", tx);
        var insertAttr = conn.Cmd("INSERT OR IGNORE INTO method_attributes (method_id, attribute) VALUES (@methodId, @attr)", tx);
        var insertField = conn.Cmd(@"INSERT OR REPLACE INTO symbols (id, assembly, full_name, namespace, name, kind, signature)
                VALUES (@id, @assembly, @fullName, @ns, @name, 'Field', @signature)", tx);
        var insertFieldData = conn.Cmd(@"INSERT INTO fields (id, symbol_id, declaring_type, type, is_static, is_literal, literal_value)
                VALUES (@id, @symbolId, @declaringType, @type, @isStatic, @isLiteral, @literalValue)", tx);
        var insertCallee = conn.Cmd("INSERT OR IGNORE INTO method_callees (method_id, called_id) VALUES (@methodId, @calledId)", tx);
        var insertCaller = conn.Cmd("INSERT OR IGNORE INTO method_callers (caller_id, callee_id) VALUES (@callerId, @calleeId)", tx);
        var insertHierarchy = conn.Cmd("INSERT OR IGNORE INTO type_hierarchy (type_id, base_type_id) VALUES (@typeId, @baseTypeId)", tx);
        var insertTypeRef = conn.Cmd("INSERT OR IGNORE INTO type_references (source_type_id, target_type_id) VALUES (@sourceId, @targetId)", tx);

        foreach (var type in assemblyDef.MainModule.Types)
        {
            foreach (var method in type.Methods)
            {
                if (method.Name.StartsWith("set_", StringComparison.Ordinal) ||
                    method.Name.StartsWith("get_", StringComparison.Ordinal) ||
                    method.Name.StartsWith("add_", StringComparison.Ordinal) ||
                    method.Name.StartsWith("remove_", StringComparison.Ordinal))
                    continue;

                var methodId = $"{assemblyName}::{type.FullName}::{method.Name}::{GetSignature(method)}";
                var fullName = $"{type.FullName}.{method.Name}";
                var signature = GetSignature(method);

                string? summary = null;
                if (!method.IsAbstract && method.HasBody && method.Body.Instructions != null)
                {
                    var body = _decompiler.TryDecompileMethod(assemblyDef.MainModule.FileName, type.FullName, signature);
                    var isOverride = method.IsVirtual && method.Name != ".ctor" && method.Name != ".cctor" && method.HasBody;
                    summary = MethodSummaryGenerator.Generate(new MethodInfo { FullName = fullName, Name = method.Name, Signature = signature, IsStatic = method.IsStatic, IsVirtual = method.IsVirtual, IsAbstract = method.IsAbstract, IsOverride = isOverride, Attributes = new List<string>() }, body);
                }

                insertMethod.P(new (string name, object value)[]
                {
                    ("@id", methodId), ("@assembly", assemblyName),
                    ("@fullName", fullName), ("@ns", type.Namespace),
                    ("@name", method.Name), ("@signature", signature),
                    ("@summary", summary ?? string.Empty),
                }).ExecuteNonQuery();

                if (summary != null)
                {
                    var body = _decompiler.TryDecompileMethod(assemblyDef.MainModule.FileName, type.FullName, signature);
                    insertMethodBody.P(new (string name, object value)[]
                    {
                        ("@id", methodId), ("@declaringType", type.FullName),
                        ("@body", body ?? string.Empty), ("@summary", summary), ("@sourceFile", (object)null!),
                        ("@startLine", (object)null!), ("@startCol", (object)null!),
                        ("@endLine", (object)null!), ("@endCol", (object)null!),
                        ("@isStatic", method.IsStatic ? 1 : 0),
                        ("@isVirtual", method.IsVirtual ? 1 : 0),
                        ("@isAbstract", method.IsAbstract ? 1 : 0),
                        ("@isOverride", method.IsVirtual ? 1 : 0),
                    }).ExecuteNonQuery();

                    // Extract call graph from IL
                    ExtractCallGraph(conn, assemblyDef, method, type.FullName, assemblyName, insertCallee, insertCaller);
                }

                for (var i = 0; i < method.Parameters.Count; i++)
                {
                    var param = method.Parameters[i];
                    insertParam.P(new (string name, object value)[]
                    {
                        ("@methodId", methodId), ("@paramIndex", i),
                        ("@paramName", param.Name), ("@paramType", param.ParameterType.FullName),
                    }).ExecuteNonQuery();
                }

                foreach (var attr in method.CustomAttributes)
                    insertAttr.P(new (string name, object value)[] { ("@methodId", methodId), ("@attr", attr.AttributeType.FullName) }).ExecuteNonQuery();
            }

            foreach (var field in type.Fields)
            {
                if (field.Name.StartsWith("<") && field.Name.EndsWith(">"))
                    continue;

                var fieldId = $"{assemblyName}::{type.FullName}::{field.Name}";
                var fieldFullName = $"{type.FullName}.{field.Name}";

                insertField.P(new (string name, object value)[]
                {
                    ("@id", fieldId), ("@assembly", assemblyName),
                    ("@fullName", fieldFullName), ("@ns", type.Namespace),
                    ("@name", field.Name), ("@signature", field.FieldType.FullName),
                }).ExecuteNonQuery();

                var hasDefault = field.HasConstant;
                var defaultValue = hasDefault ? (object)field.Constant! : null!;

                insertFieldData.P(new (string name, object value)[]
                {
                    ("@id", fieldId), ("@symbolId", fieldId),
                    ("@declaringType", type.FullName), ("@type", field.FieldType.FullName),
                    ("@isStatic", field.IsStatic ? 1 : 0),
                    ("@isLiteral", (hasDefault || field.IsLiteral) ? 1 : 0),
                    ("@literalValue", defaultValue),
                }).ExecuteNonQuery();
            }
        }
    }

    private void ExtractCallGraph(SqliteConnection conn, AssemblyDefinition assemblyDef, MethodDefinition method, string declaringType, string assemblyName, SqliteCommand insertCallee, SqliteCommand insertCaller)
    {
        if (method.Body == null || method.Body.Instructions == null)
            return;

        foreach (var instr in method.Body.Instructions)
        {
            if (instr.OpCode != Mono.Cecil.Cil.OpCodes.Call && instr.OpCode != Mono.Cecil.Cil.OpCodes.Callvirt)
                continue;

            if (instr.Operand is not MethodReference methodRef)
                continue;

            var calleeId = ResolveMethodId(assemblyDef, methodRef, assemblyName);
            if (calleeId == null)
                continue;

            var callerId = $"{assemblyName}::{declaringType}::{method.Name}::{GetSignature(method)}";
            insertCallee.P(new (string name, object value)[] { ("@methodId", callerId), ("@calledId", calleeId) }).ExecuteNonQuery();
            insertCaller.P(new (string name, object value)[] { ("@callerId", callerId), ("@calleeId", calleeId) }).ExecuteNonQuery();
        }
    }

    private static string? ResolveMethodId(AssemblyDefinition assemblyDef, MethodReference methodRef, string assemblyName)
    {
        var declaringType = methodRef.DeclaringType;
        if (declaringType == null)
            return null;

        // Handle generic instance types — unwrap to the generic type definition
        while (declaringType is Mono.Cecil.GenericInstanceType genericInst)
        {
            declaringType = genericInst.ElementType;
        }

        // Search within the same assembly first
        var typeDef = assemblyDef.MainModule.Types.FirstOrDefault(t => t.FullName == declaringType.FullName);
        if (typeDef == null)
        {
            // Try generic type definition
            var genericName = declaringType.FullName;
            var genericIdx = genericName.IndexOf('`');
            if (genericIdx > 0)
            {
                genericName = genericName.Substring(0, genericIdx);
            }
            typeDef = assemblyDef.MainModule.Types.FirstOrDefault(t => t.FullName == genericName);
        }

        if (typeDef == null)
            return null;

        var methodDef = typeDef.Methods.FirstOrDefault(m => m.Name == methodRef.Name && m.Parameters.Count == methodRef.Parameters.Count);
        if (methodDef == null)
            return null;

        return $"{assemblyName}::{typeDef.FullName}::{methodDef.Name}::{GetSignature(methodDef)}";
    }

    private void IndexTypeHierarchy(SqliteConnection conn, AssemblyDefinition assemblyDef, string assemblyName, SqliteTransaction? tx)
    {
        var insertHierarchy = conn.Cmd("INSERT OR IGNORE INTO type_hierarchy (type_id, base_type_id) VALUES (@typeId, @baseTypeId)", tx);
        var insertTypeRef = conn.Cmd("INSERT OR IGNORE INTO type_references (source_type_id, target_type_id) VALUES (@sourceId, @targetId)", tx);

        var allTypeIds = new Dictionary<string, string>();
        foreach (var type in assemblyDef.MainModule.Types)
        {
            if (type.Namespace == "<>c" || type.Namespace == "<PrivateImplementationDetails>")
                continue;
            var typeId = $"{assemblyName}::{type.FullName}";
            allTypeIds[type.FullName] = typeId;
        }

        foreach (var type in assemblyDef.MainModule.Types)
        {
            if (type.Namespace == "<>c" || type.Namespace == "<PrivateImplementationDetails>")
                continue;

            var sourceId = $"{assemblyName}::{type.FullName}";

            // Type hierarchy: base type
            if (type.BaseType != null && !type.BaseType.FullName.StartsWith("System."))
            {
                if (allTypeIds.TryGetValue(type.BaseType.FullName, out var baseId))
                {
                    insertHierarchy.P(new (string name, object value)[] { ("@typeId", sourceId), ("@baseTypeId", baseId) }).ExecuteNonQuery();
                }
            }

            // Type references: fields, parameters, locals, interfaces
            foreach (var field in type.Fields)
            {
                RecordTypeRef(insertTypeRef, sourceId, field.FieldType, allTypeIds);
            }

            foreach (var method in type.Methods)
            {
                RecordTypeRef(insertTypeRef, sourceId, method.ReturnType, allTypeIds);
                foreach (var param in method.Parameters)
                    RecordTypeRef(insertTypeRef, sourceId, param.ParameterType, allTypeIds);
                if (method.Body != null && method.Body.Variables != null)
                {
                    foreach (var local in method.Body.Variables)
                        RecordTypeRef(insertTypeRef, sourceId, local.VariableType, allTypeIds);
                }
            }

            foreach (var iface in type.Interfaces)
                RecordTypeRef(insertTypeRef, sourceId, iface.InterfaceType, allTypeIds);
        }
    }

    private static void RecordTypeRef(SqliteCommand insertTypeRef, string sourceId, TypeReference typeRef, Dictionary<string, string> allTypeIds)
    {
        if (typeRef == null) return;

        var fullName = typeRef.FullName;
        if (string.IsNullOrEmpty(fullName) || fullName.StartsWith("<") || fullName.StartsWith("System."))
            return;

        if (allTypeIds.TryGetValue(fullName, out var targetId) && targetId != sourceId)
            insertTypeRef.P(new (string name, object value)[] { ("@sourceId", sourceId), ("@targetId", targetId) }).ExecuteNonQuery();
    }

    private static string GetSignature(MethodDefinition method)
    {
        var parts = new List<string> { method.Name };
        parts.Add("(");
        parts.Add(string.Join(", ", method.Parameters.Select(p => p.ParameterType.FullName)));
        parts.Add(") : ");
        parts.Add(method.ReturnType.FullName);
        return string.Concat(parts);
    }

    private static string GetExcerpt(string body)
    {
        if (string.IsNullOrEmpty(body))
            return string.Empty;
        var lines = body.Split('\n');
        var start = Math.Max(0, lines.Length / 2 - 3);
        var end = Math.Min(lines.Length, start + 7);
        return string.Join("\n", lines[start..end]).Trim();
    }

    public void Dispose() { }
}
