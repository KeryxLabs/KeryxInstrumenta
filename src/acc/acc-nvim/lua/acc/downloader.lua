-- lua/acc/downloader.lua
-- Handles downloading the ACC engine binary and ensuring lizard is available.

local M = {}

local ACC_VERSION = "0.3.2"
local GITHUB_RELEASES_URL = "https://github.com/KeryxLabs/KeryxInstrumenta/releases/download"
local ACC_RELEASE_TAG = "acc-engine/v" .. ACC_VERSION

-- Returns the data directory where the binary is stored
local function server_dir()
  return vim.fn.stdpath("data") .. "/acc/server"
end

-- Detect platform asset name and binary name
local function platform_info()
  local sysname = vim.loop.os_uname().sysname:lower()
  local machine = vim.loop.os_uname().machine:lower()

  local asset, binary

  if sysname:find("darwin") then
    binary = "acc"
    asset = machine:find("arm") and
      ("acc-" .. ACC_VERSION .. "-macos-arm64.tar.gz") or
      ("acc-" .. ACC_VERSION .. "-macos-x64.tar.gz")
  elseif sysname:find("linux") then
    binary = "acc"
    asset = machine:find("arm") and
      ("acc-" .. ACC_VERSION .. "-linux-arm64.tar.gz") or
      ("acc-" .. ACC_VERSION .. "-linux-x64.tar.gz")
  elseif sysname:find("windows") then
    binary = "acc.exe"
    asset = "acc-" .. ACC_VERSION .. "-win-x64.tar.gz"
  else
    return nil
  end

  return { asset = asset, binary = binary }
end

-- Returns the managed binary path if it exists, otherwise nil
function M.get_server_path(cfg)
  if cfg.server_path and vim.fn.executable(cfg.server_path) == 1 then
    return cfg.server_path
  end

  local info = platform_info()
  if not info then return nil end

  local bin = server_dir() .. "/" .. info.binary
  return vim.fn.filereadable(bin) == 1 and bin or nil
end

-- Returns "lizard" if it's on PATH, otherwise nil
function M.get_lizard_path(cfg)
  if cfg.lizard_path then return cfg.lizard_path end
  return vim.fn.executable("lizard") == 1 and "lizard" or nil
end

-- Download + extract the ACC binary, call cb(path_or_nil) when done
function M.download_server(cb)
  local info = platform_info()
  if not info then
    vim.notify("ACC: Unsupported platform", vim.log.levels.ERROR)
    cb(nil)
    return
  end

  local dir      = server_dir()
  local archive  = dir .. "/" .. info.asset
  local bin_path = dir .. "/" .. info.binary
  local url      = GITHUB_RELEASES_URL .. "/" .. ACC_RELEASE_TAG .. "/" .. info.asset

  vim.fn.mkdir(dir, "p")

  vim.notify("ACC: Downloading engine v" .. ACC_VERSION .. "...", vim.log.levels.INFO)

  -- Use curl (available on all platforms Neovim targets)
  local curl_cmd = { "curl", "-fsSL", "--output", archive, "-L", url }

  vim.fn.jobstart(curl_cmd, {
    on_exit = function(_, code)
      if code ~= 0 then
        vim.notify("ACC: Download failed (curl exit " .. code .. ")", vim.log.levels.ERROR)
        cb(nil)
        return
      end

      vim.notify("ACC: Extracting...", vim.log.levels.INFO)

      local tar_cmd = { "tar", "-xzf", archive, "-C", dir }
      vim.fn.jobstart(tar_cmd, {
        on_exit = function(_, tcode)
          -- Clean up archive regardless
          vim.fn.delete(archive)

          if tcode ~= 0 then
            vim.notify("ACC: Extraction failed", vim.log.levels.ERROR)
            cb(nil)
            return
          end

          -- Make executable on Unix
          if not vim.loop.os_uname().sysname:lower():find("windows") then
            vim.fn.jobstart({ "chmod", "+x", bin_path }, {
              on_exit = function()
                vim.notify("ACC: Engine installed at " .. bin_path, vim.log.levels.INFO)
                cb(bin_path)
              end
            })
          else
            vim.notify("ACC: Engine installed at " .. bin_path, vim.log.levels.INFO)
            cb(bin_path)
          end
        end,
      })
    end,
  })
end

-- Ensure the server binary exists; prompt to download if not
function M.ensure_server_installed(cfg, cb)
  local path = M.get_server_path(cfg)
  if path then
    cb(path)
    return
  end

  vim.ui.select({ "Download", "Cancel" }, {
    prompt = "ACC engine not found. Download v" .. ACC_VERSION .. "?",
  }, function(choice)
    if choice ~= "Download" then
      cb(nil)
      return
    end
    M.download_server(cb)
  end)
end

-- Ensure lizard is available; prompt to pip-install if not
function M.ensure_lizard_installed(cfg, cb)
  local path = M.get_lizard_path(cfg)
  if path then
    cb(path)
    return
  end

  vim.ui.select({ "Install via pip", "Skip" }, {
    prompt = "lizard not found. Install via pip for full functionality?",
  }, function(choice)
    if choice ~= "Install via pip" then
      vim.notify("ACC: lizard not available — some features may be limited", vim.log.levels.WARN)
      cb(nil)
      return
    end

    -- Find python
    local python = vim.fn.executable("python3") == 1 and "python3"
      or vim.fn.executable("python") == 1 and "python"
      or nil

    if not python then
      vim.notify("ACC: Python not found in PATH — install Python to use pip", vim.log.levels.ERROR)
      cb(nil)
      return
    end

    vim.notify("ACC: Installing lizard via pip...", vim.log.levels.INFO)

    vim.fn.jobstart(
      { python, "-m", "pip", "install", "--upgrade", "--user", "lizard" },
      {
        on_exit = function(_, code)
          if code == 0 and vim.fn.executable("lizard") == 1 then
            vim.notify("ACC: lizard installed", vim.log.levels.INFO)
            cb("lizard")
          else
            vim.notify("ACC: lizard install failed — install manually with `pip install lizard`", vim.log.levels.ERROR)
            cb(nil)
          end
        end,
      }
    )
  end)
end

return M
