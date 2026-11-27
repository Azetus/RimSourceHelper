import { execFileSync } from "node:child_process";
import { constants, copyFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { build } from "esbuild";

const target = process.argv[2];
if (target !== "release" && target !== "local-run") {
  throw new Error("Expected build target release or local-run.");
}

const root = fileURLToPath(new URL("../", import.meta.url));
const output = join(root, target);

execFileSync("dotnet", [
  "publish", "packages/rim-analyzer/rim-analyzer.csproj", "-c", "Release", "-o", join(output, "rim-analyzer"),
], { cwd: root, stdio: "inherit" });

await build({
  absWorkingDir: root,
  entryPoints: ["packages/mcp-server/src/index.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: join(output, "index.js"),
});

writeFileSync(join(output, "package.json"), '{"type":"module"}\n');
copyFileSync(join(root, "opencode.example.release.json"), join(output, "opencode.example.json"));

// Initialize local config once so rebuilds preserve the user's settings.
const keepConfig = target === "local-run";
try {
  copyFileSync(join(root, "config.release.json"), join(output, "config.json"),
    keepConfig ? constants.COPYFILE_EXCL : 0);
} catch (error) {
  if (!keepConfig || error.code !== "EEXIST") throw error;
}
