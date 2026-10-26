using Microsoft.Data.Sqlite;

namespace DeepTrace.Indexing;

internal static class DbHelper
{
    public static SqliteCommand Cmd(this SqliteConnection conn, string sql)
        => new(sql, conn);

    public static SqliteCommand Cmd(this SqliteConnection conn, string sql, SqliteTransaction? tx)
    {
        var cmd = new SqliteCommand(sql, conn);
        if (tx != null) cmd.Transaction = tx;
        return cmd;
    }

    public static SqliteCommand P(this SqliteCommand cmd, string name, object? value)
    {
        cmd.Parameters.AddWithValue(name, value ?? (object?)DBNull.Value);
        return cmd;
    }

    public static SqliteCommand P(this SqliteCommand cmd, (string name, object value)[] pairs)
    {
        cmd.Parameters.Clear();
        foreach (var (name, value) in pairs)
            cmd.P(name, value);
        return cmd;
    }
}
