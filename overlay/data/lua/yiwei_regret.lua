-- 一维输入法 · 上屏后反悔重选（Ctrl+Backspace）
-- 刚用拼音上屏一段字（30 秒内、之后没再上屏别的），没有正在输入的拼音时按 Ctrl+Backspace：
-- 请一维助手删掉刚上屏的字，再把原来的拼音重新敲回来，候选重新出现，可以改选别的词。
-- 关闭方式：设置 → 常规 → 反悔重选（存在 yiwei/regret.disabled 即关闭）。
local PIPE = "\\\\.\\pipe\\YiweiHelper.Commands"
local WINDOW = 30

local P = {}

local function sep() return package.config:sub(1, 1) end

local function disabled(env)
  local now = os.time()
  if env.checked and now - env.checked < 3 then return env.off end
  env.checked = now
  local f = io.open(env.flag, "r")
  env.off = f ~= nil
  if f then f:close() end
  return env.off
end

local function send(msg)
  pcall(function()
    local f = io.open(PIPE, "wb")
    if f then f:write(msg); f:close() end
  end)
end

local function ulen(s)
  local ok, n = pcall(utf8.len, s)
  if ok and n then return n end
  local c = 0
  for _ in s:gmatch("[^\128-\191]") do c = c + 1 end
  return c
end

function P.init(env)
  env.flag = rime_api.get_user_data_dir() .. sep() .. "yiwei" .. sep() .. "regret.disabled"
  local ctx = env.engine.context
  env.conn = ctx.commit_notifier:connect(function(c)
    local text = c:get_commit_text() or ""
    local input = c.input or ""
    if text ~= "" and input:match("^[a-z][a-z']*$") and text ~= input then
      env.last_text, env.last_input, env.last_at = text, input, os.time()
    else
      env.last_text = nil -- something else was committed: nothing to take back
    end
  end)
end

function P.fini(env)
  if env.conn then env.conn:disconnect() end
end

function P.func(key, env)
  if key:release() or not key:ctrl() or key:alt() or key:shift() then return 2 end
  if key:repr() ~= "Control+BackSpace" then return 2 end
  local ctx = env.engine.context
  if ctx:is_composing() or not env.last_text or os.time() - env.last_at > WINDOW then return 2 end
  if disabled(env) then return 2 end
  local app = (ctx:get_property("client_app") or ""):lower()
  if app == "mstsc.exe" or app == "vmconnect.exe" then return 2 end
  send("regret:" .. ulen(env.last_text) .. ":" .. env.last_input)
  env.last_text = nil
  return 1 -- kAccepted: the app never sees this Ctrl+Backspace
end

return P
