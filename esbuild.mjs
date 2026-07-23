import { build } from "esbuild";

await build({
    entryPoints: [
        "packages/mcp-server/src/index.ts"
    ],

    bundle: true,

    platform: "node",

    format: "esm",

    outfile: "release/mcp/index.js",

    external: [
        "better-sqlite3",
        "sqlite-vec"
    ]
});

console.log("MCP build completed.");
