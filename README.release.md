# RimSourceHelper Quick Start

## Requirements

- Node.js 22.13 or newer in the 22.x series, or Node.js 24 or newer.
- .NET 10 Runtime.
- A local RimWorld installation.

Make sure the MCP client can find `node` and `dotnet` on its `PATH`.

## Configure and connect

1. Extract the entire package into one directory.
2. Open `config.json` and set `gamePath` to the absolute path of your RimWorld installation directory, for example `D:/Steam/steamapps/common/RimWorld`.
3. Add a local stdio server in your MCP client. Set its command to `node` and pass the absolute path to the extracted `index.js` as the argument. For OpenCode, use the bundled `opencode.example.json` and replace the path placeholder.

```json
["node", "C:/Tools/RimSourceHelper/index.js"]
```

## Build the knowledge database

After connecting for the first time, ask your agent to run `build_database` before querying RimWorld. Use `add_mod` to index any additional mods you want to inspect.

**Running `build_database` again replaces the entire database and clears all previously imported mod indexes.** Re-import the mods you still need with `add_mod`.
