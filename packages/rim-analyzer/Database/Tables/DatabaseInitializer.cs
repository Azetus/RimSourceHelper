using Dapper;
using Microsoft.Data.Sqlite;

namespace RimAnalyzer.Database.Tables;

// 数据库初始化：创建所有表和索引
public static class DatabaseInitializer
{
    public static void Initialize(SqliteConnection connection)
    {
        connection.Execute(CreateTypesSql);
        connection.Execute(CreateMethodsSql);
        connection.Execute(CreateFieldsSql);
        connection.Execute(CreatePropertiesSql);
        connection.Execute(CreateInheritanceSql);
        connection.Execute(CreateCallsSql);
        connection.Execute(CreateDefsSql);
        connection.Execute(CreateDefReferencesSql);
        connection.Execute(CreateMetadataSql);
    }

    private const string CreateTypesSql = """
        CREATE TABLE IF NOT EXISTS Types (
            Id            INTEGER PRIMARY KEY,
            Namespace     TEXT,
            Name          TEXT NOT NULL,
            FullName      TEXT NOT NULL UNIQUE,
            BaseType      TEXT,
            IsAbstract    INTEGER NOT NULL DEFAULT 0,
            IsInterface   INTEGER NOT NULL DEFAULT 0,
            IsEnum        INTEGER NOT NULL DEFAULT 0,
            IsSealed      INTEGER NOT NULL DEFAULT 0,
            Accessibility TEXT,
            AssemblyName  TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_types_name ON Types(Name);
        CREATE INDEX IF NOT EXISTS idx_types_namespace ON Types(Namespace);
        """;

    private const string CreateMethodsSql = """
        CREATE TABLE IF NOT EXISTS Methods (
            Id            INTEGER PRIMARY KEY,
            TypeId        INTEGER NOT NULL REFERENCES Types(Id),
            Name          TEXT NOT NULL,
            FullName      TEXT NOT NULL,
            Signature     TEXT NOT NULL,
            ReturnType    TEXT,
            IsStatic      INTEGER NOT NULL DEFAULT 0,
            IsVirtual     INTEGER NOT NULL DEFAULT 0,
            IsAbstract    INTEGER NOT NULL DEFAULT 0,
            Accessibility TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_methods_name ON Methods(Name);
        CREATE INDEX IF NOT EXISTS idx_methods_typeid ON Methods(TypeId);
        CREATE INDEX IF NOT EXISTS idx_methods_fullname ON Methods(FullName);
        """;

    private const string CreateFieldsSql = """
        CREATE TABLE IF NOT EXISTS Fields (
            Id            INTEGER PRIMARY KEY,
            TypeId        INTEGER NOT NULL REFERENCES Types(Id),
            Name          TEXT NOT NULL,
            FieldType     TEXT,
            IsStatic      INTEGER NOT NULL DEFAULT 0,
            Accessibility TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_fields_typeid ON Fields(TypeId);
        """;

    private const string CreatePropertiesSql = """
        CREATE TABLE IF NOT EXISTS Properties (
            Id            INTEGER PRIMARY KEY,
            TypeId        INTEGER NOT NULL REFERENCES Types(Id),
            Name          TEXT NOT NULL,
            PropertyType  TEXT,
            HasGetter     INTEGER NOT NULL DEFAULT 0,
            HasSetter     INTEGER NOT NULL DEFAULT 0,
            Accessibility TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_properties_typeid ON Properties(TypeId);
        """;

    private const string CreateInheritanceSql = """
        CREATE TABLE IF NOT EXISTS Inheritance (
            ParentTypeId  INTEGER NOT NULL REFERENCES Types(Id),
            ChildTypeId   INTEGER NOT NULL REFERENCES Types(Id),
            IsInterface   INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (ParentTypeId, ChildTypeId)
        );
        CREATE INDEX IF NOT EXISTS idx_inheritance_parent ON Inheritance(ParentTypeId);
        CREATE INDEX IF NOT EXISTS idx_inheritance_child ON Inheritance(ChildTypeId);
        """;

    private const string CreateCallsSql = """
        CREATE TABLE IF NOT EXISTS Calls (
            CallerMethodId INTEGER NOT NULL REFERENCES Methods(Id),
            CalleeMethodId INTEGER NOT NULL REFERENCES Methods(Id),
            PRIMARY KEY (CallerMethodId, CalleeMethodId)
        );
        CREATE INDEX IF NOT EXISTS idx_calls_caller ON Calls(CallerMethodId);
        CREATE INDEX IF NOT EXISTS idx_calls_callee ON Calls(CalleeMethodId);
        """;

    private const string CreateDefsSql = """
        CREATE TABLE IF NOT EXISTS Defs (
            Id          INTEGER PRIMARY KEY,
            DefName     TEXT NOT NULL,
            DefType     TEXT NOT NULL,
            Label       TEXT,
            ParentDef   TEXT,
            SourceFile  TEXT,
            MergedJson  TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_defs_name ON Defs(DefName);
        CREATE INDEX IF NOT EXISTS idx_defs_type ON Defs(DefType);
        """;

    private const string CreateDefReferencesSql = """
        CREATE TABLE IF NOT EXISTS DefReferences (
            SourceDefId   INTEGER NOT NULL REFERENCES Defs(Id),
            TargetDefName TEXT NOT NULL,
            FieldPath     TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_defrefs_source ON DefReferences(SourceDefId);
        CREATE INDEX IF NOT EXISTS idx_defrefs_target ON DefReferences(TargetDefName);
        """;

    private const string CreateMetadataSql = """
        CREATE TABLE IF NOT EXISTS Metadata (
            Key   TEXT PRIMARY KEY,
            Value TEXT
        );
        """;
}
