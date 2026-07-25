import { writeFileSync, readFileSync, unlinkSync, existsSync } from "node:fs";

interface LockInfo {
  pid: number;
  sourceId: number | null;
  started: number; // unix timestamp ms
}

function lockPath(vectorDbPath: string): string {
  return vectorDbPath + ".lock";
}

// 尝试获取锁。已存在则返回 null，否则创建锁文件并返回 LockInfo
export function acquireLock(vectorDbPath: string, sourceId: number | null): LockInfo | null {
  const path = lockPath(vectorDbPath);
  if (existsSync(path)) return null;

  const info: LockInfo = {
    pid: process.pid,
    sourceId,
    started: Date.now(),
  };

  writeFileSync(path, JSON.stringify(info), "utf-8");
  return info;
}

// 释放锁（删除锁文件）
export function releaseLock(vectorDbPath: string): void {
  const path = lockPath(vectorDbPath);
  if (existsSync(path)) unlinkSync(path);
}

// 读取锁内容
export function readLock(vectorDbPath: string): LockInfo | null {
  const path = lockPath(vectorDbPath);
  if (!existsSync(path)) return null;
  try {
    return JSON.parse(readFileSync(path, "utf-8"));
  } catch {
    return null;
  }
}
