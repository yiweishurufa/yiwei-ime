-- 一维输入法 · 中英之间自动加空格：处理器（见 yiwei_pangu_core.lua）
local M = require("yiwei_pangu_core")
local enabled, recent, remember, needs_space = M.enabled, M.recent, M.remember, M.needs_space

local P = {}
function P.init(env)
  env.conn = env.engine.context.commit_notifier:connect(function(ctx) remember(ctx:get_commit_text()) end)
end
function P.fini(env) if env.conn then env.conn:disconnect() end end
function P.func(key, env)
  if key:release() then return 2 end
  local code = key.keycode
  local ctx = env.engine.context
  -- 光标可能挪走了：忘掉上一次上屏
  if code == 0xff0d or code == 0xff08 or code == 0xff1b or code == 0xff09 or (code >= 0xff50 and code <= 0xff58)
     or key:ctrl() or key:alt() or key:super() then
    if not ctx:is_composing() then M.last = "" end
    return 2
  end
  if not ctx:get_option("ascii_mode") or ctx:is_composing() or not enabled() then return 2 end
  if code < 0x20 or code > 0x7e then return 2 end
  local ch = string.char(code)
  if recent() and needs_space(M.last, ch) then
    env.engine:commit_text(" " .. ch)
    return 1
  end
  if code == 0x20 then M.last = "" else remember(ch) end
  return 2
end

return P
