-- lua/acc/client.lua
-- Async TCP JSON-RPC 2.0 client using vim.loop (libuv).
-- The ACC engine handles one request per connection (connect → write → read → close).

local M = {}
local request_id = 1

-- Send a JSON-RPC request and call cb(err, result)
function M.call(host, port, method, params, cb)
  local id = request_id
  request_id = request_id + 1

  local envelope = vim.json.encode({
    jsonrpc = "2.0",
    id      = id,
    method  = method,
    params  = params or {},
  })

  local tcp     = vim.loop.new_tcp()
  local buffer  = ""
  local done    = false

  local function finish(err, result)
    if done then return end
    done = true
    if not tcp:is_closing() then tcp:close() end
    -- Schedule back onto the main loop so callers can update UI safely
    vim.schedule(function() cb(err, result) end)
  end

  tcp:connect(host, port, function(conn_err)
    if conn_err then
      finish("Connection error: " .. conn_err, nil)
      return
    end

    tcp:read_start(function(read_err, chunk)
      if read_err then
        finish("Read error: " .. read_err, nil)
        return
      end

      if chunk then
        -- Strip UTF-8 BOM if present
        if chunk:byte(1) == 0xEF and chunk:byte(2) == 0xBB and chunk:byte(3) == 0xBF then
          chunk = chunk:sub(4)
        end
        buffer = buffer .. chunk

        -- Try to parse as soon as we have a complete JSON object
        local ok, parsed = pcall(vim.json.decode, buffer)
        if ok and parsed then
          if parsed.error then
            finish("ACC error " .. (parsed.error.code or 0) .. ": " .. (parsed.error.message or "unknown"), nil)
          else
            finish(nil, parsed.result)
          end
        end
      else
        -- EOF — attempt final parse
        if buffer ~= "" then
          local ok, parsed = pcall(vim.json.decode, buffer)
          if ok and parsed then
            if parsed.error then
              finish("ACC error " .. (parsed.error.code or 0) .. ": " .. (parsed.error.message or "unknown"), nil)
            else
              finish(nil, parsed.result)
            end
          else
            finish("Invalid JSON response", nil)
          end
        else
          finish("Empty response", nil)
        end
      end
    end)

    -- Write request followed by newline (matches C# ReadLineAsync)
    tcp:write(envelope .. "\n", function(write_err)
      if write_err then
        finish("Write error: " .. write_err, nil)
      end
    end)
  end)

  -- 5 second timeout
  vim.defer_fn(function()
    if not done then
      finish("Request timed out", nil)
    end
  end, 5000)
end

return M
