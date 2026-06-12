using Dapper;
using Microsoft.Data.Sqlite;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database.Tables;

// Calls 表的数据访问
public class CallRepository(SqliteConnection connection)
{
    // 批量插入调用关系，返回插入数量
    public int BulkInsert(IEnumerable<(long CallerMethodId, long CalleeMethodId)> calls)
    {
        const string sql = """
            INSERT OR IGNORE INTO Calls (CallerMethodId, CalleeMethodId)
            VALUES (@CallerMethodId, @CalleeMethodId)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, calls.Select(c => new
        {
            c.CallerMethodId,
            c.CalleeMethodId
        }), transaction);
        transaction.Commit();
        return count;
    }

    // 查找调用指定方法的所有方法（callers）
    public IEnumerable<MethodEntity> GetCallers(long methodId)
    {
        const string sql = """
            SELECT m.* FROM Methods m
            JOIN Calls c ON m.Id = c.CallerMethodId
            WHERE c.CalleeMethodId = @methodId
            """;
        return connection.Query<MethodEntity>(sql, new { methodId });
    }

    // 查找指定方法调用的所有方法（callees）
    public IEnumerable<MethodEntity> GetCallees(long methodId)
    {
        const string sql = """
            SELECT m.* FROM Methods m
            JOIN Calls c ON m.Id = c.CalleeMethodId
            WHERE c.CallerMethodId = @methodId
            """;
        return connection.Query<MethodEntity>(sql, new { methodId });
    }
}
