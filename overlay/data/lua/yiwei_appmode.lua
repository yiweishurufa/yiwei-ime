-- 一维输入法 · 按程序记住中英状态（重启后也记得）
-- 小狼毫本来就让每个程序各用各的中英状态（「应用规则 → 所有应用共用中英文状态」关闭时），
-- 但程序关掉重开、或电脑重启后会回到默认。这里把每个程序最后的状态记在 yiwei/appmode.tsv，
-- 新会话第一次在这个程序里按键时恢复。存在 yiwei/appmode.disabled 时不起作用；游戏模式期间不记录。
local P = {}

local function sep() return package.config:sub(1, 1) end

local function exists(path)
  local f = io.open(path, "r")
  if f then f:close(); return true end
  return false
end

local function load(env)
  local m = {}
  local f = io.open(env.file, "r")
  if f then
    for line in f:lines() do
      local app, v = line:match("^([^\t]+)\t([01])$")
      if app then m[app] = (v == "1") end
    end
    f:close()
  end
  return m
end

local function save(env)
  local f = io.open(env.file, "w")
  if not f then return end
  local n = 0
  for app, v in pairs(env.modes) do
    if n >= 300 then break end
    f:write(app, "\t", v and "1" or "0", "\n")
    n = n + 1
  end
  f:close()
end

function P.init(env)
  local dir = rime_api.get_user_data_dir() .. sep() .. "yiwei" .. sep()
  env.file = dir .. "appmode.tsv"
  env.off_flag = dir .. "appmode.disabled"
  env.game_flag = dir .. "game.now"
  env.modes = load(env)
  local ctx = env.engine.context
  env.conn = ctx.option_update_notifier:connect(function(c, name)
    if name ~= "ascii_mode" or not env.app or env.app == "" or env.restoring then return end
    if exists(env.game_flag) or exists(env.off_flag) then return end
    local v = c:get_option("ascii_mode")
    if env.modes[env.app] ~= v then
      env.modes[env.app] = v
      save(env)
    end
  end)
end

function P.fini(env)
  if env.conn then env.conn:disconnect() end
end

function P.func(key, env)
  local ctx = env.engine.context
  local app = (ctx:get_property("client_app") or ""):lower()
  if app ~= env.app then
    env.app = app
    local want = env.modes[app]
    if app ~= "" and want ~= nil and not ctx:is_composing() and ctx:get_option("ascii_mode") ~= want
       and not exists(env.off_flag) and not exists(env.game_flag) then
      env.restoring = true
      ctx:set_option("ascii_mode", want)
      env.restoring = false
    end
  end
  return 2
end

return P
