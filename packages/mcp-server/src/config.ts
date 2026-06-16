import { readFileSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

export interface Config {
  gamePath: string;
  databasePath: string;
  analyzerPath: string;
}

// 从项目根目录加载 config.json，将相对路径解析为绝对路径
export function loadConfig(): Config {
  const __dirname = dirname(fileURLToPath(import.meta.url));
  const rootDir = resolve(__dirname, "../../..");
  const configPath = resolve(rootDir, "config.json");
  const raw = readFileSync(configPath, "utf-8");
  const config = JSON.parse(raw) as Config;

  config.databasePath = resolve(rootDir, config.databasePath);
  config.analyzerPath = resolve(rootDir, config.analyzerPath);

  return config;
}
