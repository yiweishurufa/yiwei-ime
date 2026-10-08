-- 一维输入法：中/英切换时通知一维助手在光标旁弹出「中 / A」提示。
-- 通过命名管道 \\.\pipe\YiweiHelper.Commands 发送 "toast:中" 或 "toast:A"；
-- 助手没运行或管道忙时静默忽略，不影响打字。
local PIPE = "\\\\.\\pipe\\YiweiHelper.Commands"

local function send(msg)
  pcall(function()
    local f = io.open(PIPE, "wb")
    if f then f:write(msg); f:close() end
  end)
end

local P = {}

function P.init(env)
  local ctx = env.engine.context
  env.conn = ctx.option_update_notifier:connect(function(c, name)
    if name == "ascii_mode" then
      send(c:get_option("ascii_mode") and "toast:A" or "toast:中")
    end
  end)
end

function P.fini(env)
  if env.conn then env.conn:disconnect() end
end

function P.func(key, env)
  return 2 -- kNoop：只监听，不处理按键
end

return P
