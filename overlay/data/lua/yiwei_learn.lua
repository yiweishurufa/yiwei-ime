-- 一维输入法 · 按习惯自动调候选（过滤器；Windows 与安卓共用）
--
-- 雾凇的用户词库（librime userdb）本来就会记住分几次选出来的整词（yuanyiwei 依次选 苑/祎/玮 → 苑祎玮），
-- 这里再加一层「习惯」：
--   * 每次上屏都记一笔（拼音 → 词，次数、最近时间）到 yiwei/learn.tsv（只追加，定期压缩）；
--   * 下次打同样的拼音，常选的词提到最前（词组选 1 次、单字选 2 次就算常选；学习强度可调），
--     第二、三常选的也排到前三；用户词库里没有（比如被清过或别的方案）时也能补出来；
--   * 置顶（Ctrl+T）优先于这里；无痕窗口（yiwei/private.now）不记。
-- 开关（一维助手 设置 → 输入 → 按习惯调整候选，或手动放文件到用户目录 yiwei/）：
--   learn.disabled   存在即关闭（不记、不调，只剩 RIME 自带的词频）
--   learn.level      1 低 / 2 中（默认）/ 3 高：多少次算常选、多久淡忘
--   learn.clear      一次性命令：all 清除全部；数字 N = 清除最近 N 秒（3600 一小时、86400 一天）。
--                    同时把这些词从 RIME 用户词库删掉（userdb 里标记删除，和 Ctrl+Delete 一样）。
local flags = require("yiwei_flags")

local LEVELS = {
  -- 词组几次算常选、单字几次、半衰期（天）
  ["1"] = { phrase = 2, char = 3, half = 14 },
  ["2"] = { phrase = 1, char = 2, half = 60 },
  ["3"] = { phrase = 1, char = 1, half = 365 },
}
local MAX_ENTRIES = 5000

local S = { map = {}, size = -1, lines = 0, entries = 0, loaded = nil }

local function log_path() return flags.path("learn.tsv") end

local function add(code, text, t, n)
  local m = S.map[code]
  if not m then m = {}; S.map[code] = m end
  local e = m[text]
  if not e then e = { n = 0, t = 0 }; m[text] = e; S.entries = S.entries + 1 end
  e.n = e.n + (n or 1)
  if t > e.t then e.t = t end
end

local function file_size(p)
  local f = io.open(p, "rb")
  if not f then return 0 end
  local s = f:seek("end")
  f:close()
  return s or 0
end

local function load()
  local p = log_path()
  local size = file_size(p)
  if S.loaded == p and size == S.size then return end
  S.map, S.lines, S.entries, S.loaded, S.size = {}, 0, 0, p, size
  local f = io.open(p, "r")
  if not f then return end
  for line in f:lines() do
    local code, text, t, n = line:match("^([^\t]+)\t([^\t]+)\t(%d+)\t?(%d*)$")
    if code then
      add(code, text, tonumber(t), tonumber(n) or 1)
      S.lines = S.lines + 1
    end
  end
  f:close()
end

