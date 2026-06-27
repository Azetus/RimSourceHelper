using Dapper;
using Microsoft.Data.Sqlite;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database.Tables;

// XmlPatches 表的数据访问
public class XmlPatchRepository(SqliteConnection connection)
{
    public int BulkInsert(IEnumerable<XmlPatchEntity> patches)
    {
        const string sql = """
            INSERT INTO XmlPatches (SourceId, TargetXPaths, OperationClasses, RawXml, SourceFile)
            VALUES (@SourceId, @TargetXPaths, @OperationClasses, @RawXml, @SourceFile)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, patches, transaction);
        transaction.Commit();
        return count;
    }

    // 分页列出指定 Source 的 Patches
    public IEnumerable<XmlPatchEntity> ListBySource(long sourceId, int offset, int limit)
    {
        const string sql = "SELECT * FROM XmlPatches WHERE SourceId = @sourceId LIMIT @limit OFFSET @offset";
        return connection.Query<XmlPatchEntity>(sql, new { sourceId, limit, offset });
    }

    // 查询指定 Source 的 Patches 总数
    public int CountBySource(long sourceId)
    {
        const string sql = "SELECT COUNT(*) FROM XmlPatches WHERE SourceId = @sourceId";
        return connection.ExecuteScalar<int>(sql, new { sourceId });
    }

    // 按 defName 或 source 搜索 Patches
    public IEnumerable<XmlPatchEntity> Search(string? defName, string? sourceName, int limit)
    {
        var conditions = new List<string>();
        if (defName is not null)
            conditions.Add($"TargetXPaths LIKE '%defName=\"{defName}\"%'");
        if (sourceName is not null)
            conditions.Add("s.Name = @sourceName");

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $"""
            SELECT p.* FROM XmlPatches p
            JOIN Sources s ON p.SourceId = s.Id
            {where} LIMIT @limit
            """;

        return connection.Query<XmlPatchEntity>(sql, new { defName, sourceName, limit });
    }
}
