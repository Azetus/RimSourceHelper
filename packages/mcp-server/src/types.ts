// 数据库查询结果的严格类型定义，字段名统一 PascalCase（对齐 DB 列名）

// --- find_target ---
export interface TargetSearchResult {
  Kind: "type" | "method" | "field" | "property";
  FullName: string;
  Name: string;
  Namespace?: string | null;
  IsAbstract?: number;
  IsInterface?: number;
  Signature?: string;
  ReturnType?: string;
  TypeFullName?: string;
  FieldType?: string;
  PropertyType?: string;
  IsStatic?: number;
  HasGetter?: number;
  HasSetter?: number;
  Source: string;
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
  TargetParams?: string | null;
  PatchType: string;
  PatchClass: string;
  PatchMethod: string;
  Priority: string | null;
  Source: string;
}

export interface TypeInfoResult {
  Kind: "type";
  FullName: string;
  Namespace: string | null;
  BaseType: string | null;
  IsAbstract: boolean;
  IsInterface: boolean;
  IsEnum: boolean;
  IsSealed: boolean;
  Accessibility: string | null;
  Source: SourceInfo;
  Parents: InheritanceEntry[];
  Children: { FullName: string }[];
  MemberCounts: { Methods: number; Fields: number; Properties: number };
  HarmonyPatches: HarmonyPatchEntry[];
  Decompiled?: string;
}

export interface MethodReference {
  FullName: string;
  Signature: string;
  ReturnType?: string;
  Source: string;
}

export interface MethodInfoResult {
  Kind: "method";
  FullName: string;
  Signature: string;
  ReturnType: string;
  IsStatic: boolean;
  IsVirtual: boolean;
  IsAbstract: boolean;
  Accessibility: string | null;
  Source: SourceInfo;
  ParentType: { FullName: string; Namespace: string; BaseType: string } | null;
  Overloads: { Signature: string; ReturnType: string }[];
  Callers: MethodReference[];
  CallersTruncated: boolean;
  Callees: MethodReference[];
  CalleesTruncated: boolean;
  HarmonyPatches: HarmonyPatchEntry[];
  Decompiled?: string;
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
  TypeName: string;
  Methods?: MemberMethod[];
  Fields?: MemberField[];
  Properties?: MemberProperty[];
}

// --- call tree ---
export interface CallTreeNode {
  Method: string;
  Signature: string;
  IsCycle?: boolean;
  Truncated?: number;
  Children: CallTreeNode[];
}

// --- defs ---
export interface DefSummary {
  DefName: string;
  DefType: string;
  Label: string | null;
  IsAbstract?: number;
  ParentDef?: string | null;
  Source: string;
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
  Source: string;
}

export interface DefTypeCount {
  DefType: string;
  Count: number;
}

// --- harmony ---
export interface HarmonyPatchResult {
  TargetType: string;
  TargetMethod: string | null;
  TargetParams: string | null;
  PatchType: string;
  PatchClass: string;
  PatchMethod: string;
  Priority: string | null;
  Source: string;
}

// --- sources ---
export interface SourceResult {
  Name: string;
  Type: string;
  PackageId: string | null;
}
