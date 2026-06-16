import { cpSync, mkdirSync } from "node:fs";

mkdirSync("release", { recursive: true });
cpSync("config.release.json", "release/config.json");
cpSync("opencode.example.release.json", "release/opencode.example.json");
