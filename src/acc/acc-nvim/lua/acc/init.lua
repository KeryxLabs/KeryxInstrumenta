-- lua/acc/init.lua
-- Public API. Call require("acc").setup(opts) in your Neovim config.

local M = {}

local config     = require("acc.config")
local downloader = require("acc.downloader")
local engine     = require("acc.engine")
local client     = require("acc.client")
local lsp        = require("acc.lsp")
local ui         = require("acc.ui")

local cfg = {}

-- Convenience wrapper: call ACC engine RPC and handle errors uniformly
local function rpc(method, params, cb)
  client.call(cfg.host, cfg.port, method, params, function(err, result)
    if err then
      vim.notify("ACC: " .. err, vim.log.levels.ERROR)
      return
    end
    cb(result)
  end)
end

-- ── Commands ────────────────────────────────────────────────────────────────

local function cmd_build_graph()
  lsp.build_graph(client, cfg)
end

local function cmd_search()
  vim.ui.input({ prompt = "ACC Search: " }, function(query)
    if not query or query == "" then return end
    rpc("acc.search", { name = query, limit = 10 }, function(results)
      ui.show_search_results(results)
    end)
  end)
end

local function cmd_stats()
  rpc("acc.getStats", {}, function(stats)
    if not stats then
      vim.notify("ACC: No stats returned", vim.log.levels.WARN)
      return
    end

    vim.notify(string.format(
      "ACC Stats: %d nodes | Logic:%.2f Stability:%.2f Friction:%.2f Autonomy:%.2f",
      stats.total_nodes   or 0,
      stats.avg_logic     or 0,
      stats.avg_stability or 0,
      stats.avg_friction  or 0,
      stats.avg_autonomy  or 0
    ), vim.log.levels.INFO)

    ui.update_statusline({
      stability = stats.avg_stability,
      friction  = stats.avg_friction,
      logic     = stats.avg_logic,
    })
  end)
end

local function cmd_high_friction()
  rpc("acc.getHighFriction", { minFriction = 0.7, limit = 20 }, function(nodes)
    ui.show_node_list("High-Friction Nodes (Bottlenecks)", nodes)
  end)
end

local function cmd_unstable()
  rpc("acc.getUnstable", { maxStability = 0.4, limit = 20 }, function(nodes)
    ui.show_node_list("Unstable Nodes (High Churn)", nodes)
  end)
end

local function cmd_download_server()
  downloader.download_server(function(path)
    if path then
      engine.start(path, cfg, function()
        vim.notify("ACC: Engine started at " .. path, vim.log.levels.INFO)
      end)
    end
  end)
end

-- ── Autocommands ────────────────────────────────────────────────────────────

local function register_autocommands()
  local group = vim.api.nvim_create_augroup("AccNvim", { clear = true })

  -- Forward symbols on save (mirrors onDidSaveTextDocument)
  vim.api.nvim_create_autocmd("BufWritePost", {
    group    = group,
    callback = function(ev)
      lsp.tap_and_forward(ev.buf, client, cfg)
    end,
  })

  -- Forward symbols when switching buffers (mirrors onDidChangeActiveTextEditor)
  vim.api.nvim_create_autocmd("BufEnter", {
    group    = group,
    callback = function(ev)
      -- Only tap if an LSP client is attached (otherwise no symbols to forward)
      if #vim.lsp.get_clients({ bufnr = ev.buf }) > 0 then
        lsp.tap_and_forward(ev.buf, client, cfg)
      end
    end,
  })

  -- Clean up on exit
  vim.api.nvim_create_autocmd("VimLeavePre", {
    group    = group,
    callback = function()
      lsp.cleanup()
      engine.stop()
    end,
  })
end

-- ── Keymaps ─────────────────────────────────────────────────────────────────

local function register_keymaps(km)
  if not km then return end
  local function map(lhs, fn, desc)
    if lhs then
      vim.keymap.set("n", lhs, fn, { desc = desc, silent = true })
    end
  end
  map(km.build_graph,   cmd_build_graph,   "ACC: Build dependency graph")
  map(km.search,        cmd_search,        "ACC: Search nodes")
  map(km.stats,         cmd_stats,         "ACC: Show project stats")
  map(km.high_friction, cmd_high_friction, "ACC: Show high-friction nodes")
  map(km.unstable,      cmd_unstable,      "ACC: Show unstable nodes")
end

-- ── User commands ────────────────────────────────────────────────────────────

local function register_user_commands()
  vim.api.nvim_create_user_command("AccBuildGraph",    cmd_build_graph,    { desc = "ACC: Build dependency graph" })
  vim.api.nvim_create_user_command("AccSearch",        cmd_search,         { desc = "ACC: Search nodes by name" })
  vim.api.nvim_create_user_command("AccStats",         cmd_stats,          { desc = "ACC: Show project stats" })
  vim.api.nvim_create_user_command("AccHighFriction",  cmd_high_friction,  { desc = "ACC: Show high-friction nodes" })
  vim.api.nvim_create_user_command("AccUnstable",      cmd_unstable,       { desc = "ACC: Show unstable nodes" })
  vim.api.nvim_create_user_command("AccDownloadServer",cmd_download_server,{ desc = "ACC: Download engine binary" })
end

-- ── Setup ────────────────────────────────────────────────────────────────────

function M.setup(opts)
  cfg = vim.tbl_deep_extend("force", config.defaults, opts or {})

  register_user_commands()
  register_keymaps(cfg.keymaps)
  register_autocommands()

  -- Ensure server binary exists, then start the engine
  downloader.ensure_server_installed(cfg, function(server_path)
    if not server_path then
      vim.notify(
        'ACC: Engine not installed. Run :AccDownloadServer to install.',
        vim.log.levels.WARN
      )
      return
    end

    -- Non-blocking lizard check (doesn't block engine startup)
    downloader.ensure_lizard_installed(cfg, function(lizard_path)
      if not lizard_path then
        vim.notify("ACC: lizard not available — some features may be limited", vim.log.levels.WARN)
      end
    end)

    engine.start(server_path, cfg, function()
      -- Tap the current buffer once the engine is ready
      local bufnr = vim.api.nvim_get_current_buf()
      if #vim.lsp.get_clients({ bufnr = bufnr }) > 0 then
        lsp.tap_and_forward(bufnr, client, cfg)
      end

      vim.notify('ACC: Ready! Run :AccBuildGraph to index your codebase.', vim.log.levels.INFO)
    end)
  end)
end

-- Expose ui module for statusline integration
M.statusline         = ui.statusline
M.lualine_component  = ui.lualine_component

return M