local function rewrite()
  -- 压缩：每个（拼音, 词）一行；太多时丢掉最久没用的
  local all = {}
  for code, m in pairs(S.map) do
    for text, e in pairs(m) do all[#all + 1] = { code, text, e } end
  end
  table.sort(all, function(a, b) return a[3].t > b[3].t end)
  local f = io.open(log_path(), "w")
  if not f then return end
  S.map, S.entries = {}, 0
  for i, r in ipairs(all) do
    if i > MAX_ENTRIES then break end
    f:write(r[1], "\t", r[2], "\t", r[3].t, "\t", r[3].n, "\n")
    add(r[1], r[2], r[3].t, r[3].n)
  end
  f:close()
  S.lines = math.min(#all, MAX_ENTRIES)
  S.size = file_size(log_path())
end

local function record(code, text)
  load()
  local now = os.time()
  add(code, text, now)
  local f = io.open(log_path(), "a")
  if not f and flags.mkdir() then f = io.open(log_path(), "a") end
  if not f then return end
  f:write(code, "\t", text, "\t", now, "\n")
  f:close()
  S.lines = S.lines + 1
  S.size = file_size(log_path())
  if S.lines > S.entries * 2 + 200 or S.entries > MAX_ENTRIES * 1.2 then rewrite() end
end

local function level()
  return LEVELS[flags.read("learn.level") or "2"] or LEVELS["2"]
end

local function score(e, lv, now)
  local age = math.max(0, now - e.t) / 86400
  return e.n * 0.5 ^ (age / lv.half)
end

local function ulen(s)
  local ok, n = pcall(utf8.len, s)
  return (ok and n) or #s
end

-- 本拼音下的常选词，按分数从高到低（最多 3 个）
local function habits(code)
  local m = S.map[code]
  if not m then return nil end
  local lv, now = level(), os.time()
  local list = {}
  for text, e in pairs(m) do
    local need = ulen(text) == 1 and lv.char or lv.phrase
    -- 次数够了才算；分数随时间淡忘，但不会因为淡忘低于「刚好够」的门槛之下太多
    if e.n >= need and score(e, lv, now) >= need * 0.5 then
      list[#list + 1] = { text = text, s = score(e, lv, now), t = e.t }
    end
  end
  if #list == 0 then return nil end
  table.sort(list, function(a, b)
    if math.abs(a.s - b.s) < 0.01 then return a.t > b.t end
    return a.s > b.s
  end)
  while #list > 3 do table.remove(list) end
  return list
end

-- ---------- 清除（也从 RIME 用户词库删除） ----------

local function forget_userdb(env, victims)
  -- victims: set of code .. "\t" .. text，nil 表示全部
  if not Memory then return 0 end
  local ok, n = pcall(function()
    local mem = Memory(env.engine, env.engine.schema)
    local doomed = {}
    if mem:user_lookup("", true) then
      for e in mem:iter_user() do
        local code = (e.custom_code or ""):gsub("[ ']", "")
        if not victims or victims[code .. "\t" .. e.text] then doomed[#doomed + 1] = e end
      end
    end
    for _, e in ipairs(doomed) do mem:update_userdict(e, -1, "") end
    if mem.disconnect then mem:disconnect() end
    return #doomed
  end)
  return ok and n or 0
end

local function clear(env, how)
  load()
  local victims
  if how ~= "all" then
    local secs = tonumber(how)
    if not secs then return end
    local since = os.time() - secs
    victims = {}
    for code, m in pairs(S.map) do
      for text, e in pairs(m) do
        if e.t >= since then victims[code .. "\t" .. text] = true; m[text] = nil end
      end
    end
  else
    S.map = {}
  end
  S.entries = 0
  for _, m in pairs(S.map) do for _ in pairs(m) do S.entries = S.entries + 1 end end
  rewrite()
  forget_userdb(env, victims)
end

-- ---------- 过滤器 ----------

local F = {}

local function private()
  return flags.exists("private.now")
end

function F.init(env)
  load()
  env.conn = env.engine.context.commit_notifier:connect(function(ctx)
    if flags.exists("learn.disabled") or private() then return end
    local text = ctx:get_commit_text() or ""
    local input = (ctx.input or ""):gsub("'", "")
    if text == "" or text == input or not input:match("^[a-z;]+$") or #input > 40 then return end
    if not text:find("[\128-\255]") or text:find("[\t\n]") then return end -- 只记中文（含符号的整句也行）
    pcall(record, input, text)
  end)
end

function F.fini(env)
  if env.conn then env.conn:disconnect() end
end

function F.func(input, env)
  local cmd = flags.take("learn.clear")
  if cmd then pcall(clear, env, cmd) end
  if flags.exists("learn.disabled") then
    for cand in input:iter() do yield(cand) end
    return
  end
  local ctx = env.engine.context
  local seg = ctx.composition:back()
  local code = seg and ctx.input:sub(seg.start + 1, seg._end):gsub("'", "") or ""
  load()
  local want = code ~= "" and habits(code)
  if not want then
    for cand in input:iter() do yield(cand) end
    return
  end
  local rank = {}
  for i, w in ipairs(want) do rank[w.text] = i end
  local found, buffered, n = {}, {}, 0
  local full_end = seg._end
  for cand in input:iter() do
    n = n + 1
    local r = rank[cand.text]
    if r and not found[r] and cand._end == full_end then
      found[r] = cand
    else
      buffered[#buffered + 1] = cand
    end
    local all = true
    for i = 1, #want do if not found[i] then all = false end end
    if all or n >= 100 then break end
  end
  -- 第一常选放第一；其余常选不往后挪，只保证进前三
  local out = {}
  for i = 1, #want do
    local c = found[i]
    if not c and i == 1 then
      c = Candidate("yiwei_learn", seg.start, full_end, want[1].text, "")
      c.quality = 999
    end
    if c then out[#out + 1] = c end
  end
  local head = out[1]
  if head then yield(head) end
  local rest_slots = {}
  for i = 2, #out do rest_slots[#rest_slots + 1] = out[i] end
  local yielded = 1
  local bi = 1
  while yielded < 3 and (bi <= #buffered or #rest_slots > 0) do
    -- 原来排在前面的词先出，常选词至迟第 3 个出
    if #rest_slots > 0 and (bi > #buffered or yielded + #rest_slots >= 3) then
      yield(table.remove(rest_slots, 1))
    else
      yield(buffered[bi]); bi = bi + 1
    end
    yielded = yielded + 1
  end
  for _, c in ipairs(rest_slots) do yield(c) end
  for i = bi, #buffered do yield(buffered[i]) end
  for cand in input:iter() do
    local r = rank[cand.text]
    if not (r and cand._end == full_end and (found[r] or r == 1)) then yield(cand) end
  end
end

return F
