import type {
  TargetSearchResult, TypeInfoResult, MethodInfoResult, MethodReference,
  TypeMembersResult, MemberMethod, MemberField, MemberProperty,
  CallTreeNode, DefSummary, DefDetails, DefTypeCount,
  HarmonyPatchResult, FieldAccessEntry, SourceResult
} from "../types.js";

// --- find_target ---
export function formatFindTarget(results: TargetSearchResult[]): string {
  if (results.length === 0) return "No results found.";

  const types = results.filter(r => r.Kind === "type");
  const methods = results.filter(r => r.Kind === "method");
  const fields = results.filter(r => r.Kind === "field");
  const properties = results.filter(r => r.Kind === "property");
  const lines: string[] = [];

  if (types.length > 0) {
    lines.push(`## Types (${types.length})`);
    for (const t of types) lines.push(`- \`${t.FullName}\` — ${t.Source}`);
  }
  if (methods.length > 0) {
    if (lines.length > 0) lines.push("");
    lines.push(`## Methods (${methods.length})`);
    for (const m of methods) lines.push(`- \`${m.Signature ?? m.FullName}\` — ${m.Source}`);
  }
  if (fields.length > 0) {
    if (lines.length > 0) lines.push("");
    lines.push(`## Fields (${fields.length})`);
    for (const f of fields) {
      const mod = f.IsStatic ? "static " : "";
      lines.push(`- \`${f.TypeFullName}.${f.Name}\` ${mod}: ${f.FieldType} — ${f.Source}`);
    }
  }
  if (properties.length > 0) {
    if (lines.length > 0) lines.push("");
    lines.push(`## Properties (${properties.length})`);
    for (const p of properties) {
      const accessors = [p.HasGetter ? "get" : "", p.HasSetter ? "set" : ""].filter(Boolean).join("/");
      lines.push(`- \`${p.TypeFullName}.${p.Name}\` { ${accessors} } : ${p.PropertyType} — ${p.Source}`);
    }
  }

  return lines.join("\n");
}

// --- get_target_info (type) ---
export function formatTypeInfo(info: TypeInfoResult): string {
  const lines: string[] = [];
  lines.push(`# ${info.FullName}`);

  const mods: string[] = [];
  if (info.Accessibility) mods.push(info.Accessibility);
  if (info.IsAbstract && !info.IsInterface) mods.push("abstract");
  if (info.IsSealed) mods.push("sealed");
  if (info.IsInterface) mods.push("interface");
  if (info.IsEnum) mods.push("enum");
  if (mods.length > 0) lines.push(`- Modifiers: ${mods.join(" ")}`);

  if (info.BaseType && info.BaseType !== "System.Object") lines.push(`- Base: \`${info.BaseType}\``);

  const interfaces = info.Parents.filter(p => p.IsInterface);
  if (interfaces.length > 0) lines.push(`- Implements: ${interfaces.map(i => `\`${i.FullName}\``).join(", ")}`);

  lines.push(`- Source: ${info.Source.Name} (${info.Source.Type})`);
  lines.push(`- Members: ${info.MemberCounts.Methods} methods, ${info.MemberCounts.Fields} fields, ${info.MemberCounts.Properties} properties`);

  if (info.Children.length > 0) {
    lines.push(`- Children: ${info.Children.map(c => `\`${c.FullName}\``).join(", ")}`);
  }

  if (info.HarmonyPatches.length > 0) {
    lines.push("");
    lines.push(`## Harmony Patches (${info.HarmonyPatches.length})`);
    for (const p of info.HarmonyPatches) {
      const tParams = p.TargetParams ? `(${p.TargetParams})` : "";
      lines.push(`- **${p.PatchType}** on \`${p.TargetMethod ?? "(class)"}${tParams}\` by \`${p.PatchClass}.${p.PatchMethod}\` — ${p.Source}`);
    }
  }

  if (info.Decompiled) {
    lines.push("");
    lines.push("## Source");
    lines.push("```csharp");
    lines.push(info.Decompiled);
    lines.push("```");
  }

  return lines.join("\n");
}

