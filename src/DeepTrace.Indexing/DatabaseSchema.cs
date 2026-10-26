namespace DeepTrace.Indexing;

internal static class DatabaseSchema
{
    public const string CreateTables = @"
        CREATE TABLE IF NOT EXISTS assemblies (
            name TEXT PRIMARY KEY,
            location TEXT NOT NULL,
            last_write_time BIGINT NOT NULL,
            hash TEXT NOT NULL,
            indexed_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS symbols (
            id TEXT PRIMARY KEY,
            assembly TEXT NOT NULL REFERENCES assemblies(name),
            full_name TEXT NOT NULL,
            namespace TEXT NOT NULL DEFAULT '',
            name TEXT NOT NULL,
            kind TEXT NOT NULL,
            signature TEXT,
            summary TEXT,
            base_type TEXT,
            FOREIGN KEY (assembly) REFERENCES assemblies(name) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS methods (
            symbol_id TEXT PRIMARY KEY REFERENCES symbols(id) ON DELETE CASCADE,
            declaring_type TEXT NOT NULL,
            body TEXT,
            summary TEXT,
            source_file TEXT,
            start_line INTEGER,
            start_column INTEGER,
            end_line INTEGER,
            end_column INTEGER,
            is_static INTEGER NOT NULL DEFAULT 0,
            is_virtual INTEGER NOT NULL DEFAULT 0,
            is_abstract INTEGER NOT NULL DEFAULT 0,
            is_override INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS fields (
            id TEXT PRIMARY KEY,
            symbol_id TEXT NOT NULL REFERENCES symbols(id) ON DELETE CASCADE,
            declaring_type TEXT NOT NULL,
            type TEXT NOT NULL,
            is_static INTEGER NOT NULL DEFAULT 0,
            is_literal INTEGER NOT NULL DEFAULT 0,
            literal_value TEXT
        );

        CREATE TABLE IF NOT EXISTS properties (
            id TEXT PRIMARY KEY,
            symbol_id TEXT NOT NULL REFERENCES symbols(id) ON DELETE CASCADE,
            declaring_type TEXT NOT NULL,
            type TEXT NOT NULL,
            is_static INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS type_interfaces (
            type_id TEXT NOT NULL REFERENCES symbols(id) ON DELETE CASCADE,
            interface_name TEXT NOT NULL,
            PRIMARY KEY (type_id, interface_name)
        );

        CREATE TABLE IF NOT EXISTS type_attributes (
            type_id TEXT NOT NULL REFERENCES symbols(id) ON DELETE CASCADE,
            attribute TEXT NOT NULL,
            PRIMARY KEY (type_id, attribute)
        );

        CREATE TABLE IF NOT EXISTS method_attributes (
            method_id TEXT NOT NULL REFERENCES methods(symbol_id) ON DELETE CASCADE,
            attribute TEXT NOT NULL,
            PRIMARY KEY (method_id, attribute)
        );

        CREATE TABLE IF NOT EXISTS method_parameters (
            method_id TEXT NOT NULL REFERENCES methods(symbol_id) ON DELETE CASCADE,
            param_index INTEGER NOT NULL,
            param_name TEXT NOT NULL,
            param_type TEXT NOT NULL,
            PRIMARY KEY (method_id, param_index)
        );

        CREATE TABLE IF NOT EXISTS method_callers (
            caller_id TEXT NOT NULL REFERENCES methods(symbol_id) ON DELETE CASCADE,
            callee_id TEXT NOT NULL REFERENCES methods(symbol_id) ON DELETE CASCADE,
            PRIMARY KEY (caller_id, callee_id)
        );

        CREATE TABLE IF NOT EXISTS method_callees (
            method_id TEXT NOT NULL REFERENCES methods(symbol_id) ON DELETE CASCADE,
            called_id TEXT NOT NULL REFERENCES methods(symbol_id) ON DELETE CASCADE,
            PRIMARY KEY (method_id, called_id)
        );

        CREATE TABLE IF NOT EXISTS type_hierarchy (
            type_id TEXT NOT NULL REFERENCES symbols(id) ON DELETE CASCADE,
            base_type_id TEXT NOT NULL REFERENCES symbols(id) ON DELETE CASCADE,
            PRIMARY KEY (type_id, base_type_id)
        );

        CREATE TABLE IF NOT EXISTS type_references (
            source_type_id TEXT NOT NULL REFERENCES symbols(id) ON DELETE CASCADE,
            target_type_id TEXT NOT NULL REFERENCES symbols(id) ON DELETE CASCADE,
            PRIMARY KEY (source_type_id, target_type_id)
        );

        CREATE VIRTUAL TABLE IF NOT EXISTS fts_symbols USING fts5(
            full_name,
            name,
            signature,
            summary,
            kind,
            content='symbols',
            content_rowid='rowid'
        );

        CREATE VIRTUAL TABLE IF NOT EXISTS fts_methods USING fts5(
            name,
            declaring_type,
            body,
            summary,
            content='methods',
            content_rowid='rowid'
        );

        CREATE TRIGGER IF NOT EXISTS ai_symbols AFTER INSERT ON symbols
        BEGIN
            INSERT INTO fts_symbols(rowid, full_name, name, signature, summary, kind)
            VALUES (NEW.rowid, NEW.full_name, NEW.name, NEW.signature, NEW.summary, NEW.kind);
        END;

        CREATE TRIGGER IF NOT EXISTS au_symbols AFTER UPDATE ON symbols
        BEGIN
            UPDATE fts_symbols SET
                full_name = NEW.full_name,
                name = NEW.name,
                signature = NEW.signature,
                summary = NEW.summary,
                kind = NEW.kind
            WHERE rowid = OLD.rowid;
        END;

        CREATE TRIGGER IF NOT EXISTS ad_symbols AFTER DELETE ON symbols
        BEGIN
            DELETE FROM fts_symbols WHERE rowid = OLD.rowid;
        END;

        CREATE TRIGGER IF NOT EXISTS ai_methods AFTER INSERT ON methods
        BEGIN
            INSERT INTO fts_methods(rowid, name, declaring_type, body, summary)
            VALUES (NEW.rowid, (SELECT name FROM symbols WHERE id=NEW.symbol_id),
                    NEW.declaring_type, NEW.body, NEW.summary);
        END;

        CREATE TRIGGER IF NOT EXISTS ad_methods AFTER DELETE ON methods
        BEGIN
            DELETE FROM fts_methods WHERE rowid = OLD.rowid;
        END;

        CREATE TABLE IF NOT EXISTS runtime_observations (
            id TEXT PRIMARY KEY,
            symbol_id TEXT NOT NULL,
            fully_qualified_name TEXT NOT NULL,
            assembly TEXT NOT NULL,
            timestamp BIGINT NOT NULL,
            depth INTEGER NOT NULL DEFAULT 0,
            parameters TEXT,
            return_value TEXT,
            duration_ms REAL NOT NULL DEFAULT 0
        );

        CREATE INDEX IF NOT EXISTS idx_runtime_symbol ON runtime_observations(symbol_id);
        CREATE INDEX IF NOT EXISTS idx_runtime_timestamp ON runtime_observations(timestamp);
    ";
}
