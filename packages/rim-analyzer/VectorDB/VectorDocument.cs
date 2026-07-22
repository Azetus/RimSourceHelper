namespace RimAnalyzer.VectorDB;

// 向量存储数据模型

public record VectorConfig(string Provider, string Model, int Dimension);

public record VectorDocument(long SqliteId, long SourceId, string Kind, string FullName);

public record VectorSearchResult(long SqliteId, string Kind, string FullName, float Distance);
