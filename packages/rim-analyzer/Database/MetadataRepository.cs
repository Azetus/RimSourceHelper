using Microsoft.Data.Sqlite;
using Dapper;

namespace RimAnalyzer.Database;

// Metadata 表的数据访问（存储构建时间、版本等元信息）
public class MetadataRepository(SqliteConnection connection)
{
    // 设置元数据键值对
    public void Set(string key, string value)
    {
        const string sql = """
            INSERT INTO Metadata (Key, Value) VALUES (@key, @value)
            ON CONFLICT(Key) DO UPDATE SET Value = @value
            """;
        connection.Execute(sql, new { key, value });
    }

    // 获取元数据值
    public string? Get(string key)
    {
        const string sql = "SELECT Value FROM Metadata WHERE Key = @key";
        return connection.QueryFirstOrDefault<string>(sql, new { key });
    }

    // 获取所有元数据
    public IDictionary<string, string> GetAll()
    {
        const string sql = "SELECT Key, Value FROM Metadata";
        return connection.Query<(string Key, string Value)>(sql)
            .ToDictionary(x => x.Key, x => x.Value);
    }
}
