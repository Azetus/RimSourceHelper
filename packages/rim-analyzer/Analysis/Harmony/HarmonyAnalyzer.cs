using Mono.Cecil;
using RimAnalyzer.Models;

namespace RimAnalyzer.Analysis.Harmony;

// 扫描 DLL 中的 [HarmonyPatch] 属性声明，提取 Patch 信息
public static class HarmonyAnalyzer
{
    // 已知的 Harmony 属性名前缀（支持 HarmonyLib 和旧版 Harmony 命名空间）
    private static readonly string[] HarmonyNamespaces = ["HarmonyLib.", "Harmony."];

    private sealed record DeclaringType(string FullName, TypeReference? Reference = null);

    private sealed record PatchTarget(DeclaringType? Type = null, string? MemberName = null,
        string? MethodKind = null, TypeReference[]? ParameterTypes = null)
    {
        public PatchTarget WithOverrides(PatchTarget detail) => new(
            detail.Type ?? Type,
            detail.MemberName ?? MemberName,
            detail.MethodKind ?? MethodKind,
            detail.ParameterTypes ?? ParameterTypes);
    }

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
            var classTarget = ExtractPatchTarget(type);
            foreach (var method in type.Methods)
            {
                var patchType = DetectPatchType(method);
                if (patchType is null) continue;

                // 方法级声明覆盖对应字段，未指定的字段沿用类级声明。
                var target = classTarget.WithOverrides(ExtractPatchTarget(method));
                if (target.Type is null) continue;

                patches.Add(new HarmonyPatchEntity
                {
                    TargetType = target.Type.FullName,
                    TargetMethod = GetTargetMethodName(target),
                    TargetParams = target.ParameterTypes is null ? null
                        : string.Join(",", target.ParameterTypes.Select(FormatParameterType)),
                    PatchType = patchType,
                    PatchClass = type.FullName,
                    PatchMethod = method.Name,
                    Priority = ExtractPriority(method) ?? (classTarget.Type is not null ? ExtractPriority(type) : null)
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

    // 合并同一声明上拆开的 HarmonyPatch 特性。
    private static PatchTarget ExtractPatchTarget(ICustomAttributeProvider provider)
    {
        var target = new PatchTarget();
        foreach (var attr in provider.CustomAttributes)
        {
            if (IsHarmonyAttribute(attr, "HarmonyPatch"))
                target = target.WithOverrides(ExtractPatchArguments(attr));
        }

        return target;
    }

    private static PatchTarget ExtractPatchArguments(CustomAttribute attr)
    {
        DeclaringType? targetType = null;
        string? memberName = null;
        string? methodKind = null;
        TypeReference[]? parameterTypes = null;
        CustomAttributeArgument[]? argumentVariations = null;

        var arguments = attr.ConstructorArguments;
        // 双字符串重载的第一个参数是类型名，其余重载的字符串是成员名。
        var startsWithTypeName = arguments.Count >= 2 && arguments[0].Type.FullName == "System.String"
            && arguments[1].Type.FullName == "System.String";
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (argument.Value is TypeReference type)
                targetType = new DeclaringType(type.FullName, type);
            else if (argument.Value is string name)
            {
                if (index == 0 && startsWithTypeName)
                    targetType = new DeclaringType(name.Replace('+', '/'));
                else
                    memberName = name;
            }
            else if (IsHarmonyType(argument.Type, "MethodType"))
                methodKind = GetEnumName(argument);
            else if (argument.Type is ArrayType array && argument.Value is CustomAttributeArgument[] values)
            {
                if (array.ElementType.FullName == "System.Type" && values.All(value => value.Value is TypeReference))
                    parameterTypes = values.Select(value => (TypeReference)value.Value).ToArray();
                else if (IsHarmonyType(array.ElementType, "ArgumentType"))
                    argumentVariations = values;
            }
        }

        // 保留空 Type[]，它可以覆盖类级参数并表示无参重载。
        if (parameterTypes is not null && argumentVariations is { Length: > 0 }
            && argumentVariations.Length == parameterTypes.Length)
        {
            for (var index = 0; index < parameterTypes.Length; index++)
            {
                var type = parameterTypes[index];
                parameterTypes[index] = GetEnumName(argumentVariations[index]) switch
                {
                    "Ref" or "Out" => new ByReferenceType(type),
                    "Pointer" => new PointerType(type),
                    _ => type
                };
            }
        }

        return new PatchTarget(targetType, memberName, methodKind, parameterTypes);
    }

    private static string? GetEnumName(CustomAttributeArgument argument)
    {
        return argument.Type.Resolve()?.Fields
            .FirstOrDefault(field => field.HasConstant && Equals(field.Constant, argument.Value))?.Name;
    }

    private static string FormatParameterType(TypeReference type) => type is ByReferenceType byReference
        ? $"ref {byReference.ElementType.FullName}"
        : type.FullName;

    private static string? GetTargetMethodName(PatchTarget target) => target.MethodKind switch
    {
        "Constructor" => ".ctor",
        "StaticConstructor" => ".cctor",
        "Getter" => GetAccessorName(target, getter: true),
        "Setter" => GetAccessorName(target, getter: false),
        _ => target.MemberName
    };

    private static string? GetAccessorName(PatchTarget target, bool getter)
    {
        if (string.IsNullOrEmpty(target.MemberName)) return null;

        try
        {
            var type = target.Type?.Reference?.Resolve();
            if (type is not null)
            {
                var property = type.Properties.FirstOrDefault(property => property.Name == target.MemberName);
                return (getter ? property?.GetMethod : property?.SetMethod)?.Name;
            }
        }
        catch (AssemblyResolutionException)
        {
            // 可选依赖的 DLL 缺失时，使用常规访问器名称。
        }

        return (getter ? "get_" : "set_") + target.MemberName;
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
    private static bool IsHarmonyAttribute(CustomAttribute attr, string shortName) =>
        IsHarmonyType(attr.AttributeType, shortName);

    private static bool IsHarmonyType(TypeReference type, string shortName)
    {
        var fullName = type.FullName;
        foreach (var ns in HarmonyNamespaces)
        {
            if (fullName == ns + shortName)
                return true;
        }
        return false;
    }
}
