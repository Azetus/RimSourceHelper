using Dapper;
using Microsoft.Data.Sqlite;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database.Tables;

// Sources 表的数据访问
public class SourceRepository(SqliteConnection connection)
{
    // 插入一条 Source 记录并返回其 Id
    public long Insert(SourceEntity source)
    {
        const string sql = """
            INSERT INTO Sources (Name, Type, PackageId, AssemblyPath, RootPath)
            VALUES (@Name, @Type, @PackageId, @AssemblyPath, @RootPath);
            SELECT last_insert_rowid();
            """;
        return connection.ExecuteScalar<long>(sql, source);
    }

    // 按名称查找
    public SourceEntity? FindByName(string name)
    {
        const string sql = "SELECT * FROM Sources WHERE Name = @name";
        return connection.QueryFirstOrDefault<SourceEntity>(sql, new { name });
    }

    // 获取所有 Source
    public IEnumerable<SourceEntity> GetAll()
    {
        const string sql = "SELECT * FROM Sources ORDER BY Type, Name";
        return connection.Query<SourceEntity>(sql);
    }

    // 按名称删除并返回被删除 Source 的 Id（用于级联删除）
    public long? DeleteByName(string name)
    {
        var source = FindByName(name);
        if (source is null) return null;

        connection.Execute("DELETE FROM Sources WHERE Id = @Id", new { source.Id });
        return source.Id;
    }
}
