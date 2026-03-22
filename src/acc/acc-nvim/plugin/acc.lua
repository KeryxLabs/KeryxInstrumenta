-- acc.nvim/plugin/acc.lua
-- Auto-loaded by Neovim. Delegates everything to the lua/acc module.

if vim.g.acc_loaded then return end
vim.g.acc_loaded = true

require("acc").setup()
