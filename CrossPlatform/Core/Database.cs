#nullable enable
using Microsoft.Data.Sqlite;

namespace AstroArchive;

/// <summary>The same archive schema and parameter semantics on every desktop RID.</summary>
public sealed class Database : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly object sync = new();
    private long generation;
    public long Generation { get { lock (sync) return generation; } }

    public Database(string path, bool wal = false)
    {
        connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false,
            DefaultTimeout = 5
        }.ToString());
        try
        {
            connection.Open();
            Exec("PRAGMA busy_timeout=5000");
            Exec(wal ? "PRAGMA journal_mode=WAL" : "PRAGMA journal_mode=DELETE");
            Exec("PRAGMA synchronous=FULL");
            Exec("CREATE TABLE IF NOT EXISTS files(hash TEXT PRIMARY KEY, data TEXT NOT NULL)");
            Exec("CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY,data TEXT NOT NULL)");
            ManifestSchema();
        }
        catch { connection.Dispose(); throw; }
    }

    public List<string> Query(string sql, params string[] args)
    {
        lock (sync)
        {
            try
            {
                using var command = connection.CreateCommand();
                // The legacy queries use anonymous positional parameters. Give them names,
                // respecting SQL quoted literals rather than rewriting question marks in data.
                var rewritten = new System.Text.StringBuilder();
                bool quoted = false; int parameter = 0;
                foreach (char c in sql)
                {
                    if (c == '\'') quoted = !quoted;
                    if (c == '?' && !quoted) rewritten.Append("$p" + parameter++);
                    else rewritten.Append(c);
                }
                if (parameter != args.Length) throw new ArgumentException("SQL parameter count does not match.");
                command.CommandText = rewritten.ToString();
                for (int i = 0; i < args.Length; i++) command.Parameters.AddWithValue("$p" + i, args[i] ?? "");
                using var reader = command.ExecuteReader();
                var rows = new List<string>();
                while (reader.Read()) rows.Add(reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
                return rows;
            }
            catch (SqliteException e)
            {
                var error = new IOException("Archive database: " + e.Message, e);
                error.Data["SQLiteCode"] = e.SqliteExtendedErrorCode;
                throw error;
            }
        }
    }

    public void Exec(string sql, params string[] args)
    {
        lock (sync)
        {
            Query(sql, args);
            if (sql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ||
                sql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                sql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)) generation++;
        }
    }

    public void ManifestSchema()
    {
        Exec("CREATE TABLE IF NOT EXISTS source_manifest(root TEXT,path TEXT,identity TEXT,size INTEGER,mtime INTEGER,hash TEXT,destination TEXT,status TEXT,data TEXT,PRIMARY KEY(root,path))");
        Exec("CREATE INDEX IF NOT EXISTS manifest_path ON source_manifest(path COLLATE NOCASE)");
        Exec("CREATE INDEX IF NOT EXISTS manifest_identity ON source_manifest(identity)");
        Exec("CREATE INDEX IF NOT EXISTS manifest_hash ON source_manifest(hash,status)");
        Exec("CREATE TABLE IF NOT EXISTS deleted_files(hash TEXT PRIMARY KEY,data TEXT NOT NULL)");
        Exec("CREATE TABLE IF NOT EXISTS import_names(hash TEXT,telescope TEXT,folder TEXT,name TEXT,data TEXT NOT NULL,PRIMARY KEY(hash,telescope,folder,name))");
        Exec("CREATE INDEX IF NOT EXISTS import_names_telescope ON import_names(telescope COLLATE NOCASE)");
    }

    public void Transaction(Action action)
    {
        lock (sync)
        {
            Exec("BEGIN IMMEDIATE");
            try { action(); Exec("COMMIT"); }
            catch { Exec("ROLLBACK"); throw; }
        }
    }

    public void BackupTo(string path, CancellationToken ct)
    {
        lock (sync)
        {
            ct.ThrowIfCancellationRequested();
            using var target = new Database(path);
            connection.BackupDatabase(target.connection);
            ct.ThrowIfCancellationRequested();
        }
    }

    public void Dispose() { lock (sync) connection.Dispose(); }
}
