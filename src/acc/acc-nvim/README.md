# acc.nvim

Neovim plugin for the [ACC (AdaptiveCodecContext)](https://github.com/KeryxLabs/KeryxInstrumenta) engine.
Indexes your codebase into a live code graph and surfaces AVEC dimensional metrics
(Stability, Logic, Friction, Autonomy) directly in your editor.

## Requirements

- Neovim ≥ 0.9
- `curl` + `tar` in PATH (for auto-download)
- An LSP client attached to your project (e.g. `nvim-lspconfig`)
- `lizard` Python package (optional, for cyclomatic complexity — installed automatically on prompt)

## Installation

### lazy.nvim
```lua
{
  "KeryxLabs/acc.nvim",
  event = "VeryLazy",
  opts = {},   -- uses defaults; see Configuration below
}
```

### packer.nvim
```lua
use {
  "KeryxLabs/acc.nvim",
  config = function()
    require("acc").setup()
  end,
}
```

## Configuration

All options are optional — the defaults work out of the box.

```lua
require("acc").setup({
  -- ACC engine connection
  host = "localhost",
  port = 9339,

  -- Path to acc binary. nil = auto-managed (downloaded to Neovim's data dir)
  server_path = nil,

  -- Path to lizard binary. nil = auto-detect from PATH
  lizard_path = nil,

  -- Engine startup wait in ms
  startup_timeout = 5000,

  -- Engine process settings
  engine = {
    rpc_port           = 9339,
    remote_db          = false,
    remote_endpoint    = "localhost:8000/rpc",
    git_branch_naming  = true,
    telemetry          = false,
    telemetry_endpoint = "localhost:4317",
  },

  -- Keymaps (set any to false to disable)
  keymaps = {
    build_graph   = "<leader>ab",
    search        = "<leader>as",
    stats         = "<leader>aS",
    high_friction = "<leader>af",
    unstable      = "<leader>au",
  },

  -- AVEC weight overrides (only applied when override = true)
  avec_weights = {
    override = false,
    -- stability = { churn_weight = 0.4, ... }
    -- logic     = { complexity_weight = 0.7, ... }
    -- friction  = { centrality_weight = 0.4, ... }
    -- autonomy  = { file_number_blast_radius = 30, ... }
  },
})
```

## Commands

| Command               | Description                              |
|-----------------------|------------------------------------------|
| `:AccBuildGraph`      | Index all source files in the workspace  |
| `:AccSearch`          | Search nodes by name (fuzzy picker)      |
| `:AccStats`           | Show aggregate AVEC health stats         |
| `:AccHighFriction`    | List high-friction bottleneck nodes      |
| `:AccUnstable`        | List unstable high-churn nodes           |
| `:AccDownloadServer`  | Download / re-download the engine binary |

## Statusline

### lualine
```lua
require("lualine").setup({
  sections = {
    lualine_x = { require("acc").lualine_component },
  },
})
```

### Manual (any statusline)
```lua
-- Returns a string like "✓ S:82% F:31%" when the engine is running
require("acc").statusline()
```

## How it works

1. On startup the plugin checks for the ACC engine binary in Neovim's data directory
   (`~/.local/share/nvim/acc/server/`). If missing, it prompts to download it from GitHub Releases.
2. The engine process is spawned in the background, pointing at the current working directory.
3. On `BufEnter` and `BufWritePost`, the plugin taps Neovim's built-in LSP client to collect
   document symbols and references, then forwards them to the engine via a named pipe using
   LSP Content-Length framing — exactly as the VSCode extension does.
4. All query commands (search, stats, friction, unstable) talk to the engine over TCP JSON-RPC 2.0
   on `localhost:9339`.
