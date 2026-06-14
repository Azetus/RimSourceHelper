using Mono.Cecil;
using RimAnalyzer.Models;

namespace RimAnalyzer.Analysis.Harmony;

// 扫描 DLL 中的 [HarmonyPatch] 属性声明，提取 Patch 信息
public static class HarmonyAnalyzer
{
    // 已知的 Harmony 属性名前缀（支持 HarmonyLib 和旧版 Harmony 命名空间）
    private static readonly string[] HarmonyNamespaces = ["HarmonyLib.", "Harmony."];

    public static List<HarmonyPatchEntity> Analyze(IReadOnlyList<AssemblyDefinition> assemblies, Action<string> log)
    {
        var patches = new List<HarmonyPatchEntity>();

        foreach (var asm in assemblies)
        {
            foreach (var type in asm.MainModule.Types)
                AnalyzeTypeRecursive(type, patches);
        }

        log($"[INFO] Found {patches.Count} Harmony patches.");
        return patches;
    }

    private static void AnalyzeTypeRecursive(TypeDefinition type, List<HarmonyPatchEntity> patches)
    {
        try
        {
            // 从类级 [HarmonyPatch] 属性提取目标信息
            var (targetType, targetMethod) = ExtractClassLevelTarget(type);

            if (targetType is not null)
            {
                // 扫描类中的方法，确定 PatchType
                foreach (var method in type.Methods)
                {
                    var patchType = DetectPatchType(method);
                    if (patchType is null) continue;

                    var priority = ExtractPriority(method) ?? ExtractPriority(type);

                    patches.Add(new HarmonyPatchEntity
                    {
                        TargetType = targetType,
                        TargetMethod = targetMethod,
                        PatchType = patchType,
                        PatchClass = type.FullName,
                        PatchMethod = method.Name,
                        Priority = priority
                    });
                }
            }

            // 检查方法级 [HarmonyPatch] 属性
            foreach (var method in type.Methods)
            {
                var (mTargetType, mTargetMethod) = ExtractMethodLevelTarget(method);
                if (mTargetType is null) continue;

                var patchType = DetectPatchType(method) ?? InferPatchTypeFromName(method.Name);
                if (patchType is null) continue;

                var priority = ExtractPriority(method);

                patches.Add(new HarmonyPatchEntity
                {
                    TargetType = mTargetType,
                    TargetMethod = mTargetMethod,
                    PatchType = patchType,
                    PatchClass = type.FullName,
                    PatchMethod = method.Name,
                    Priority = priority
                });
            }
        }
        catch (AssemblyResolutionException)
        {
            // 无法解析 Harmony 程序集引用时跳过该类型（0Harmony.dll 不在引用路径中）
        }

        // 递归处理嵌套类型
        foreach (var nested in type.NestedTypes)
            AnalyzeTypeRecursive(nested, patches);
    }

    // 从类级 [HarmonyPatch] 属性提取 TargetType 和 TargetMethod
    private static (string? TargetType, string? TargetMethod) ExtractClassLevelTarget(TypeDefinition type)
    {
        string? targetType = null;
        string? targetMethod = null;

        foreach (var attr in type.CustomAttributes)
        {
            if (!IsHarmonyAttribute(attr, "HarmonyPatch")) continue;

            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Value is TypeReference typeRef)
                    targetType = typeRef.FullName;
                else if (arg.Value is string str && targetType is not null)
                    targetMethod ??= str;
            }
        }

        return (targetType, targetMethod);
    }

    // 从方法级 [HarmonyPatch] 属性提取目标信息
    private static (string? TargetType, string? TargetMethod) ExtractMethodLevelTarget(MethodDefinition method)
    {
        string? targetType = null;
        string? targetMethod = null;

        foreach (var attr in method.CustomAttributes)
        {
            if (!IsHarmonyAttribute(attr, "HarmonyPatch")) continue;

            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Value is TypeReference typeRef)
                    targetType = typeRef.FullName;
                else if (arg.Value is string str && targetType is not null)
                    targetMethod ??= str;
            }
        }

        return (targetType, targetMethod);
    }

    // 检测方法的 Patch 类型（通过属性）
    private static string? DetectPatchType(MethodDefinition method)
    {
        foreach (var attr in method.CustomAttributes)
        {
            if (IsHarmonyAttribute(attr, "HarmonyPrefix")) return "Prefix";
            if (IsHarmonyAttribute(attr, "HarmonyPostfix")) return "Postfix";
            if (IsHarmonyAttribute(attr, "HarmonyTranspiler")) return "Transpiler";
            if (IsHarmonyAttribute(attr, "HarmonyFinalizer")) return "Finalizer";
        }

        // 按方法名推断
        return InferPatchTypeFromName(method.Name);
    }

    // 从方法名推断 PatchType
    private static string? InferPatchTypeFromName(string methodName)
    {
        return methodName switch
        {
            "Prefix" => "Prefix",
            "Postfix" => "Postfix",
            "Transpiler" => "Transpiler",
            "Finalizer" => "Finalizer",
            _ => null
        };
    }

    // 提取优先级信息（从方法或类的属性）
    private static string? ExtractPriority(ICustomAttributeProvider provider)
    {
        var parts = new List<string>();

        foreach (var attr in provider.CustomAttributes)
        {
            if (IsHarmonyAttribute(attr, "HarmonyPriority") && attr.ConstructorArguments.Count > 0)
            {
                parts.Add($"Priority={attr.ConstructorArguments[0].Value}");
            }
            else if (IsHarmonyAttribute(attr, "HarmonyBefore") && attr.ConstructorArguments.Count > 0)
            {
                var ids = ExtractStringArray(attr.ConstructorArguments[0]);
                if (ids.Length > 0)
                    parts.Add($"Before={string.Join(",", ids)}");
            }
            else if (IsHarmonyAttribute(attr, "HarmonyAfter") && attr.ConstructorArguments.Count > 0)
            {
                var ids = ExtractStringArray(attr.ConstructorArguments[0]);
                if (ids.Length > 0)
                    parts.Add($"After={string.Join(",", ids)}");
            }
        }

        return parts.Count > 0 ? string.Join(";", parts) : null;
    }

    // 从属性参数提取字符串数组
    private static string[] ExtractStringArray(CustomAttributeArgument arg)
    {
        if (arg.Value is CustomAttributeArgument[] array)
            return array.Select(a => a.Value?.ToString() ?? "").Where(s => s.Length > 0).ToArray();
        if (arg.Value is string s)
            return [s];
        return [];
    }

    // 判断属性是否为指定的 Harmony 属性
    private static bool IsHarmonyAttribute(CustomAttribute attr, string shortName)
    {
        var fullName = attr.AttributeType.FullName;
        foreach (var ns in HarmonyNamespaces)
        {
            if (fullName == ns + shortName)
                return true;
        }
        return false;
    }
}
