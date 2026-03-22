-- lua/acc/lsp.lua
-- Taps Neovim's LSP client to forward document symbols and references
-- to the ACC engine via a named pipe (mirrors tapAndForwardSymbols in extension.ts).

local M = {}

local registered_languages = {}
local pipe_handles          = {}  -- language → uv_pipe_t

local RELEVANT_SYMBOL_KINDS = {
  [5]  = true,  -- Class
  [8]  = true,  -- Function
  [6]  = true,  -- Method
  [11] = true,  -- Interface
}

-- Returns the named pipe path for a language (matches getPipePath in extension.ts)
local function pipe_path(language)
  if vim.fn.has("win32") == 1 then
    return "\\\\.\\pipe\\acc-engine-" .. language
  else
    return "/tmp/acc-engine-" .. language .. ".sock"
  end
end

-- Get or create a persistent pipe connection for a language
local function get_pipe(language, cb)
  local existing = pipe_handles[language]
  if existing and not existing:is_closing() then
    cb(nil, existing)
    return
  end

  local pipe = vim.loop.new_pipe(false)
  pipe:connect(pipe_path(language), function(err)
    if err then
      pipe_handles[language] = nil
      cb("Pipe connect error (" .. language .. "): " .. err, nil)
      return
    end

    pipe_handles[language] = pipe

    pipe:read_start(function() end) -- drain any engine ACKs silently

    pipe:once("error",  function() pipe_handles[language] = nil end)
    pipe:once("end",    function() pipe_handles[language] = nil end)
    pipe:once("close",  function() pipe_handles[language] = nil end)

    cb(nil, pipe)
  end)
end

-- Write an LSP message with Content-Length framing to the pipe
local function forward_to_acc(message, language)
  local content        = vim.json.encode(message)
  local content_length = #content
  local framed         = "Content-Length: " .. content_length .. "\r\n\r\n" .. content

  get_pipe(language, function(err, pipe)
    if err then
      vim.notify("ACC LSP: " .. err, vim.log.levels.DEBUG)
      return
    end
    pipe:write(framed)
  end)
end

-- Register a language stream with the engine via JSON-RPC
local function ensure_language_registered(language, client_module, cfg)
  if registered_languages[language] then return end

  client_module.call(cfg.host, cfg.port, "acc.registerLspStream", {
    type     = "pipe",
    language = language,
    path     = pipe_path(language),
  }, function(err)
    if err then
      vim.notify("ACC: Failed to register LSP stream for " .. language .. ": " .. err, vim.log.levels.WARN)
    else
      registered_languages[language] = true
      vim.notify("ACC: Registered LSP stream for " .. language, vim.log.levels.DEBUG)
    end
  end)
end

-- Flatten nested symbols into a single list, filtering by relevant kinds
local function flatten_symbols(symbols, out)
  out = out or {}
  for _, sym in ipairs(symbols or {}) do
    if RELEVANT_SYMBOL_KINDS[sym.kind] then
      table.insert(out, sym)
    end
    if sym.children then flatten_symbols(sym.children, out) end
  end
  return out
end

-- Convert a Neovim LSP symbol to the shape ACC expects
local function convert_symbol(sym)
  return {
    name   = sym.name,
    kind   = sym.kind,
    detail = sym.detail,
    range  = sym.range,
    selectionRange = sym.selectionRange,
    children = sym.children and vim.tbl_map(convert_symbol, sym.children) or nil,
  }
end

-- Tap symbols + references for a buffer and forward to ACC
function M.tap_and_forward(bufnr, client_module, cfg)
  bufnr = bufnr or vim.api.nvim_get_current_buf()

  local uri      = vim.uri_from_bufnr(bufnr)
  local language = vim.bo[bufnr].filetype
  if language == "" then return end

  ensure_language_registered(language, client_module, cfg)

  -- Request document symbols from LSP
  local params = { textDocument = { uri = uri } }

  vim.lsp.buf_request(bufnr, "textDocument/documentSymbol", params, function(err, symbols, _)
    if err or not symbols or #symbols == 0 then return end

    local relevant = flatten_symbols(symbols)
    if #relevant == 0 then return end

    -- Forward the symbol list
    forward_to_acc({
      jsonrpc = "2.0",
      id      = os.time(),
      method  = "textDocument/documentSymbol",
      params  = { textDocument = { uri = uri } },
      result  = vim.tbl_map(convert_symbol, relevant),
    }, language)

    -- Forward references for each symbol with a small stagger
    for i, sym in ipairs(relevant) do
      vim.defer_fn(function()
        local ref_params = {
          textDocument = { uri = uri },
          position     = sym.range.start,
          context      = { includeDeclaration = false },
        }

        vim.lsp.buf_request(bufnr, "textDocument/references", ref_params, function(ref_err, refs, _)
          if ref_err or not refs or #refs == 0 then return end

          local ref_list = vim.tbl_map(function(loc)
            return {
              uri   = loc.uri or loc.targetUri,
              range = loc.range or loc.targetRange,
            }
          end, refs)

          forward_to_acc({
            jsonrpc = "2.0",
            id      = os.time(),
            method  = "textDocument/references",
            params  = {
              textDocument = { uri = uri },
              position     = sym.range.start,
              context      = { includeDeclaration = false },
            },
            result  = ref_list,
          }, language)
        end)
      end, i * 5) -- 5ms stagger per symbol
    end
  end)
end

-- Build graph for all matching source files in the workspace
function M.build_graph(client_module, cfg)
  local extensions = { "cs", "ts", "js", "py", "go", "rs" }
  local pattern    = "**/*.{" .. table.concat(extensions, ",") .. "}"

  -- Use vim.fs.find or a glob job depending on availability
  local files = vim.fn.globpath(
    vim.fn.getcwd(),
    table.concat(vim.tbl_map(function(e) return "**/*." .. e end, extensions), "\n"),
    false, true
  )

  if #files == 0 then
    vim.notify("ACC: No source files found", vim.log.levels.WARN)
    return
  end

  vim.notify("ACC: Indexing " .. #files .. " files...", vim.log.levels.INFO)

  local processed = 0

  local function process_next(i)
    if i > #files then
      vim.notify("ACC: Indexed " .. #files .. " files!", vim.log.levels.INFO)
      return
    end

    local filepath = files[i]
    local bufnr    = vim.fn.bufadd(filepath)
    vim.fn.bufload(bufnr)

    M.tap_and_forward(bufnr, client_module, cfg)

    processed = processed + 1
    vim.defer_fn(function() process_next(i + 1) end, 10)
  end

  process_next(1)
end

-- Close all open pipe connections
function M.cleanup()
  for lang, pipe in pairs(pipe_handles) do
    if not pipe:is_closing() then pipe:close() end
    pipe_handles[lang] = nil
  end
  registered_languages = {}
end

return M
