import { readFileSync, existsSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

export interface Config {
  gamePath: string;
  databasePath: string;
  analyzerPath: string;
}

// 从脚本所在目录向上逐级搜索 config.json
export function loadConfig(): Config {
  const configPath = findConfig();
  const configDir = dirname(configPath);
  const raw = readFileSync(configPath, "utf-8");
  const config = JSON.parse(raw) as Config;

  // 相对路径基于 config.json 所在目录解析
  config.databasePath = resolve(configDir, config.databasePath);
  config.analyzerPath = resolve(configDir, config.analyzerPath);

  return config;
}

function findConfig(): string {
  let dir = dirname(fileURLToPath(import.meta.url));
  while (true) {
    const candidate = resolve(dir, "config.json");
    if (existsSync(candidate)) return candidate;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error("config.json not found. See config.example.json for the template.");
}
