import type {
  TargetSearchResult, TypeInfoResult, MethodInfoResult, MethodReference,
  TypeMembersResult, MemberMethod, MemberField, MemberProperty,
  CallTreeNode, DefSummary, DefDetails, DefTypeCount,
  HarmonyPatchResult, SourceResult
} from "../types.js";

// --- find_target ---
export function formatFindTarget(results: TargetSearchResult[]): string {
  if (results.length === 0) return "No results found.";

  const types = results.filter(r => r.kind === "type");
  const methods = results.filter(r => r.kind === "method");
  const lines: string[] = [];

  if (types.length > 0) {
    lines.push(`## Types (${types.length})`);
    for (const t of types) lines.push(`- \`${t.FullName}\` — ${t.source}`);
  }
  if (methods.length > 0) {
    if (types.length > 0) lines.push("");
    lines.push(`## Methods (${methods.length})`);
    for (const m of methods) lines.push(`- \`${m.FullName}\` — ${m.source}`);
  }

  return lines.join("\n");
}

// --- get_target_info (type) ---
export function formatTypeInfo(info: TypeInfoResult): string {
  const lines: string[] = [];
  lines.push(`# ${info.fullName}`);

  const mods: string[] = [];
  if (info.accessibility) mods.push(info.accessibility);
  if (info.isAbstract && !info.isInterface) mods.push("abstract");
  if (info.isSealed) mods.push("sealed");
  if (info.isInterface) mods.push("interface");
  if (info.isEnum) mods.push("enum");
  if (mods.length > 0) lines.push(`- Modifiers: ${mods.join(" ")}`);

  if (info.baseType && info.baseType !== "System.Object") lines.push(`- Base: \`${info.baseType}\``);

  const interfaces = info.parents.filter(p => p.IsInterface);
  if (interfaces.length > 0) lines.push(`- Implements: ${interfaces.map(i => `\`${i.FullName}\``).join(", ")}`);

  lines.push(`- Source: ${info.source.Name} (${info.source.Type})`);
  lines.push(`- Members: ${info.memberCounts.methods} methods, ${info.memberCounts.fields} fields, ${info.memberCounts.properties} properties`);

  if (info.children.length > 0) {
    lines.push(`- Children: ${info.children.map(c => `\`${c.FullName}\``).join(", ")}`);
  }

  if (info.harmonyPatches.length > 0) {
    lines.push("");
    lines.push(`## Harmony Patches (${info.harmonyPatches.length})`);
    for (const p of info.harmonyPatches) {
      lines.push(`- **${p.PatchType}** on \`${p.TargetMethod ?? "(class)"}\` by \`${p.PatchClass}.${p.PatchMethod}\` — ${p.source}`);
    }
  }

  if (info.decompiled) {
    lines.push("");
    lines.push("## Source");
    lines.push("```csharp");
    lines.push(info.decompiled);
    lines.push("```");
  }

  return lines.join("\n");
}

// --- get_target_info (method) ---
export function formatMethodInfo(info: MethodInfoResult): string {
  const lines: string[] = [];
  lines.push(`# ${info.fullName}`);
  lines.push(`- Signature: \`${info.signature}\``);

  const mods: string[] = [];
  if (info.accessibility) mods.push(info.accessibility);
  if (info.isStatic) mods.push("static");
  if (info.isVirtual) mods.push("virtual");
  if (info.isAbstract) mods.push("abstract");
  if (mods.length > 0) lines.push(`- Modifiers: ${mods.join(" ")}`);

  lines.push(`- Returns: \`${info.returnType}\``);
  if (info.parentType) lines.push(`- Parent: \`${info.parentType.FullName}\``);
  lines.push(`- Source: ${info.source.Name} (${info.source.Type})`);

  if (info.overloads.length > 0) {
    lines.push(`- Overloads: ${info.overloads.map(o => `\`${o.Signature}\``).join(", ")}`);
  }

  if (info.callers.length > 0) {
    lines.push("");
    lines.push(`## Callers (${info.callers.length}${info.callersTruncated ? "+" : ""})`);
    for (const c of info.callers) lines.push(`- \`${c.FullName}\` — ${c.source}`);
  }

  if (info.callees.length > 0) {
    lines.push("");
    lines.push(`## Callees (${info.callees.length}${info.calleesTruncated ? "+" : ""})`);
    for (const c of info.callees) lines.push(`- \`${c.FullName}\` — ${c.source}`);
  }

  if (info.harmonyPatches.length > 0) {
    lines.push("");
    lines.push(`## Harmony Patches (${info.harmonyPatches.length})`);
    for (const p of info.harmonyPatches) {
      const prio = p.Priority ? ` [${p.Priority}]` : "";
      lines.push(`- **${p.PatchType}** \`${p.PatchClass}.${p.PatchMethod}\`${prio} — ${p.source}`);
    }
  }

  if (info.decompiled) {
    lines.push("");
    lines.push("## Source");
    lines.push("```csharp");
    lines.push(info.decompiled);
    lines.push("```");
  }

  return lines.join("\n");
}