// --- get_target_info (method) ---
export function formatMethodInfo(info: MethodInfoResult): string {
  const lines: string[] = [];
  lines.push(`# ${info.FullName}`);
  lines.push(`- Signature: \`${info.Signature}\``);

  const mods: string[] = [];
  if (info.Accessibility) mods.push(info.Accessibility);
  if (info.IsStatic) mods.push("static");
  if (info.IsVirtual) mods.push("virtual");
  if (info.IsAbstract) mods.push("abstract");
  if (mods.length > 0) lines.push(`- Modifiers: ${mods.join(" ")}`);

  lines.push(`- Returns: \`${info.ReturnType}\``);
  if (info.ParentType) lines.push(`- Parent: \`${info.ParentType.FullName}\``);
  lines.push(`- Source: ${info.Source.Name} (${info.Source.Type})`);

  if (info.Overloads.length > 0) {
    lines.push(`- Overloads: ${info.Overloads.map(o => `\`${o.Signature}\``).join(", ")}`);
  }

  if (info.Callers.length > 0) {
    lines.push("");
    lines.push(`## Callers (${info.Callers.length}${info.CallersTruncated ? "+" : ""})`);
    for (const c of info.Callers) lines.push(`- \`${c.Signature}\` — ${c.Source}`);
  }

  if (info.Callees.length > 0) {
    lines.push("");
    lines.push(`## Callees (${info.Callees.length}${info.CalleesTruncated ? "+" : ""})`);
    for (const c of info.Callees) lines.push(`- \`${c.Signature}\` — ${c.Source}`);
  }

  if (info.HarmonyPatches.length > 0) {
    lines.push("");
    lines.push(`## Harmony Patches (${info.HarmonyPatches.length})`);
    for (const p of info.HarmonyPatches) {
      const prio = p.Priority ? ` [${p.Priority}]` : "";
      const mParams = p.TargetParams ? ` (${p.TargetParams})` : "";
      lines.push(`- **${p.PatchType}**${mParams} \`${p.PatchClass}.${p.PatchMethod}\`${prio} — ${p.Source}`);
    }
  }

  if (info.Decompiled) {
    lines.push("");
    lines.push("## Source");
    lines.push("```csharp");
    lines.push(info.Decompiled);
    lines.push("```");
  }

  return lines.join("\n");
}

// --- list_type_members ---
export function formatTypeMembers(result: TypeMembersResult): string {
  const lines: string[] = [];
  lines.push(`# ${result.TypeName}`);

  if (result.Methods && result.Methods.length > 0) {
    lines.push("");
    lines.push(`## Methods (${result.Methods.length})`);
    for (const m of result.Methods) {
      const mods = [m.Accessibility, m.IsStatic ? "static" : "", m.IsVirtual ? "virtual" : "", m.IsAbstract ? "abstract" : ""].filter(Boolean).join(" ");
      lines.push(`- \`${m.Name}\` ${mods} → ${m.ReturnType}`);
    }
  }

  if (result.Fields && result.Fields.length > 0) {
    lines.push("");
    lines.push(`## Fields (${result.Fields.length})`);
    for (const f of result.Fields) {
      const mods = [f.Accessibility, f.IsStatic ? "static" : ""].filter(Boolean).join(" ");
      lines.push(`- \`${f.Name}\` ${mods} : ${f.FieldType}`);
    }
  }

  if (result.Properties && result.Properties.length > 0) {
    lines.push("");
    lines.push(`## Properties (${result.Properties.length})`);
    for (const p of result.Properties) {
      const accessors = [p.HasGetter ? "get" : "", p.HasSetter ? "set" : ""].filter(Boolean).join("/");
      lines.push(`- \`${p.Name}\` { ${accessors} } : ${p.PropertyType}`);
    }
  }

  return lines.join("\n");
}

// --- get_callers / get_callees ---
export function formatMethodList(methods: MethodReference[], title: string): string {
  if (methods.length === 0) return `${title}\nNone.`;
  const lines: string[] = [title];
  for (const m of methods) lines.push(`- \`${m.Signature}\` — ${m.Source}`);
  return lines.join("\n");
}

