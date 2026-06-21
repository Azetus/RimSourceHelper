using Dapper;
using Microsoft.Data.Sqlite;

namespace RimAnalyzer.Database.Tables;

// FieldAccesses 表的数据访问
public class FieldAccessRepository(SqliteConnection connection)
{
    public int BulkInsert(IEnumerable<(long MethodId, long FieldId, string AccessType)> accesses)
    {
        const string sql = """
            INSERT OR IGNORE INTO FieldAccesses (MethodId, FieldId, AccessType)
            VALUES (@MethodId, @FieldId, @AccessType)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, accesses.Select(a => new
        {
            a.MethodId,
            a.FieldId,
            a.AccessType
        }), transaction);
        transaction.Commit();
        return count;
    }
}
