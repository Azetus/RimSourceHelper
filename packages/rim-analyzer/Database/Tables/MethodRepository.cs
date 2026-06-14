using Dapper;
using Microsoft.Data.Sqlite;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database.Tables;

// Methods 表的数据访问
public class MethodRepository(SqliteConnection connection)
{
    // 批量插入方法记录，返回插入数量
    public int BulkInsert(IEnumerable<MethodEntity> methods)
    {
        const string sql = """
            INSERT INTO Methods (TypeId, Name, FullName, Signature, ReturnType, IsStatic, IsVirtual, IsAbstract, Accessibility, SourceId)
            VALUES (@TypeId, @Name, @FullName, @Signature, @ReturnType, @IsStatic, @IsVirtual, @IsAbstract, @Accessibility, @SourceId)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, methods, transaction);
        transaction.Commit();
        return count;
    }

    // 按名称搜索方法
    public IEnumerable<MethodEntity> SearchByName(string name, string? typeName = null)
    {
        if (typeName is not null)
        {
            const string sql = """
                SELECT m.* FROM Methods m
                JOIN Types t ON m.TypeId = t.Id
                WHERE m.Name LIKE @pattern AND (t.Name = @typeName OR t.FullName = @typeName)
                """;
            return connection.Query<MethodEntity>(sql, new { pattern = $"%{name}%", typeName });
        }

        const string sqlAll = "SELECT * FROM Methods WHERE Name LIKE @pattern";
        return connection.Query<MethodEntity>(sqlAll, new { pattern = $"%{name}%" });
    }

    // 按完全限定名精确查找
    public MethodEntity? FindByFullName(string fullName)
    {
        const string sql = "SELECT * FROM Methods WHERE FullName = @fullName";
        return connection.QueryFirstOrDefault<MethodEntity>(sql, new { fullName });
    }

    // 获取某类型的所有方法
    public IEnumerable<MethodEntity> GetByTypeId(long typeId)
    {
        const string sql = "SELECT * FROM Methods WHERE TypeId = @typeId";
        return connection.Query<MethodEntity>(sql, new { typeId });
    }

    // 获取所有 Signature → Id 映射（用于构建 MethodDefinition→Id 字典）
    public Dictionary<string, long> GetSignatureToIdMap()
    {
        const string sql = "SELECT Signature, Id FROM Methods";
        var map = new Dictionary<string, long>();
        foreach (var (sig, id) in connection.Query<(string Signature, long Id)>(sql))
            map.TryAdd(sig, id);
        return map;
    }
}