// --- field accesses ---
export function formatFieldAccesses(accesses: FieldAccessEntry[], title: string): string {
  if (accesses.length === 0) return `${title}\nNone.`;
  const lines: string[] = [title];
  for (const a of accesses) {
    lines.push(`- \`${a.MethodFullName}\` → \`${a.TypeFullName}.${a.Name}\` [${a.AccessType}] : ${a.FieldType} — ${a.Source}`);
  }
  return lines.join("\n");
}

// --- get_call_tree ---
export function formatCallTree(tree: CallTreeNode, title: string): string {
  const lines: string[] = [title];
  renderTreeNode(tree, lines, "", true);
  return lines.join("\n");
}

function renderTreeNode(node: CallTreeNode, lines: string[], prefix: string, isRoot: boolean): void {
  if (isRoot) {
    lines.push(node.Method);
  }
  const children = node.Children;
  for (let i = 0; i < children.length; i++) {
    const isLast = i === children.length - 1;
    const connector = isLast ? "└── " : "├── ";
    const child = children[i];
    const label = child.IsCycle ? `${child.Method} ⟳ (cycle)` : child.Method;
    lines.push(`${prefix}${connector}${label}`);
    if (!child.IsCycle && child.Children.length > 0) {
      const newPrefix = prefix + (isLast ? "    " : "│   ");
      renderTreeNode(child, lines, newPrefix, false);
    }
  }
  if (node.Truncated) {
    lines.push(`${prefix}└── ... (${node.Truncated} more)`);
  }
}

// --- search_defs / find_def_references ---
export function formatDefList(defs: DefSummary[], title: string): string {
  if (defs.length === 0) return `${title}\nNone.`;
  const lines: string[] = [title];
  for (const d of defs) {
    const label = d.Label ? ` "${d.Label}"` : "";
    lines.push(`- **${d.DefName}** (${d.DefType})${label} — ${d.Source}`);
  }
  return lines.join("\n");
}

// --- get_def_details ---
export function formatDefDetails(def: DefDetails): string {
  const lines: string[] = [];
  lines.push(`# ${def.DefName} (${def.DefType})`);
  if (def.Label) lines.push(`- Label: ${def.Label}`);
  if (def.Description) lines.push(`- Description: ${def.Description}`);
  if (def.ParentDef) lines.push(`- Parent: ${def.ParentDef}`);
  lines.push(`- Source: ${def.Source}`);
  lines.push(`- File: ${def.SourceFile}`);
  lines.push("");
  lines.push("## XML");
  lines.push("```xml");
  lines.push(def.RawXml);
  lines.push("```");
  return lines.join("\n");
}

// --- list_def_types ---
export function formatDefTypes(types: DefTypeCount[]): string {
  const lines: string[] = [`## Def Types (${types.length})`];
  for (const t of types) lines.push(`- ${t.DefType}: ${t.Count}`);
  return lines.join("\n");
}

// --- find_harmony_patches / list_harmony_patches ---
export function formatPatchList(patches: HarmonyPatchResult[], title: string): string {
  if (patches.length === 0) return `${title}\nNone.`;
  const lines: string[] = [title];
  for (const p of patches) {
    const params = p.TargetParams ? `(${p.TargetParams})` : "";
    const target = p.TargetMethod ? `${p.TargetType}.${p.TargetMethod}${params}` : p.TargetType;
    const prio = p.Priority ? ` [${p.Priority}]` : "";
    lines.push(`- **${p.PatchType}** on \`${target}\` by \`${p.PatchClass}.${p.PatchMethod}\`${prio} — ${p.Source}`);
  }
  return lines.join("\n");
}

// --- list_sources ---
export function formatSourceList(sources: SourceResult[]): string {
  const lines: string[] = [`## Sources (${sources.length})`];
  for (const s of sources) {
    const pkg = s.PackageId ? ` — ${s.PackageId}` : "";
    lines.push(`- **${s.Name}** (${s.Type})${pkg}`);
  }
  return lines.join("\n");
}
