-- 一维输入法 · 括号自动配对
-- 中文标点状态下、没有正在输入的拼音时，输入左括号/左引号直接上屏一对，
-- 再请一维助手把光标左移一格，停在两半中间：（|）【|】《|》「|」“|” ‘|’
-- 关闭方式：设置 → 输入 → 括号自动配对（存在 yiwei/autopair.disabled 即关闭）。
-- 终端、代码编辑器、远程桌面和游戏里不配对，以免与应用自带的配对打架。
local PIPE = "\\\\.\\pipe\\YiweiHelper.Commands"

local PAIRS = {
  [0x28] = "（）", -- (
  [0x3c] = "《》", -- <
  [0x5b] = "【】", -- [
  [0x7b] = "「」", -- {
  [0x22] = "“”",   -- "
  [0x27] = "‘’",   -- '
}

local SKIP = {}
for _, a in ipairs({
  "cmd.exe", "conhost.exe", "windowsterminal.exe", "openconsole.exe", "powershell.exe", "pwsh.exe",
  "wsl.exe", "code.exe", "cursor.exe", "windsurf.exe", "idea64.exe", "pycharm64.exe", "webstorm64.exe",
  "goland64.exe", "clion64.exe", "rider64.exe", "devenv.exe", "powershell_ise.exe", "sublime_text.exe",
  "notepad++.exe", "zed.exe", "mintty.exe", "alacritty.exe", "wezterm-gui.exe", "mstsc.exe",
  "vmconnect.exe", "parsecd.exe", "steam.exe", "wegame.exe",
}) do SKIP[a] = true end

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

function P.init(env)
  env.flag = rime_api.get_user_data_dir() .. sep() .. "yiwei" .. sep() .. "autopair.disabled"
end

function P.func(key, env)
  local pair = PAIRS[key.keycode]
  if not pair or key:release() or key:ctrl() or key:alt() or key:super() then return 2 end
  local ctx = env.engine.context
  if ctx:is_composing() or ctx:get_option("ascii_mode") or ctx:get_option("ascii_punct")
     or ctx:get_option("full_shape") then return 2 end
  local app = (ctx:get_property("client_app") or ""):lower()
  if SKIP[app] or disabled(env) then return 2 end
  env.engine:commit_text(pair)
  send("caret:left")
  return 1 -- kAccepted
end

return P
