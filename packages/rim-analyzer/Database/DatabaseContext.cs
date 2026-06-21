using Dapper;
using Microsoft.Data.Sqlite;
using RimAnalyzer.Database.Tables;

namespace RimAnalyzer.Database;

// 数据库连接生命周期管理：打开、配置、建表、关闭
public class DatabaseContext : IDisposable
{
    public SqliteConnection Connection { get; }
    public SourceRepository Sources { get; }
    public TypeRepository Types { get; }
    public MethodRepository Methods { get; }
    public FieldRepository Fields { get; }
    public PropertyRepository Properties { get; }
    public InheritanceRepository Inheritance { get; }
    public CallRepository Calls { get; }
    public DefRepository Defs { get; }
    public DefReferenceRepository DefReferences { get; }
    public HarmonyPatchRepository HarmonyPatches { get; }
    public FieldAccessRepository FieldAccesses { get; }
    public MetadataRepository Metadata { get; }

    private DatabaseContext(SqliteConnection connection)
    {
        Connection = connection;
        Sources = new SourceRepository(connection);
        Types = new TypeRepository(connection);
        Methods = new MethodRepository(connection);
        Fields = new FieldRepository(connection);
        Properties = new PropertyRepository(connection);
        Inheritance = new InheritanceRepository(connection);
        Calls = new CallRepository(connection);
        Defs = new DefRepository(connection);
        DefReferences = new DefReferenceRepository(connection);
        HarmonyPatches = new HarmonyPatchRepository(connection);
        FieldAccesses = new FieldAccessRepository(connection);
        Metadata = new MetadataRepository(connection);
    }

    // 创建并初始化数据库（处理 --force 覆盖逻辑）
    public static DatabaseContext Open(string dbPath, bool force)
    {
        if (force && File.Exists(dbPath))
            File.Delete(dbPath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();

        // 配置 SQLite PRAGMA
        connection.Execute("PRAGMA journal_mode = WAL;");
        connection.Execute("PRAGMA synchronous = NORMAL;");
        connection.Execute("PRAGMA foreign_keys = ON;");
        connection.Execute("PRAGMA busy_timeout = 5000;");

        DatabaseInitializer.Initialize(connection);

        return new DatabaseContext(connection);
    }

    public void Dispose()
    {
        Connection.Dispose();
    }
}
