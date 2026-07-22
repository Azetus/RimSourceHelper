import type { Config } from "../config.js";
import type { SourceResult } from "../types.js";
import { runAnalyzer } from "../utils/analyzer.js";
import { withDatabase } from "../utils/database.js";
import { formatSourceList } from "../utils/formatter.js";

function buildEmbeddingArgs(config: Config): string[] {
  if (!config.embeddingService) return [];
  return ["--embedding-url", `http://${config.embeddingService.host}:${config.embeddingService.port}`];
}

function buildVectorArgs(config: Config): string[] {
  if (!config.vectorIndex?.enabled || !config.vectorIndex?.databasePath) return [];
  return ["--vector-db", config.vectorIndex.databasePath];
}

// build_database: 构建/重建知识库
export async function buildDatabase(args: Record<string, unknown>, config: Config) {
  const cliArgs = [
    "build",
    "--game-path", config.gamePath,
    "--output", config.databasePath,
    ...buildVectorArgs(config),
    ...buildEmbeddingArgs(config)
  ];
  const stdout = await runAnalyzer(config.analyzerPath, cliArgs);
  return { content: [{ type: "text" as const, text: stdout }] };
}

// add_mod: 添加 Mod 到知识库
export async function addMod(args: Record<string, unknown>, config: Config) {
  const modPath = args.mod_path as string;
  const cliArgs = [
    "add-mod",
    "--mod-path", modPath,
    "--db", config.databasePath,
    "--game-path", config.gamePath,
    ...buildVectorArgs(config),
    ...buildEmbeddingArgs(config)
  ];
  const stdout = await runAnalyzer(config.analyzerPath, cliArgs);
  return { content: [{ type: "text" as const, text: stdout }] };
}

// remove_mod: 从知识库移除 Mod
export async function removeMod(args: Record<string, unknown>, config: Config) {
  const modName = args.mod_name as string;
  const cliArgs = [
    "remove-mod",
    "--name", modName,
    "--db", config.databasePath,
    ...buildVectorArgs(config)
  ];
  const stdout = await runAnalyzer(config.analyzerPath, cliArgs);
  return { content: [{ type: "text" as const, text: stdout }] };
}

// list_sources: 列出数据库中所有 Source
export async function listSources(args: Record<string, unknown>, config: Config) {
  const sources = withDatabase(config.databasePath, (db) => {
    return db.prepare("SELECT Id, Name, Type, PackageId FROM Sources ORDER BY Type, Name").all() as unknown as SourceResult[];
  });
  return { content: [{ type: "text" as const, text: formatSourceList(sources) }] };
}
