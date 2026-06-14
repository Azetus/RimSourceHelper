using Dapper;
using Microsoft.Data.Sqlite;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database.Tables;

// Defs 表的数据访问
public class DefRepository(SqliteConnection connection)
{
    // 批量插入 Def 记录，返回插入数量
    public int BulkInsert(IEnumerable<DefEntity> defs)
    {
        const string sql = """
            INSERT INTO Defs (DefName, DefType, ParentDef, Label, Description, IsAbstract, RawXml, SourceFile, SourceId)
            VALUES (@DefName, @DefType, @ParentDef, @Label, @Description, @IsAbstract, @RawXml, @SourceFile, @SourceId)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, defs, transaction);
        transaction.Commit();
        return count;
    }

    // 获取所有已知 defName 集合（用于引用检测）
    public HashSet<string> GetAllDefNames()
    {
        const string sql = "SELECT DefName FROM Defs WHERE DefName IS NOT NULL";
        return connection.Query<string>(sql).ToHashSet();
    }

    // 按名称搜索 Def
    public IEnumerable<DefEntity> SearchByName(string query, string? defType = null)
    {
        if (defType is not null)
        {
            const string sql = "SELECT * FROM Defs WHERE DefName LIKE @pattern AND DefType = @defType";
            return connection.Query<DefEntity>(sql, new { pattern = $"%{query}%", defType });
        }

        const string sqlAll = "SELECT * FROM Defs WHERE DefName LIKE @pattern";
        return connection.Query<DefEntity>(sqlAll, new { pattern = $"%{query}%" });
    }

    // 按 DefName 精确查找
    public DefEntity? FindByName(string defName, string? defType = null)
    {
        if (defType is not null)
        {
            const string sql = "SELECT * FROM Defs WHERE DefName = @defName AND DefType = @defType";
            return connection.QueryFirstOrDefault<DefEntity>(sql, new { defName, defType });
        }

        const string sqlAll = "SELECT * FROM Defs WHERE DefName = @defName";
        return connection.QueryFirstOrDefault<DefEntity>(sqlAll, new { defName });
    }

    // 列出所有 Def 类型及数量
    public IEnumerable<(string DefType, int Count)> ListDefTypes()
    {
        const string sql = "SELECT DefType, COUNT(*) AS Count FROM Defs GROUP BY DefType ORDER BY Count DESC";
        return connection.Query<(string, int)>(sql);
    }

    // 分页获取指定类型的 Def
    public IEnumerable<DefEntity> GetByType(string defType, int limit = 50, int offset = 0)
    {
        const string sql = "SELECT * FROM Defs WHERE DefType = @defType LIMIT @limit OFFSET @offset";
        return connection.Query<DefEntity>(sql, new { defType, limit, offset });
    }

    // 获取指定 SourceId 的 Def 的 (DefName → Id) 映射（用于引用检测时关联 Id）
    public Dictionary<string, long> GetDefNameToIdMap(long? sourceId = null)
    {
        var sql = sourceId.HasValue
            ? "SELECT DefName, Id FROM Defs WHERE DefName IS NOT NULL AND SourceId = @sourceId"
            : "SELECT DefName, Id FROM Defs WHERE DefName IS NOT NULL";

        var map = new Dictionary<string, long>();
        foreach (var (name, id) in connection.Query<(string DefName, long Id)>(sql, new { sourceId }))
            map.TryAdd(name, id);
        return map;
    }
}
