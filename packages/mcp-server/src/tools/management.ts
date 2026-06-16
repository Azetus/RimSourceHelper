import type { Config } from "../config.js";
import { runAnalyzer } from "../utils/analyzer.js";
import { withDatabase } from "../utils/database.js";

// build_database: 构建/重建知识库
export async function buildDatabase(args: Record<string, unknown>, config: Config) {
  const stdout = await runAnalyzer(config.analyzerPath, [
    "build",
    "--game-path", config.gamePath,
    "--output", config.databasePath
  ]);
  return { content: [{ type: "text" as const, text: stdout }] };
}

// add_mod: 添加 Mod 到知识库
export async function addMod(args: Record<string, unknown>, config: Config) {
  const modPath = args.mod_path as string;
  const stdout = await runAnalyzer(config.analyzerPath, [
    "add-mod",
    "--mod-path", modPath,
    "--db", config.databasePath,
    "--game-path", config.gamePath
  ]);
  return { content: [{ type: "text" as const, text: stdout }] };
}

// remove_mod: 从知识库移除 Mod
export async function removeMod(args: Record<string, unknown>, config: Config) {
  const modName = args.mod_name as string;
  const stdout = await runAnalyzer(config.analyzerPath, [
    "remove-mod",
    "--name", modName,
    "--db", config.databasePath
  ]);
  return { content: [{ type: "text" as const, text: stdout }] };
}

// list_sources: 列出数据库中所有 Source
export async function listSources(args: Record<string, unknown>, config: Config) {
  return withDatabase(config.databasePath, (db) => {
    const sources = db.prepare("SELECT Name, Type, PackageId FROM Sources ORDER BY Type, Name").all();
    return { content: [{ type: "text" as const, text: JSON.stringify(sources, null, 2) }] };
  });
}
