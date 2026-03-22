-- lua/acc/config.lua
-- Default configuration. Override via require("acc").setup({ ... })

local M = {}

M.defaults = {
  -- ACC engine connection
  host = "localhost",
  port = 9339,

  -- Path to acc binary. nil = auto-managed (downloaded to data dir)
  server_path = nil,

  -- Path to lizard binary. nil = auto-detect from PATH
  lizard_path = nil,

  -- Engine startup timeout in ms
  startup_timeout = 5000,

  -- Engine CLI args overrides
  engine = {
    rpc_port          = 9339,
    remote_db         = false,
    remote_endpoint   = "localhost:8000/rpc",
    git_branch_naming = true,
    telemetry         = false,
    telemetry_endpoint = "localhost:4317",
  },

  -- AVEC weight overrides (only applied when override = true)
  avec_weights = {
    override = false,
    stability = {
      churn_weight                    = 0.4,
      contributor_weight              = 0.3,
      test_weight                     = 0.3,
      churn_normalize                 = 10,
      test_line_coverage_normalize    = 100.0,
      test_line_coverage_weight       = 0.5,
      test_branch_coverage_normalize  = 100.0,
      test_branch_coverage_weight     = 0.5,
      test_base_bias                  = 0.5,
      contributor_cap                 = 5,
    },
    logic = {
      complexity_weight = 0.7,
      parameter_weight  = 0.3,
      loc_divisor       = 10,
      parameter_cap     = 5,
    },
    friction = {
      centrality_weight             = 0.4,
      dependency_weight             = 0.6,
      churn_weight                  = 0.7,
      collaboration_normalize       = 0.3,
      structural_friction_weight    = 0.4,
      process_friction_weight       = 0.3,
      cognitive_friction_weight     = 0.3,
      cyclomatic_complexity_weight  = 20.0,
      git_contributors_normalize    = 10.0,
      git_total_commits_normalize   = 50.0,
      incoming_cap                  = 10,
    },
    autonomy = {
      file_number_blast_radius = 30,
      dependency_ratio         = 0.8,
      absolute_count           = 0.2,
    },
  },

  -- Keymaps (set to false to disable)
  keymaps = {
    build_graph    = "<leader>ab",
    search         = "<leader>as",
    stats          = "<leader>aS",
    high_friction  = "<leader>af",
    unstable       = "<leader>au",
  },
}

return M
