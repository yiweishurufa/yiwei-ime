-- 一维输入法 · 输入统计
-- 只有用户在「一维输入法设置 → 统计」里开启后（存在 yiwei/stats.enabled）才记录。
-- 记录只写在本机用户文件夹 yiwei/stats-YYYY-MM.tsv：时间戳<TAB>上屏文字。
-- 另外记两类「选词不顺」的词（给维护报告和候选排序调优用），写 yiwei/typo-YYYY-MM.tsv：
--   paged<TAB>词              翻到第 2 页以后才选到的词
--   retype<TAB>删掉的字<TAB>重打的字   刚上屏的词马上按退格删掉、又重新打了别的
-- 只记 1–8 个汉字的词，不记时间、不记前后文、不记整句；同样受统计开关和无痕窗口限制。
-- 首选命中率（统计页「本周首选命中率」）：每次用拼音选词上屏，在 yiwei/pick-YYYY-MM.tsv 记一行「日期<TAB>1 或 0」
-- （1 = 每一段都选的第一个候选），不含任何文字。
local M = {}

local RETYPE_WINDOW = 30 -- 上屏后 30 秒内开始退格才算「重打」
local MAX_WORD = 8

local function sep()
  return package.config:sub(1, 1)
end

local function ulen(s)
  local ok, n = pcall(utf8.len, s)
  if ok and n then return n end
  return nil
end

-- 1–8 个汉字（CJK 统一表意文字及扩展 A），其它一律不记
local function is_word(s)
  if not s or s == "" then return false end
  local n = 0
  local ok = pcall(function()
    for _, cp in utf8.codes(s) do
      if cp < 0x3400 or cp > 0x9FFF then error("x") end
      n = n + 1
    end
  end)
  return ok and n >= 1 and n <= MAX_WORD
end

-- 最后 k 个字
local function tail(s, k)
  local n = ulen(s)
  if not n or k > n then return nil end
  if k == n then return s end
  return s:sub(utf8.offset(s, n - k + 1))
end

local function enabled(env)
  local f = io.open(env.yiwei_flag, "r")
  if not f then return false end
  f:close()
  -- 浏览器无痕 / InPrivate 窗口里不统计（一维助手写的标记）
  local p = io.open(env.yiwei_dir .. sep() .. "private.now", "r")
  if p then p:close(); return false end
  return true
end

local function append(env, prefix, line)
  local path = env.yiwei_dir .. sep() .. prefix .. "-" .. os.date("%Y-%m") .. ".tsv"
  local out = io.open(path, "a")
  if out then
    out:write(line, "\n")
    out:close()
  end
end

-- 本次上屏是否每一段都选的第一个候选（nil = 没有可判断的段）
local function all_first(ctx)
  local seen, first = false, true
  pcall(function()
    for _, seg in ipairs(ctx.composition:toSegmentation():get_segments()) do
      if seg.selected_index then
        seen = true
        if seg.selected_index > 0 then first = false end
      end
    end
  end)
  if not seen then return nil end
  return first
end

-- 本次上屏里翻页才选到的词（每段的选中序号 >= 每页候选数）
local function paged_words(ctx, page_size)
  local words = {}
  pcall(function()
    for _, seg in ipairs(ctx.composition:toSegmentation():get_segments()) do
      if seg.selected_index and seg.selected_index >= page_size then
        local cand = seg:get_selected_candidate()
        if cand and is_word(cand.text) then table.insert(words, cand.text) end
      end
    end
  end)
  return words
end

function M.init(env)
  local dir = rime_api.get_user_data_dir() .. sep() .. "yiwei"
  env.yiwei_dir = dir
  env.yiwei_flag = dir .. sep() .. "stats.enabled"
  env.page_size = (env.engine.schema and env.engine.schema.page_size) or 5
  if env.page_size < 1 then env.page_size = 5 end
  env.yiwei_conn = env.engine.context.commit_notifier:connect(function(ctx)
    local text = ctx:get_commit_text()
    local last, bs = env.last_word, env.bs or 0
    env.last_word, env.bs = nil, 0
    if not text or text == "" then return end
    if not enabled(env) then return end
    append(env, "stats", tostring(os.time()) .. "\t" .. (text:gsub("[\t\r\n]", " ")))

    local input = ctx.input or ""
    if input:match("^[a-z]") and text ~= input then
      local f = all_first(ctx)
      if f ~= nil then append(env, "pick", os.date("%Y-%m-%d") .. "\t" .. (f and "1" or "0")) end
    end
    for _, w in ipairs(paged_words(ctx, env.page_size)) do
      append(env, "typo", "paged\t" .. w)
    end
    if last and bs > 0 and os.time() - env.last_at <= RETYPE_WINDOW then
      local gone = tail(last, bs)
      if gone and is_word(text) then append(env, "typo", "retype\t" .. gone .. "\t" .. text) end
    end
    -- 只有用拼音打出来的汉字词才可能被「退格重打」
    if is_word(text) and input:match("^[a-z]") then
      env.last_word, env.last_at = text, os.time()
    end
  end)
end

function M.fini(env)
  if env.yiwei_conn then env.yiwei_conn:disconnect() end
end

-- 只观察按键，不拦截：没在输入时按退格就数一下；按了别的键（方向键、回车、Ctrl+…）就不算重打
function M.func(key, env)
  if not env.last_word or key:release() then return 2 end
  if env.engine.context:is_composing() then return 2 end
  local kc = key.keycode
  if kc >= 0xffe1 and kc <= 0xffee then return 2 end -- 单独的修饰键
  if kc == 0xff08 and not key:ctrl() and not key:alt() then
    env.bs = (env.bs or 0) + 1
    return 2
  end
  if kc >= 0x61 and kc <= 0x7a and not key:ctrl() and not key:alt() then
    if (env.bs or 0) == 0 then env.last_word = nil end -- 没删就接着打：不是重打
    return 2
  end
  env.last_word, env.bs = nil, 0
  return 2
end

return M
