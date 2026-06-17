using Dapper;
using Microsoft.Data.Sqlite;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database.Tables;

// HarmonyPatches 表的数据访问
public class HarmonyPatchRepository(SqliteConnection connection)
{
    // 批量插入 Patch 记录，返回插入数量
    public int BulkInsert(IEnumerable<HarmonyPatchEntity> patches)
    {
        const string sql = """
            INSERT INTO HarmonyPatches (TargetType, TargetMethod, PatchType, PatchClass, PatchMethod, TargetParams, Priority, SourceId)
            VALUES (@TargetType, @TargetMethod, @PatchType, @PatchClass, @PatchMethod, @TargetParams, @Priority, @SourceId)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, patches, transaction);
        transaction.Commit();
        return count;
    }

    // 按目标类型查找所有 Patch
    public IEnumerable<HarmonyPatchEntity> FindByTargetType(string targetType)
    {
        const string sql = "SELECT * FROM HarmonyPatches WHERE TargetType = @targetType";
        return connection.Query<HarmonyPatchEntity>(sql, new { targetType });
    }

    // 按目标类型+方法查找 Patch
    public IEnumerable<HarmonyPatchEntity> FindByTarget(string targetType, string targetMethod)
    {
        const string sql = "SELECT * FROM HarmonyPatches WHERE TargetType = @targetType AND TargetMethod = @targetMethod";
        return connection.Query<HarmonyPatchEntity>(sql, new { targetType, targetMethod });
    }

    // 按 SourceId 查找所有 Patch
    public IEnumerable<HarmonyPatchEntity> FindBySource(long sourceId)
    {
        const string sql = "SELECT * FROM HarmonyPatches WHERE SourceId = @sourceId";
        return connection.Query<HarmonyPatchEntity>(sql, new { sourceId });
    }
}
