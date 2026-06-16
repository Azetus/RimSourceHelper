import type { Config } from "../config.js";

// build_database: 构建/重建知识库
export async function buildDatabase(args: Record<string, unknown>, config: Config) {
  // TODO: spawn rim-analyzer build --game-path ... --output ...
  return { content: [{ type: "text" as const, text: "TODO: build_database()" }] };
}

// add_mod: 添加 Mod 到知识库
export async function addMod(args: Record<string, unknown>, config: Config) {
  const modPath = args.mod_path as string;

  // TODO: spawn rim-analyzer add-mod --mod-path ... --db ... --game-path ...
  return { content: [{ type: "text" as const, text: `TODO: add_mod(mod_path="${modPath}")` }] };
}

// remove_mod: 从知识库移除 Mod
export async function removeMod(args: Record<string, unknown>, config: Config) {
  const modName = args.mod_name as string;

  // TODO: spawn rim-analyzer remove-mod --name ... --db ...
  return { content: [{ type: "text" as const, text: `TODO: remove_mod(mod_name="${modName}")` }] };
}

// list_sources: 列出数据库中所有 Source
export async function listSources(args: Record<string, unknown>, config: Config) {
  // TODO: SQLite 查询 Sources 表
  return { content: [{ type: "text" as const, text: "TODO: list_sources()" }] };
}
