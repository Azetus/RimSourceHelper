import { spawn } from "child_process";

// 调用 rim-analyzer 子进程，返回 stdout 内容（JSON 字符串）
export function runAnalyzer(analyzerPath: string, args: string[]): Promise<string> {
  return new Promise((resolve, reject) => {
    const proc = spawn(analyzerPath, args, { stdio: ["ignore", "pipe", "pipe"] });
    let stdout = "";
    let stderr = "";

    proc.stdout.on("data", (chunk: Buffer) => { stdout += chunk.toString(); });
    proc.stderr.on("data", (chunk: Buffer) => { stderr += chunk.toString(); });

    proc.on("error", (err) => {
      reject(new Error(`Failed to start rim-analyzer: ${err.message}`));
    });

    proc.on("close", () => {
      // rim-analyzer 无论成功/失败都输出 JSON 到 stdout
      resolve(stdout.trim());
    });
  });
}
