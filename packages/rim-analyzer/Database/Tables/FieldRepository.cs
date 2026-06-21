using Dapper;
using Microsoft.Data.Sqlite;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database.Tables;

// Fields 表的数据访问
public class FieldRepository(SqliteConnection connection)
{
    // 批量插入字段记录，返回插入数量
    public int BulkInsert(IEnumerable<FieldEntity> fields)
    {
        const string sql = """
            INSERT INTO Fields (TypeId, Name, FieldType, IsStatic, Accessibility, SourceId)
            VALUES (@TypeId, @Name, @FieldType, @IsStatic, @Accessibility, @SourceId)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, fields, transaction);
        transaction.Commit();
        return count;
    }

    // 获取某类型的所有字段
    public IEnumerable<FieldEntity> GetByTypeId(long typeId)
    {
        const string sql = "SELECT * FROM Fields WHERE TypeId = @typeId";
        return connection.Query<FieldEntity>(sql, new { typeId });
    }

    // 获取 FieldSignature → Id 映射（TypeFullName + "." + Name，用于构建 FieldDefinition→Id 字典）
    public Dictionary<string, long> GetSignatureToIdMap()
    {
        const string sql = "SELECT f.Id, (t.FullName || '.' || f.Name) as Signature FROM Fields f JOIN Types t ON f.TypeId = t.Id";
        var map = new Dictionary<string, long>();
        foreach (var row in connection.Query<(long Id, string Signature)>(sql))
            map.TryAdd(row.Signature, row.Id);
        return map;
    }
}
