using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RimAnalyzer.Analysis.CallGraph;

// IL 调用图分析：扫描方法体指令，通过 Resolve() 做对象级匹配
public static partial class CallGraphAnalyzer
{
    public static List<(long CallerId, long CalleeId)> Analyze(
        IReadOnlyList<AssemblyDefinition> assemblies,
        Dictionary<MethodDefinition, long> methodDefToId,
        Action<string> log)
    {
        var result = new HashSet<(long, long)>();
        var resolveFailures = 0;

        foreach (var asm in assemblies)
        {
            foreach (var type in asm.MainModule.Types)
                ScanTypeRecursive(type, methodDefToId, result, ref resolveFailures);
        }

        log($"[INFO] Call graph: {result.Count} unique edges, {resolveFailures} unresolved references skipped.");
        return result.ToList();
    }

    private static void ScanTypeRecursive(TypeDefinition type,
        Dictionary<MethodDefinition, long> methodDefToId,
        HashSet<(long, long)> result, ref int resolveFailures)
    {
        foreach (var method in type.Methods)
        {
            if (!method.HasBody)
                continue;

            // 解析 caller Id（含 Lambda 归属）
            var callerId = ResolveCallerId(method, methodDefToId);
            if (callerId is null)
                continue;

            foreach (var instruction in method.Body.Instructions)
            {
                if (!IsCallOpCode(instruction.OpCode))
                    continue;

                if (instruction.Operand is not MethodReference calleeRef)
                    continue;

                MethodDefinition? calleeDef;
                try
                {
                    calleeDef = calleeRef.Resolve();
                }
                catch
                {
                    // Resolve 可能因程序集加载问题抛异常
                    resolveFailures++;
                    continue;
                }

                if (calleeDef is null)
                {
                    resolveFailures++;
                    continue;
                }

                if (methodDefToId.TryGetValue(calleeDef, out var calleeId))
                    result.Add((callerId.Value, calleeId));
            }
        }

        // 递归处理嵌套类型
        foreach (var nested in type.NestedTypes)
            ScanTypeRecursive(nested, methodDefToId, result, ref resolveFailures);
    }

    // 解析 caller 的数据库 Id，含 Lambda/闭包归属逻辑
    private static long? ResolveCallerId(MethodDefinition method, Dictionary<MethodDefinition, long> methodDefToId)
    {
        // 正常方法：直接查找
        if (methodDefToId.TryGetValue(method, out var directId))
            return directId;

        // 闭包方法：尝试归属到父方法
        var declaringType = method.DeclaringType;
        if (declaringType is null || !IsCompilerGeneratedClosure(declaringType.Name))
            return null;

        // 从方法名 "<Kill>b__0" 中提取 "Kill"
        var match = ClosureMethodPattern().Match(method.Name);
        if (!match.Success)
            return null;

        var parentMethodName = match.Groups[1].Value;
        var parentType = declaringType.DeclaringType;
        if (parentType is null)
            return null;

        // 在外层类型中查找同名方法
        var parentMethod = parentType.Methods.FirstOrDefault(m => m.Name == parentMethodName);
        if (parentMethod is not null && methodDefToId.TryGetValue(parentMethod, out var parentId))
            return parentId;

        return null;
    }

    private static bool IsCallOpCode(OpCode opCode)
    {
        return opCode == OpCodes.Call
            || opCode == OpCodes.Callvirt
            || opCode == OpCodes.Newobj
            || opCode == OpCodes.Ldftn;
    }

    private static bool IsCompilerGeneratedClosure(string typeName)
    {
        return typeName.Contains("<>c__DisplayClass") || typeName == "<>c";
    }

    // 匹配闭包方法名模式：<ParentMethodName>b__N 或 <ParentMethodName>g__LocalFuncName|N
    [GeneratedRegex(@"^<(\w+)>[bg]__")]
    private static partial Regex ClosureMethodPattern();
}
