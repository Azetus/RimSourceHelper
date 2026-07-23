import { rmSync } from "node:fs";

rmSync("release", {
    recursive: true,
    force: true
});

console.log("Release cleaned.");
