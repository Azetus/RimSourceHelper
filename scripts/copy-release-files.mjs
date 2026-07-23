import {
    cpSync,
    mkdirSync,
    existsSync
} from "node:fs";

mkdirSync("release", {
    recursive: true
});


// 配置文件

cpSync(
    "config.release.json",
    "release/config.json"
);

cpSync(
    "opencode.example.release.json",
    "release/opencode.example.json"
);


// MCP bundle

if (existsSync("release/mcp-build/index.js")) {

    cpSync(
        "release/mcp-build/index.js",
        "release/mcp/index.js"
    );

}


// 删除临时目录

import { rmSync } from "node:fs";

rmSync(
    "release/mcp-build",
    {
        recursive: true,
        force: true
    }
);


console.log("Release files copied.");
