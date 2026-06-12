using Microsoft.Data.Sqlite;
using Dapper;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database;

// Types 表的数据访问
public class TypeRepository(SqliteConnection connection)
{
    // 批量插入类型记录，返回插入数量
    public int BulkInsert(IEnumerable<TypeEntity> types)
    {
        const string sql = """
            INSERT INTO Types (Namespace, Name, FullName, BaseType, IsAbstract, IsInterface, IsEnum, IsSealed, Accessibility, AssemblyName)
            VALUES (@Namespace, @Name, @FullName, @BaseType, @IsAbstract, @IsInterface, @IsEnum, @IsSealed, @Accessibility, @AssemblyName)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, types, transaction);
        transaction.Commit();
        return count;
    }

    // 按完全限定名精确查找
    public TypeEntity? FindByFullName(string fullName)
    {
        const string sql = "SELECT * FROM Types WHERE FullName = @fullName";
        return connection.QueryFirstOrDefault<TypeEntity>(sql, new { fullName });
    }

    // 按名称模糊搜索
    public IEnumerable<TypeEntity> SearchByName(string pattern)
    {
        const string sql = "SELECT * FROM Types WHERE Name LIKE @pattern OR FullName LIKE @pattern";
        return connection.Query<TypeEntity>(sql, new { pattern = $"%{pattern}%" });
    }

    // 获取指定类型 ID
    public long? GetIdByFullName(string fullName)
    {
        const string sql = "SELECT Id FROM Types WHERE FullName = @fullName";
        return connection.QueryFirstOrDefault<long?>(sql, new { fullName });
    }
}
