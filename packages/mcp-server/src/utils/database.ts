import Database from "better-sqlite3";
import { existsSync } from "fs";

// 回调模式打开数据库：自动关闭连接，防止泄漏
export function withDatabase<T>(dbPath: string, fn: (db: Database.Database) => T): T {
  if (!existsSync(dbPath))
    throw new Error(`Database not found: ${dbPath}. Run build_database first.`);

  const db = new Database(dbPath, { readonly: true });
  try {
    return fn(db);
  } finally {
    db.close();
  }
}
