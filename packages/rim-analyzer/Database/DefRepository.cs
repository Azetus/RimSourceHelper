using Microsoft.Data.Sqlite;
using Dapper;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database;

// Defs 表的数据访问
public class DefRepository(SqliteConnection connection)
{
    // 批量插入 Def 记录，返回插入数量
    public int BulkInsert(IEnumerable<DefEntity> defs)
    {
        const string sql = """
            INSERT INTO Defs (DefName, DefType, Label, ParentDef, SourceFile, MergedJson)
            VALUES (@DefName, @DefType, @Label, @ParentDef, @SourceFile, @MergedJson)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, defs, transaction);
        transaction.Commit();
        return count;
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
}
