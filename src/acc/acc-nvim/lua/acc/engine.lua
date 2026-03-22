-- lua/acc/engine.lua
-- Spawns and manages the ACC engine process.

local M = {}

local _job_id = nil

-- Build the argument list from config (mirrors startAccEngine in extension.ts)
local function build_args(server_path, cfg)
  local e   = cfg.engine
  local cwd = vim.fn.getcwd()

  local args = {
    server_path,
    "--Acc:RepositoryPath",    cwd,
    "--JsonRpc:Port",          tostring(e.rpc_port or 9339),
    "--SurrealDb:Remote",      tostring(e.remote_db or false),
    "--Acc:UseGitBranchNaming", tostring(e.git_branch_naming ~= false),
  }

  if e.remote_db then
    vim.list_extend(args, {
      "--SurrealDb:Endpoints:Remote",
      e.remote_endpoint or "localhost:8000/rpc",
    })
  end

  if e.telemetry then
    vim.list_extend(args, {
      "--Acc:Telemetry:Enabled", "true",
      "--Acc:Telemetry:Endpoint", e.telemetry_endpoint or "localhost:4317",
    })
  end

  -- AVEC weight overrides
  local w = cfg.avec_weights
  if w and w.override then
    local function push(key, val)
      table.insert(args, key)
      table.insert(args, tostring(val))
    end

    local s = w.stability or {}
    push("--AvecWeights:Stability:ChurnWeight",                   s.churn_weight                    or 0.4)
    push("--AvecWeights:Stability:ContributorWeight",             s.contributor_weight              or 0.3)
    push("--AvecWeights:Stability:TestWeight",                    s.test_weight                     or 0.3)
    push("--AvecWeights:Stability:ChurnNormalize",                s.churn_normalize                 or 10)
    push("--AvecWeights:Stability:TestLineCoverageNormalize",     s.test_line_coverage_normalize    or 100.0)
    push("--AvecWeights:Stability:TestLineCoverageWeight",        s.test_line_coverage_weight       or 0.5)
    push("--AvecWeights:Stability:TestBranchCoverageNormalize",   s.test_branch_coverage_normalize  or 100.0)
    push("--AvecWeights:Stability:TestBranchCoverageWeight",      s.test_branch_coverage_weight     or 0.5)
    push("--AvecWeights:Stability:TestBaseBias",                  s.test_base_bias                  or 0.5)
    push("--AvecWeights:Stability:ContributorCap",                s.contributor_cap                 or 5)

    local l = w.logic or {}
    push("--AvecWeights:Logic:ComplexityWeight", l.complexity_weight or 0.7)
    push("--AvecWeights:Logic:ParameterWeight",  l.parameter_weight  or 0.3)
    push("--AvecWeights:Logic:LocDivisor",       l.loc_divisor       or 10)
    push("--AvecWeights:Logic:ParameterCap",     l.parameter_cap     or 5)

    local f = w.friction or {}
    push("--AvecWeights:Friction:CentralityWeight",            f.centrality_weight             or 0.4)
    push("--AvecWeights:Friction:DependencyWeight",            f.dependency_weight             or 0.6)
    push("--AvecWeights:Friction:ChurnWeight",                 f.churn_weight                  or 0.7)
    push("--AvecWeights:Friction:CollaborationNormalize",      f.collaboration_normalize       or 0.3)
    push("--AvecWeights:Friction:StructuralFrictionWeight",    f.structural_friction_weight    or 0.4)
    push("--AvecWeights:Friction:ProcessFrictionWeight",       f.process_friction_weight       or 0.3)
    push("--AvecWeights:Friction:CognitiveFrictionWeight",     f.cognitive_friction_weight     or 0.3)
    push("--AvecWeights:Friction:CyclomaticComplexityWeight",  f.cyclomatic_complexity_weight  or 20.0)
    push("--AvecWeights:Friction:GitContributorsNormalize",    f.git_contributors_normalize    or 10.0)
    push("--AvecWeights:Friction:GitTotalCommitsNormalize",    f.git_total_commits_normalize   or 50.0)
    push("--AvecWeights:Friction:IncomingCap",                 f.incoming_cap                  or 10)

    local a = w.autonomy or {}
    push("--AvecWeights:Autonomy:FileNumberBlastRadius", a.file_number_blast_radius or 30)
    push("--AvecWeights:Autonomy:DependencyRatio",       a.dependency_ratio         or 0.8)
    push("--AvecWeights:Autonomy:AbsoluteCount",         a.absolute_count           or 0.2)
  end

  return args
end

-- Start the engine; calls cb() once ready (after startup_timeout)
function M.start(server_path, cfg, cb)
  if _job_id then
    vim.notify("ACC: Engine already running", vim.log.levels.DEBUG)
    cb()
    return
  end

  local args = build_args(server_path, cfg)
  -- args[1] is the binary; jobstart expects cmd as list
  local cmd  = args

  vim.notify("ACC: Starting engine...", vim.log.levels.INFO)

  _job_id = vim.fn.jobstart(cmd, {
    cwd        = vim.fn.getcwd(),
    detach     = true,
    on_stderr  = function(_, data)
      for _, line in ipairs(data or {}) do
        if line ~= "" then
          vim.notify("[ACC] " .. line, vim.log.levels.DEBUG)
        end
      end
    end,
    on_exit    = function(_, code)
      _job_id = nil
      vim.notify("ACC: Engine exited with code " .. code, vim.log.levels.WARN)
    end,
  })

  if _job_id <= 0 then
    vim.notify("ACC: Failed to start engine binary: " .. server_path, vim.log.levels.ERROR)
    return
  end

  -- Give the engine time to bind to its port
  vim.defer_fn(function()
    vim.notify("ACC: Engine ready", vim.log.levels.INFO)
    cb()
  end, cfg.startup_timeout or 5000)
end

function M.stop()
  if _job_id then
    vim.fn.jobstop(_job_id)
    _job_id = nil
  end
end

function M.is_running()
  return _job_id ~= nil
end

return M
