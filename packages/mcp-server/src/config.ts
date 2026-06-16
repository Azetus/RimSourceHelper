import { readFileSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

export interface Config {
  gamePath: string;
  databasePath: string;
  analyzerPath: string;
}

// 从项目根目录加载 config.json
export function loadConfig(): Config {
  const __dirname = dirname(fileURLToPath(import.meta.url));
  const configPath = resolve(__dirname, "../../..", "config.json");
  const raw = readFileSync(configPath, "utf-8");
  return JSON.parse(raw) as Config;
}
