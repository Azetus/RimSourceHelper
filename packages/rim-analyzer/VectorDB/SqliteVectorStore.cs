using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;

namespace RimAnalyzer.VectorDB;

// sqlite-vec 向量存储实现。通过 SQLite 虚拟表存储向量索引。
public class SqliteVectorStore : IVectorStore
{
    private readonly SqliteConnection _connection;

    public SqliteVectorStore(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        _connection.LoadVector();
        _connection.Execute("PRAGMA journal_mode = WAL;");
        _connection.Execute("PRAGMA synchronous = NORMAL;");
    }

    // 建表（维度动态拼接）
    public void Initialize(int dimension)
    {
        _connection.Execute("""
            CREATE TABLE IF NOT EXISTS vector_config (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """);

        _connection.Execute("""
            CREATE TABLE IF NOT EXISTS vector_metadata (
                rowid     INTEGER PRIMARY KEY AUTOINCREMENT,
                sqlite_id INTEGER NOT NULL,
                source_id INTEGER NOT NULL,
                kind      TEXT    NOT NULL,
                full_name TEXT    NOT NULL
            );
            """);

        _connection.Execute($"""
            CREATE VIRTUAL TABLE IF NOT EXISTS vectors USING vec0(
                embedding FLOAT[{dimension}]
            );
            """);

        _connection.Execute("CREATE INDEX IF NOT EXISTS idx_vmeta_sqlite_id ON vector_metadata(sqlite_id);");
        _connection.Execute("CREATE INDEX IF NOT EXISTS idx_vmeta_source ON vector_metadata(source_id);");
    }

    // 批量写入：metadata → last_insert_rowid → vectors
    public void AddBatch(List<VectorDocument> docs, float[][] vectors)
    {
        if (docs.Count != vectors.Length)
            throw new ArgumentException("docs and vectors must have the same length");

        using var tx = _connection.BeginTransaction();

        for (var i = 0; i < docs.Count; i++)
        {
            var doc = docs[i];
            var vectorJson = SerializeVector(vectors[i]);

            _connection.Execute(
                "INSERT INTO vector_metadata (sqlite_id, source_id, kind, full_name) VALUES (@id, @sid, @kind, @name)",
                new { id = doc.SqliteId, sid = doc.SourceId, kind = doc.Kind, name = doc.FullName },
                tx);

            _connection.Execute(
                "INSERT INTO vectors (rowid, embedding) VALUES (last_insert_rowid(), ?)",
                new { vector = vectorJson },
                tx);
        }

        tx.Commit();
    }

    // 向量检索 + 模型一致性防御校验
    public List<VectorSearchResult> Search(float[] queryVector, int topK, VectorConfig queryConfig)
    {
        var stored = GetConfig()
            ?? throw new InvalidOperationException("Vector index not built. Run 'index' first.");

        ValidateConfig(queryConfig, stored);

        var queryJson = SerializeVector(queryVector);

        return _connection.Query<VectorSearchResult>(
            """
            SELECT m.sqlite_id AS SqliteId, m.kind AS Kind, m.full_name AS FullName, v.distance AS Distance
            FROM vectors v
            JOIN vector_metadata m ON v.rowid = m.rowid
            WHERE v.embedding MATCH ?
            ORDER BY v.distance
            LIMIT ?
            """,
            new { query = queryJson, limit = topK }
        ).ToList();
    }

    // 读取索引构建时绑定的模型配置
    public VectorConfig? GetConfig()
    {
        var pairs = _connection.Query<(string key, string value)>(
            "SELECT key, value FROM vector_config"
        ).ToDictionary(x => x.key, x => x.value);

        if (!pairs.TryGetValue("provider", out var provider) ||
            !pairs.TryGetValue("model", out var model) ||
            !pairs.TryGetValue("dimension", out var dimStr) ||
            !int.TryParse(dimStr, out var dimension))
            return null;

        return new VectorConfig(provider, model, dimension);
    }

    // 写入索引配置（index 完成后调用）
    public void SetConfig(VectorConfig config)
    {
        _connection.Execute("DELETE FROM vector_config");
        _connection.Execute("INSERT INTO vector_config (key, value) VALUES (@key, @value)",
            new[]
            {
                new { key = "provider", value = config.Provider },
                new { key = "model", value = config.Model },
                new { key = "dimension", value = config.Dimension.ToString() }
            });
    }

    // 按 SourceId 删除条目 + 清理 orphan vectors（add-mod 幂等重建时用）
    public void DeleteBySourceId(long sourceId)
    {
        _connection.Execute("DELETE FROM vector_metadata WHERE source_id = @sid", new { sid = sourceId });
        _connection.Execute("DELETE FROM vectors WHERE rowid NOT IN (SELECT rowid FROM vector_metadata)");
    }

    // 清空索引数据
    public void Clear()
    {
        _connection.Execute("DROP TABLE IF EXISTS vectors;");
        _connection.Execute("DROP TABLE IF EXISTS vector_metadata;");
        _connection.Execute("DROP TABLE IF EXISTS vector_config;");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    // float[] → JSON 数组字符串 "[0.123, -0.456, ...]"
    private static string SerializeVector(float[] vector)
    {
        return JsonSerializer.Serialize(vector);
    }

    // 模型一致性校验
    private static void ValidateConfig(VectorConfig query, VectorConfig stored)
    {
        if (query.Dimension != stored.Dimension)
            throw new InvalidOperationException(
                $"Dimension mismatch: stored={stored.Dimension}, query={query.Dimension}");

        if (!string.Equals(query.Model, stored.Model, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Model mismatch: stored model={stored.Model}, query model={query.Model}");

        if (!string.Equals(query.Provider, stored.Provider, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Provider mismatch: stored provider={stored.Provider}, query provider={query.Provider}");
    }
}
