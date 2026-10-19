-- 一维输入法：中/英切换时通知一维助手在光标旁弹出「中 / A」提示。
-- 通过命名管道 \\.\pipe\YiweiHelper.Commands 发送：
--   "toast:中" / "toast:A"：用户自己按键切换（Shift、Ctrl、CapsLock 等），助手弹提示并同步托盘状态；
--   "mode:中" / "mode:A"：程序自动切换（切到设了自动英文的程序、恢复上次状态、游戏模式、助手命令），
--                       助手只同步托盘和隐私状态，不弹提示。
-- 以前所有切换都弹，切到 PowerShell 等程序时会在窗口中间乱弹「A」（用户 2026-10-19 反馈）。
-- 助手没运行或管道忙时静默忽略，不影响打字。
local PIPE = "\\\\.\\pipe\\YiweiHelper.Commands"

local function send(msg)
  pcall(function()
    local f = io.open(PIPE, "wb")
    if f then f:write(msg); f:close() end
  end)
end

-- 可能切换中英的按键：Shift / Ctrl / CapsLock / Alt（单按 Alt 切换由助手转成轻按 Shift）
local TOGGLE_KEYS = {
  [0xffe1] = true, [0xffe2] = true, -- Shift_L / Shift_R
  [0xffe3] = true, [0xffe4] = true, -- Control_L / Control_R
  [0xffe5] = true,                  -- Caps_Lock
  [0xffe9] = true, [0xffea] = true, -- Alt_L / Alt_R
}

local function toggle_key(key)
  if TOGGLE_KEYS[key.keycode] then return true end
  -- Ctrl+空格、Ctrl+. 之类带 Ctrl 的切换热键
  return key:ctrl() and not key:release()
end

local P = {}

function P.init(env)
  local ctx = env.engine.context
  env.armed = false
  env.armed_at = 0
  env.conn = ctx.option_update_notifier:connect(function(c, name)
    if name ~= "ascii_mode" then return end
    local m = c:get_option("ascii_mode") and "A" or "中"
    -- 同一次（或紧接着的）按键里发生的切换才算用户手动切换
    if env.armed and os.time() - env.armed_at <= 1 then
      env.armed = false
      send("toast:" .. m)
    else
      send("mode:" .. m)
    end
  end)
end

function P.fini(env)
  if env.conn then env.conn:disconnect() end
end

function P.func(key, env)
  -- 排在其它一维处理器之前：每次按键先重置，后面的自动恢复 / 游戏模式改动不会被当成手动切换
  env.armed = toggle_key(key)
  if env.armed then env.armed_at = os.time() end
  return 2 -- kNoop：只监听，不处理按键
end

return P
