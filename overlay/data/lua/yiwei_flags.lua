-- 一维输入法 · 开关标记文件（用户目录 yiwei/ 下）。一维助手的设置页写这些文件，Lua 每 3 秒看一次。
-- 安卓（同文）没有助手：文件不存在就是默认值，也可以手动放文件。
local M = {}
local cache = {}

local function sep() return package.config:sub(1, 1) end

function M.dir()
  return rime_api.get_user_data_dir() .. sep() .. "yiwei" .. sep()
end

function M.path(name) return M.dir() .. name end

-- 文件内容（去掉首尾空白）；不存在返回 nil。3 秒内复用上次读到的结果。
function M.read(name)
  local now = os.time()
  local c = cache[name]
  if c and now - c.at < 3 then return c.v end
  local f = io.open(M.path(name), "r")
  local v
  if f then v = (f:read("*a") or ""):match("^%s*(.-)%s*$"); f:close() end
  cache[name] = { at = now, v = v }
  return v
end

function M.exists(name) return M.read(name) ~= nil end

-- 读完就删的一次性命令文件（例如 learn.clear）
function M.take(name)
  local p = M.path(name)
  local f = io.open(p, "r")
  if not f then return nil end
  local v = (f:read("*a") or ""):match("^%s*(.-)%s*$")
  f:close()
  os.remove(p)
  cache[name] = nil
  return v
end

function M.mkdir()
  -- yiwei/ 一般已由助手建好；安卓上可能没有
  local p = M.dir()
  local f = io.open(p .. ".probe", "w")
  if f then f:close(); os.remove(p .. ".probe"); return true end
  if sep() == "\\" then return false end -- Windows：助手建目录（os.execute 会闪黑窗）
  os.execute("mkdir -p '" .. p .. "'")
  return true
end

return M
