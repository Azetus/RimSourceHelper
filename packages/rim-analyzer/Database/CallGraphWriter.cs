namespace RimAnalyzer.Database;

// 调用图写入编排：接收调用对，委托给 CallRepository
public static class CallGraphWriter
{
    public static int Write(DatabaseContext db, List<(long CallerId, long CalleeId)> callPairs, Action<string> log)
    {
        var count = db.Calls.BulkInsert(callPairs);
        log($"[INFO] Inserted {count} call relations.");
        return count;
    }
}
