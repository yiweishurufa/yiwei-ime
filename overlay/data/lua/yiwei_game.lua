-- 一维输入法 · 游戏模式
-- 一维助手发现前台是全屏游戏时写 yiwei/game.now：
--   · 自动切到英文（ascii_mode），按键全部直接交给游戏，候选框不会出现；
--   · Shift 直接交给游戏（返回 kRejected：不再触发中英切换，游戏照样收到 Shift）。
-- 标记消失（离开游戏）后，如果是我们切的英文，就切回中文。
local P = {}

local function sep() return package.config:sub(1, 1) end

local function gaming(env)
  local now = os.clock()
  if env.checked and now - env.checked < 0.5 then return env.on end
  env.checked = now
  local f = io.open(env.flag, "r")
  env.on = f ~= nil
  if f then f:close() end
  return env.on
end

function P.init(env)
  env.flag = rime_api.get_user_data_dir() .. sep() .. "yiwei" .. sep() .. "game.now"
end

function P.func(key, env)
  local ctx = env.engine.context
  if gaming(env) then
    if not ctx:get_option("ascii_mode") then
      ctx:clear()
      ctx:set_option("ascii_mode", true)
      env.forced = true
    end
    local code = key.keycode
    if code == 0xffe1 or code == 0xffe2 then return 0 end -- Shift_L / Shift_R: kRejected
    return 2
  end
  if env.forced then
    env.forced = false
    if ctx:get_option("ascii_mode") then ctx:set_option("ascii_mode", false) end
  end
  return 2
end

return P
