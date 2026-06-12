using Microsoft.Data.Sqlite;
using Dapper;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database;

// Inheritance 表的数据访问
public class InheritanceRepository(SqliteConnection connection)
{
    // 批量插入继承关系，返回插入数量
    public int BulkInsert(IEnumerable<(long ParentTypeId, long ChildTypeId, bool IsInterface)> relations)
    {
        const string sql = """
            INSERT OR IGNORE INTO Inheritance (ParentTypeId, ChildTypeId, IsInterface)
            VALUES (@ParentTypeId, @ChildTypeId, @IsInterface)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, relations.Select(r => new
        {
            r.ParentTypeId,
            r.ChildTypeId,
            r.IsInterface
        }), transaction);
        transaction.Commit();
        return count;
    }

    // 查询父类链（向上）
    public IEnumerable<TypeEntity> GetParents(long typeId)
    {
        const string sql = """
            SELECT t.* FROM Types t
            JOIN Inheritance i ON t.Id = i.ParentTypeId
            WHERE i.ChildTypeId = @typeId
            """;
        return connection.Query<TypeEntity>(sql, new { typeId });
    }

    // 查询子类（向下）
    public IEnumerable<TypeEntity> GetChildren(long typeId)
    {
        const string sql = """
            SELECT t.* FROM Types t
            JOIN Inheritance i ON t.Id = i.ChildTypeId
            WHERE i.ParentTypeId = @typeId
            """;
        return connection.Query<TypeEntity>(sql, new { typeId });
    }
}
