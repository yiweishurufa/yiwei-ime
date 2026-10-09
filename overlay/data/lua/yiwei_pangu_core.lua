-- 一维输入法 · 中英之间自动加空格（盘古之白）
-- 打开方式：设置 → 常规 → 中英之间自动加空格（存在 yiwei/pangu.enabled 即开启，默认关闭）。
--   * filter：候选上屏前，如果上一次上屏以英文/数字结尾而候选以汉字开头（或反过来），在候选前补一个空格。
--   * processor：英文状态下直接敲字母/数字时，如果上一次上屏以汉字结尾，先补一个空格。
-- 只记得最近 8 秒内、且中间没按过回车/方向键/退格等键的上一次上屏，避免光标挪走后乱加空格。
local M = { last = "", at = 0 }  -- 处理器和过滤器共用（require 只加载一次）

local function sep() return package.config:sub(1, 1) end

local flag
local checked, on = 0, false
local function enabled()
  local now = os.time()
  if now - checked >= 3 then
    checked = now
    if not flag then flag = rime_api.get_user_data_dir() .. sep() .. "yiwei" .. sep() .. "pangu.enabled" end
    local f = io.open(flag, "r")
    on = f ~= nil
    if f then f:close() end
  end
  return on
end

local function first_cp(s) return utf8.codepoint(s, 1) end
local function last_cp(s)
  local cp
  for _, c in utf8.codes(s) do cp = c end
  return cp
end
local function cjk(cp)
  return cp and ((cp >= 0x4E00 and cp <= 0x9FFF) or (cp >= 0x3400 and cp <= 0x4DBF) or (cp >= 0x20000 and cp <= 0x3134F))
end
local function word(cp)
  return cp and ((cp >= 0x30 and cp <= 0x39) or (cp >= 0x41 and cp <= 0x5A) or (cp >= 0x61 and cp <= 0x7A))
end

local function recent() return M.last ~= "" and os.time() - M.at <= 8 end
local function remember(text)
  if text and text ~= "" then M.last = text; M.at = os.time() end
end
local function needs_space(before, after)
  local a, b = last_cp(before), first_cp(after)
  return (word(a) and cjk(b)) or (cjk(a) and word(b))
end

M.enabled, M.recent, M.remember, M.needs_space = enabled, recent, remember, needs_space
return M
