using Dapper;
using Microsoft.Data.Sqlite;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database.Tables;

// DefReferences 表的数据访问
public class DefReferenceRepository(SqliteConnection connection)
{
    // 批量插入 Def 引用关系，返回插入数量
    public int BulkInsert(IEnumerable<DefReferenceEntity> references)
    {
        const string sql = """
            INSERT INTO DefReferences (SourceDefId, TargetDefName)
            VALUES (@SourceDefId, @TargetDefName)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, references, transaction);
        transaction.Commit();
        return count;
    }

    // 查找引用指定 Def 的所有来源
    public IEnumerable<DefEntity> FindReferencesTo(string targetDefName)
    {
        const string sql = """
            SELECT d.* FROM Defs d
            JOIN DefReferences r ON d.Id = r.SourceDefId
            WHERE r.TargetDefName = @targetDefName
            """;
        return connection.Query<DefEntity>(sql, new { targetDefName });
    }

    // 查找指定 Def 引用的所有目标
    public IEnumerable<DefReferenceEntity> GetReferencesFrom(long sourceDefId)
    {
        const string sql = "SELECT * FROM DefReferences WHERE SourceDefId = @sourceDefId";
        return connection.Query<DefReferenceEntity>(sql, new { sourceDefId });
    }
}
