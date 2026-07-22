namespace RimAnalyzer.VectorDB;

// 向量存储抽象接口
public interface IVectorStore : IDisposable
{
    // 建表 + 加载 vec0 扩展
    void Initialize(int dimension);

    // 批量写入 metadata + vectors
    void AddBatch(List<VectorDocument> docs, float[][] vectors);

    // 向量检索，queryConfig 用于防御校验模型一致性
    List<VectorSearchResult> Search(float[] queryVector, int topK, VectorConfig queryConfig);

    // 索引配置读写
    VectorConfig? GetConfig();
    void SetConfig(VectorConfig config);

    // 清空索引
    void Clear();
}
