-- 一维输入法 · 上屏后反悔重选（默认 Ctrl+Shift+Z，可改成 Ctrl+Backspace 或两个都用）
-- 刚用拼音上屏一段字（30 秒内、之后没再上屏别的），没有正在输入的拼音时按快捷键：
-- 请一维助手删掉刚上屏的字，再把原来的拼音重新敲回来，候选重新出现，可以改选别的词。
-- 关闭方式：设置 → 常规 → 反悔重选（存在 yiwei/regret.disabled 即关闭）。
-- 快捷键：yiwei/regret.key 里写 shiftz（默认，没有这个文件也是）/ backspace / both。
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

-- 读 yiwei/regret.key（3 秒缓存）：shiftz / backspace / both
local function keymode(env)
  local now = os.time()
  if env.kchecked and now - env.kchecked < 3 then return env.kmode end
  env.kchecked = now
  env.kmode = "shiftz"
  local f = io.open(env.keyfile, "r")
  if f then
    local v = (f:read("*l") or ""):gsub("%s+", ""):lower()
    f:close()
    if v == "backspace" or v == "both" then env.kmode = v end
  end
  return env.kmode
end

local function is_regret_key(key, env)
  if not key:ctrl() or key:alt() then return false end
  local mode = keymode(env)
  local kc = key.keycode
  if kc == 0xff08 and not key:shift() then return mode ~= "shiftz" end -- Control+BackSpace
  if (kc == 0x5a or kc == 0x7a) and key:shift() then return mode ~= "backspace" end -- Control+Shift+Z
  return false
end

function P.init(env)
  env.keyfile = rime_api.get_user_data_dir() .. sep() .. "yiwei" .. sep() .. "regret.key"
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
  if key:release() or not is_regret_key(key, env) then return 2 end
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
