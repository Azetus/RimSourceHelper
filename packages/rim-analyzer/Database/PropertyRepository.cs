using Microsoft.Data.Sqlite;
using Dapper;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database;

// Properties 表的数据访问
public class PropertyRepository(SqliteConnection connection)
{
    // 批量插入属性记录，返回插入数量
    public int BulkInsert(IEnumerable<PropertyEntity> properties)
    {
        const string sql = """
            INSERT INTO Properties (TypeId, Name, PropertyType, HasGetter, HasSetter, Accessibility)
            VALUES (@TypeId, @Name, @PropertyType, @HasGetter, @HasSetter, @Accessibility)
            """;

        using var transaction = connection.BeginTransaction();
        var count = connection.Execute(sql, properties, transaction);
        transaction.Commit();
        return count;
    }

    // 获取某类型的所有属性
    public IEnumerable<PropertyEntity> GetByTypeId(long typeId)
    {
        const string sql = "SELECT * FROM Properties WHERE TypeId = @typeId";
        return connection.Query<PropertyEntity>(sql, new { typeId });
    }
}
