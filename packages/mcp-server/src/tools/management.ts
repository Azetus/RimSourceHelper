import { spawn } from "node:child_process";
import { dirname } from "node:path";
import type { Config } from "../config.js";
import type { SourceResult } from "../types.js";
import { runAnalyzer } from "../utils/analyzer.js";
import { withDatabase } from "../utils/database.js";
import { formatSourceList } from "../utils/formatter.js";
import { acquireLock, readLock, releaseLock } from "../utils/indexLock.js";

function getEmbeddingUrl(config: Config): string | null {
  if (!config.embeddingService) return null;
  return `http://${config.embeddingService.host}:${config.embeddingService.port}`;
}

function isVectorEnabled(config: Config): boolean {
  return !!(config.vectorIndex?.enabled && config.vectorIndex?.databasePath);
}

// 异步 spawn 索引构建进程，不阻塞响应
function spawnIndex(config: Config, sourceId: number | null): string | null {
  const vectorDbPath = config.vectorIndex!.databasePath;
  const embeddingUrl = getEmbeddingUrl(config);
  if (!embeddingUrl) return "embedding-service not configured";

  const lock = readLock(vectorDbPath);
  if (lock) {
    const sid = lock.sourceId ?? "all";
    return `Index already building (sourceId=${sid}, started ${new Date(lock.started).toISOString()})`;
  }

  if (!acquireLock(vectorDbPath, sourceId)) return "Failed to acquire index lock";

  const args = [
    config.analyzerPath,
    "index",
    "--db", config.databasePath,
    "--vector-db", vectorDbPath,
    "--embedding-url", embeddingUrl,
    "--batch-size", String(config.embeddingService?.batchSize ?? 128),
  ];
  if (sourceId !== null) args.push("--source-id", String(sourceId));

  const proc = spawn("dotnet", args, {
    detached: true,
    stdio: "ignore",
    windowsHide: true,
    cwd: dirname(config.analyzerPath),
    shell: true,
  });
  proc.on("error", () => releaseLock(vectorDbPath));
  proc.unref();

  return null; // null = success (index spawned)
}

// build_database: 构建/重建知识库，异步触发索引
export async function buildDatabase(args: Record<string, unknown>, config: Config) {
  const stdout = await runAnalyzer(config.analyzerPath, [
    "build",
    "--game-path", config.gamePath,
    "--output", config.databasePath,
  ]);

  if (isVectorEnabled(config)) {
    const lockError = spawnIndex(config, null);
    if (lockError) {
      return { content: [{ type: "text" as const, text: `${stdout}\nIndex: ${lockError}` }] };
    }
    return { content: [{ type: "text" as const, text: stdout + '\n{"indexStatus":"building"}' }] };
  }

  return { content: [{ type: "text" as const, text: stdout }] };
}

// add_mod: 添加 Mod + 异步索引
export async function addMod(args: Record<string, unknown>, config: Config) {
  const modPath = args.mod_path as string;
  const stdout = await runAnalyzer(config.analyzerPath, [
    "add-mod",
    "--mod-path", modPath,
    "--db", config.databasePath,
    "--game-path", config.gamePath,
  ]);

  let parsed: any;
  try { parsed = JSON.parse(stdout); } catch { parsed = {}; }

  if (isVectorEnabled(config) && parsed.sourceId) {
    const lockError = spawnIndex(config, parsed.sourceId);
    if (lockError) {
      parsed.indexStatus = lockError;
    } else {
      parsed.indexStatus = "building";
    }
    return { content: [{ type: "text" as const, text: JSON.stringify(parsed) }] };
  }

  return { content: [{ type: "text" as const, text: stdout }] };
}

// remove_mod: 从知识库移除 Mod
export async function removeMod(args: Record<string, unknown>, config: Config) {
  const modName = args.mod_name as string;
  const vectorDbPath = isVectorEnabled(config) ? config.vectorIndex!.databasePath : null;

  if (vectorDbPath) {
    const lock = readLock(vectorDbPath);
    if (lock) {
      return { content: [{ type: "text" as const, text: `Index is currently building. Cannot remove mod while index is in progress.` }], isError: true };
    }
  }

  const cliArgs = ["remove-mod", "--name", modName, "--db", config.databasePath];
  if (vectorDbPath) cliArgs.push("--vector-db", vectorDbPath);

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
