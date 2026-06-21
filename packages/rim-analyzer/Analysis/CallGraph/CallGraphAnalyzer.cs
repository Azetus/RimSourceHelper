using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RimAnalyzer.Analysis.CallGraph;

public record CallGraphResult(
    List<(long CallerId, long CalleeId)> Calls,
    List<(long MethodId, long FieldId, string AccessType)> FieldAccesses
);

// IL 调用图分析：扫描方法体指令，通过 Resolve() 做对象级匹配
public static partial class CallGraphAnalyzer
{
    public static CallGraphResult Analyze(
        IReadOnlyList<AssemblyDefinition> assemblies,
        Dictionary<MethodDefinition, long> methodDefToId,
        Dictionary<FieldDefinition, long> fieldDefToId,
        Action<string> log)
    {
        var calls = new HashSet<(long, long)>();
        var fieldAccesses = new HashSet<(long MethodId, long FieldId, string AccessType)>();
        var resolveFailures = 0;

        foreach (var asm in assemblies)
        {
            foreach (var type in asm.MainModule.Types)
                ScanTypeRecursive(type, methodDefToId, fieldDefToId, calls, fieldAccesses, ref resolveFailures);
        }

        log($"[INFO] Call graph: {calls.Count} call edges, {fieldAccesses.Count} field accesses, {resolveFailures} unresolved skipped.");
        return new CallGraphResult(calls.ToList(), fieldAccesses.ToList());
    }

    private static void ScanTypeRecursive(TypeDefinition type,
        Dictionary<MethodDefinition, long> methodDefToId,
        Dictionary<FieldDefinition, long> fieldDefToId,
        HashSet<(long, long)> calls,
        HashSet<(long, long, string)> fieldAccesses,
        ref int resolveFailures)
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
                // 方法调用分析
                if (IsCallOpCode(instruction.OpCode))
                {
                    if (instruction.Operand is MethodReference calleeRef)
                    {
                        var calleeDef = TryResolve(calleeRef, ref resolveFailures);
                        if (calleeDef is not null && methodDefToId.TryGetValue(calleeDef, out var calleeId))
                            calls.Add((callerId.Value, calleeId));
                    }
                }
                // 字段访问分析
                else if (IsFieldAccessOpCode(instruction.OpCode))
                {
                    if (instruction.Operand is FieldReference fieldRef)
                    {
                        var fieldDef = TryResolve(fieldRef, ref resolveFailures);
                        if (fieldDef is not null && fieldDefToId.TryGetValue(fieldDef, out var fieldId))
                        {
                            var accessType = (instruction.OpCode == OpCodes.Stfld || instruction.OpCode == OpCodes.Stsfld)
                                ? "write" : "read";
                            fieldAccesses.Add((callerId.Value, fieldId, accessType));
                        }
                    }
                }
            }
        }

        foreach (var nested in type.NestedTypes)
            ScanTypeRecursive(nested, methodDefToId, fieldDefToId, calls, fieldAccesses, ref resolveFailures);
    }

    private static MethodDefinition? TryResolve(MethodReference methodRef, ref int resolveFailures)
    {
        try { return methodRef.Resolve(); }
        catch { resolveFailures++; return null; }
    }

    private static FieldDefinition? TryResolve(FieldReference fieldRef, ref int resolveFailures)
    {
        try { return fieldRef.Resolve(); }
        catch { resolveFailures++; return null; }
    }

    // 解析 caller 的数据库 Id，含 Lambda/闭包归属逻辑
    private static long? ResolveCallerId(MethodDefinition method, Dictionary<MethodDefinition, long> methodDefToId)
    {
        if (methodDefToId.TryGetValue(method, out var directId))
            return directId;

        var declaringType = method.DeclaringType;
        if (declaringType is null || !IsCompilerGeneratedClosure(declaringType.Name))
            return null;

        var match = ClosureMethodPattern().Match(method.Name);
        if (!match.Success)
            return null;

        var parentMethodName = match.Groups[1].Value;
        var parentType = declaringType.DeclaringType;
        if (parentType is null)
            return null;

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

    private static bool IsFieldAccessOpCode(OpCode opCode)
    {
        return opCode == OpCodes.Ldfld
            || opCode == OpCodes.Stfld
            || opCode == OpCodes.Ldsfld
            || opCode == OpCodes.Stsfld
            || opCode == OpCodes.Ldflda
            || opCode == OpCodes.Ldsflda;
    }

    private static bool IsCompilerGeneratedClosure(string typeName)
    {
        return typeName.Contains("<>c__DisplayClass") || typeName == "<>c";
    }

    [GeneratedRegex(@"^<(\w+)>[bg]__")]
    private static partial Regex ClosureMethodPattern();
}
