# RimSourceHelper MCP Setup

## Requirements

- Node.js 22.13 or newer in the 22.x series, or Node.js 24 or newer.
- .NET 10 Runtime.
- A local RimWorld installation.

Verify that the MCP client can find `node` and `dotnet` on its `PATH`.

## Configure and connect

1. Set `gamePath` in the adjacent `config.json` to the absolute path of the user's RimWorld installation directory, such as `D:/Steam/steamapps/common/RimWorld`.
2. Register RimSourceHelper as a local stdio server in the MCP client. Launch `node` with the absolute path to the adjacent `index.js`. For OpenCode, adapt the MCP entry in `opencode.example.json` and replace the path placeholder.

```json
["node", "C:/Tools/RimSourceHelper/index.js"]
```

## Initialize the knowledge database

After connecting, call `build_database` if the knowledge database has yet to be initialized. Use `list_sources` to inspect an existing database and `add_mod` to index any mods the user wants to query.

**Running `build_database` again replaces the entire database and clears all previously imported mod indexes.** After rebuilding, re-import the required mods with `add_mod`.
