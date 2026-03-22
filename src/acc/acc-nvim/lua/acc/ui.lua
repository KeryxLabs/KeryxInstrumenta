-- lua/acc/ui.lua
-- UI helpers: search picker, node list picker, statusline component.
-- Uses vim.ui.select for pickers (works with telescope, fzf-lua, nui, etc.)

local M = {}

-- Format AVEC scores into a compact string
local function avec_str(avec)
  if not avec then return "No AVEC" end
  return string.format(
    "S:%.2f L:%.2f F:%.2f A:%.2f",
    avec.stability or 0,
    avec.logic     or 0,
    avec.friction  or 0,
    avec.autonomy  or 0
  )
end

-- Open a file at a given (0-indexed) line
local function jump_to(file_path, line)
  if not file_path or not vim.fn.filereadable(file_path) then
    vim.notify("ACC: Cannot open file: " .. (file_path or "nil"), vim.log.levels.ERROR)
    return
  end

  vim.cmd("edit " .. vim.fn.fnameescape(file_path))
  -- line from ACC is 0-indexed (LSP convention)
  vim.api.nvim_win_set_cursor(0, { line + 1, 0 })
  vim.cmd("normal! zz")
end

-- Show a list of nodes in a picker; selecting one jumps to the file
function M.show_node_list(title, nodes)
  if not nodes or #nodes == 0 then
    vim.notify("ACC: " .. title .. ": No results", vim.log.levels.INFO)
    return
  end

  local items = vim.tbl_map(function(node)
    return {
      display = string.format(
        "%-40s  %s  |  %s",
        node.name or "?",
        avec_str(node.avec),
        node.file_path or ""
      ),
      node = node,
    }
  end, nodes)

  vim.ui.select(items, {
    prompt = title,
    format_item = function(item) return item.display end,
  }, function(selected)
    if selected then
      jump_to(selected.node.file_path, selected.node.line_start or 0)
    end
  end)
end

-- Show search results — same picker shape as showSearchResults in extension.ts
function M.show_search_results(results)
  M.show_node_list("ACC Search Results", results)
end

-- ── Statusline component ────────────────────────────────────────────────────

local _status = { text = "", tooltip = "" }

function M.update_statusline(metrics)
  if not metrics then
    _status.text    = ""
    _status.tooltip = ""
    return
  end

  local icon = (metrics.friction or 0) > 0.7 and "⚠" or "✓"
  _status.text    = string.format(
    "%s S:%d%% F:%d%%",
    icon,
    math.floor((metrics.stability or 0) * 100),
    math.floor((metrics.friction  or 0) * 100)
  )
  _status.tooltip = string.format("Logic Density: %.2f", metrics.logic or 0)
end

-- Call this from your statusline (e.g. lualine custom component)
-- Returns a string like "✓ S:82% F:31%"
function M.statusline()
  return _status.text
end

function M.statusline_tooltip()
  return _status.tooltip
end

-- Built-in lualine component definition (optional)
-- Usage in lualine config:  sections = { lualine_x = { require("acc.ui").lualine_component } }
M.lualine_component = {
  function() return M.statusline() end,
  cond = function() return _status.text ~= "" end,
}

return M
