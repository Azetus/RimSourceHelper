// 数据库查询结果的严格类型定义

// --- find_target ---
export interface TargetSearchResult {
  kind: "type" | "method";
  FullName: string;
  Name: string;
  Namespace?: string | null;
  IsAbstract?: number;
  IsInterface?: number;
  Signature?: string;
  ReturnType?: string;
  source: string;
}

// --- get_target_info ---
export interface SourceInfo {
  Name: string;
  Type: string;
}

export interface InheritanceEntry {
  FullName: string;
  IsInterface: number;
}

export interface HarmonyPatchEntry {
  TargetMethod?: string;
  PatchType: string;
  PatchClass: string;
  PatchMethod: string;
  Priority: string | null;
  source: string;
}

export interface TypeInfoResult {
  kind: "type";
  fullName: string;
  namespace: string | null;
  baseType: string | null;
  isAbstract: boolean;
  isInterface: boolean;
  isEnum: boolean;
  isSealed: boolean;
  accessibility: string | null;
  source: SourceInfo;
  parents: InheritanceEntry[];
  children: { FullName: string }[];
  memberCounts: { methods: number; fields: number; properties: number };
  harmonyPatches: HarmonyPatchEntry[];
  decompiled?: string;
}

export interface MethodReference {
  FullName: string;
  Signature: string;
  ReturnType?: string;
  source: string;
}

export interface MethodInfoResult {
  kind: "method";
  fullName: string;
  signature: string;
  returnType: string;
  isStatic: boolean;
  isVirtual: boolean;
  isAbstract: boolean;
  accessibility: string | null;
  source: SourceInfo;
  parentType: { FullName: string; Namespace: string; BaseType: string } | null;
  overloads: { Signature: string; ReturnType: string }[];
  callers: MethodReference[];
  callersTruncated: boolean;
  callees: MethodReference[];
  calleesTruncated: boolean;
  harmonyPatches: HarmonyPatchEntry[];
  decompiled?: string;
}

// --- list_type_members ---
export interface MemberMethod {
  Name: string;
  Signature: string;
  ReturnType: string;
  IsStatic: number;
  IsVirtual: number;
  IsAbstract: number;
  Accessibility: string;
}

export interface MemberField {
  Name: string;
  FieldType: string;
  IsStatic: number;
  Accessibility: string;
}

export interface MemberProperty {
  Name: string;
  PropertyType: string;
  HasGetter: number;
  HasSetter: number;
  Accessibility: string;
}

export interface TypeMembersResult {
  typeName: string;
  methods?: MemberMethod[];
  fields?: MemberField[];
  properties?: MemberProperty[];
}

// --- call tree ---
export interface CallTreeNode {
  method: string;
  signature: string;
  isCycle?: boolean;
  truncated?: number;
  children: CallTreeNode[];
}

// --- defs ---
export interface DefSummary {
  DefName: string;
  DefType: string;
  Label: string | null;
  IsAbstract?: number;
  ParentDef?: string | null;
  source: string;
}

export interface DefDetails {
  DefName: string;
  DefType: string;
  ParentDef: string | null;
  Label: string | null;
  Description: string | null;
  IsAbstract: number;
  RawXml: string;
  SourceFile: string;
  source: string;
}

export interface DefTypeCount {
  DefType: string;
  count: number;
}

// --- harmony ---
export interface HarmonyPatchResult {
  TargetType: string;
  TargetMethod: string | null;
  PatchType: string;
  PatchClass: string;
  PatchMethod: string;
  Priority: string | null;
  source: string;
}

// --- sources ---
export interface SourceResult {
  Name: string;
  Type: string;
  PackageId: string | null;
}