// --- list_type_members ---
export function formatTypeMembers(result: TypeMembersResult): string {
  const lines: string[] = [];
  lines.push(`# ${result.typeName}`);

  if (result.methods && result.methods.length > 0) {
    lines.push("");
    lines.push(`## Methods (${result.methods.length})`);
    for (const m of result.methods) {
      const mods = [m.Accessibility, m.IsStatic ? "static" : "", m.IsVirtual ? "virtual" : "", m.IsAbstract ? "abstract" : ""].filter(Boolean).join(" ");
      lines.push(`- \`${m.Name}\` ${mods} → ${m.ReturnType}`);
    }
  }

  if (result.fields && result.fields.length > 0) {
    lines.push("");
    lines.push(`## Fields (${result.fields.length})`);
    for (const f of result.fields) {
      const mods = [f.Accessibility, f.IsStatic ? "static" : ""].filter(Boolean).join(" ");
      lines.push(`- \`${f.Name}\` ${mods} : ${f.FieldType}`);
    }
  }

  if (result.properties && result.properties.length > 0) {
    lines.push("");
    lines.push(`## Properties (${result.properties.length})`);
    for (const p of result.properties) {
      const accessors = [p.HasGetter ? "get" : "", p.HasSetter ? "set" : ""].filter(Boolean).join("/");
      lines.push(`- \`${p.Name}\` { ${accessors} } : ${p.PropertyType}`);
    }
  }

  return lines.join("\n");
}

// --- get_callers / get_callees ---
export function formatMethodList(methods: MethodReference[], title: string): string {
  if (methods.length === 0) return `${title}\nNone.`;
  const lines: string[] = [`${title}`];
  for (const m of methods) lines.push(`- \`${m.FullName}\` — ${m.source}`);
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
    lines.push(node.method);
  }
  const children = node.children;
  for (let i = 0; i < children.length; i++) {
    const isLast = i === children.length - 1;
    const connector = isLast ? "└── " : "├── ";
    const child = children[i];
    const label = child.isCycle ? `${child.method} ⟳ (cycle)` : child.method;
    lines.push(`${prefix}${connector}${label}`);
    if (!child.isCycle && child.children.length > 0) {
      const newPrefix = prefix + (isLast ? "    " : "│   ");
      renderTreeNode(child, lines, newPrefix, false);
    }
  }
  if (node.truncated) {
    lines.push(`${prefix}└── ... (${node.truncated} more)`);
  }
}

// --- search_defs / find_def_references ---
export function formatDefList(defs: DefSummary[], title: string): string {
  if (defs.length === 0) return `${title}\nNone.`;
  const lines: string[] = [title];
  for (const d of defs) {
    const label = d.Label ? ` "${d.Label}"` : "";
    lines.push(`- **${d.DefName}** (${d.DefType})${label} — ${d.source}`);
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
  lines.push(`- Source: ${def.source}`);
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
  for (const t of types) lines.push(`- ${t.DefType}: ${t.count}`);
  return lines.join("\n");
}

// --- find_harmony_patches / list_harmony_patches ---
export function formatPatchList(patches: HarmonyPatchResult[], title: string): string {
  if (patches.length === 0) return `${title}\nNone.`;
  const lines: string[] = [title];
  for (const p of patches) {
    const target = p.TargetMethod ? `${p.TargetType}.${p.TargetMethod}` : p.TargetType;
    const prio = p.Priority ? ` [${p.Priority}]` : "";
    lines.push(`- **${p.PatchType}** on \`${target}\` by \`${p.PatchClass}.${p.PatchMethod}\`${prio} — ${p.source}`);
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
