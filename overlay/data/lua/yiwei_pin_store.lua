-- 一维输入法 · 候选置顶 / 隐藏 共用的存储（yiwei/pins.tsv：P|H <TAB> 拼音 <TAB> 词）
local M = {}

local function sep() return package.config:sub(1, 1) end

M.version = 0
M.pins, M.hidden = {}, {}
local loaded_path

function M.path()
  return rime_api.get_user_data_dir() .. sep() .. "yiwei" .. sep() .. "pins.tsv"
end

function M.load()
  local p = M.path()
  if loaded_path == p then return end
  loaded_path = p
  M.pins, M.hidden = {}, {}
  local f = io.open(p, "r")
  if not f then return end
  for line in f:lines() do
    local kind, code, text = line:match("^([PH])\t([^\t]+)\t(.+)$")
    if kind == "P" then M.pins[code] = text
    elseif kind == "H" then
      M.hidden[code] = M.hidden[code] or {}
      M.hidden[code][text] = true
    end
  end
  f:close()
end

function M.save()
  local f = io.open(M.path(), "w")
  if not f then return end
  for code, text in pairs(M.pins) do f:write("P\t", code, "\t", text, "\n") end
  for code, set in pairs(M.hidden) do
    for text in pairs(set) do f:write("H\t", code, "\t", text, "\n") end
  end
  f:close()
  M.version = M.version + 1
end

-- the settings page edits the file too: reload when it says so (yiwei/pins.reload exists)
function M.maybe_reload()
  local flag = rime_api.get_user_data_dir() .. sep() .. "yiwei" .. sep() .. "pins.reload"
  local f = io.open(flag, "r")
  if f then f:close(); os.remove(flag); loaded_path = nil; M.load(); M.version = M.version + 1 end
end

return M
