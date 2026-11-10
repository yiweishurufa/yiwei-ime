-- 一维输入法 · 拼音纠错（全拼）：手误打错一个字母时，在候选里补上「改一个字母就对」的词，注释「纠 正确拼音」。
-- 例：yinggua → 英国（a 打成了 o 旁边的元音）、shoujo → 手机。
--
-- 什么时候纠：输入 4–12 个小写字母，且前 8 个候选里没有「完整覆盖整段输入的词」（只有拼出来的句子或单字），
--   并且输入不是「能完整切成 3 个以上正常音节」的句子（见 F.func）。
--   有完整的词（如 diushi → 丢失）就一个字都不动，避免微信输入法 diushi 打不出「丢失」的老问题。
-- 怎么纠：对输入做 1 次编辑（替换 / 多打 / 漏打 / 相邻两字母颠倒；不改第 1 个字母），只留能完整切成拼音音节的串，
--   按编辑代价从小到大最多查 MAX_QUERIES 个；每个用不带用户词库、不造句的 script_translator@yiwei_correct 查整词，
--   打分 = ln(词频) - 代价 × COST_SCALE，取最好的 MAX_OUT 个。
-- 放哪：默认排在第 2 位起（首选不动）；yiwei/correct_first.enabled 存在时排第一（设置「纠错结果可排第一」）。
-- 关闭：yiwei/correct.disabled（设置 → 输入「拼音纠错」）。
-- 性能：音节表在加载时建好；按键时不读文件（开关文件由 yiwei_flags 每 3 秒看一次）；同一输入只算一次。
local flags = require("yiwei_flags")
local okd, D = pcall(require, "yiwei_names_data")
if not okd then D = nil end

local F = {}
local MIN_LEN, MAX_LEN = 4, 12 -- 更长的输入多半是整句，交给造句
local LOOKAHEAD = 8      -- 看前几个候选里有没有完整的词
local MAX_QUERIES = 12   -- 每次按键最多查几个纠正后的拼音串
local BUDGET = 0.0025    -- 每次按键最多花 2.5 ms（CPU 时间），到点就用已经查到的结果
local MAX_OUT = 3        -- 最多补几个纠错候选
local COST_SCALE = 2.3   -- 代价 1 ≈ 词频差 10 倍
local MIN_SCORE = -16    -- 太冷僻的纠正不要（ln 概率）

local VOWEL = { a = true, e = true, i = true, o = true, u = true, v = true }
-- QWERTY 相邻键（只算同一行左右和上下斜邻，够用）
local NEAR = {
  q = "wa", w = "qeas", e = "wrsd", r = "etdf", t = "ryfg", y = "tugh", u = "yihj", i = "uojk", o = "ipkl", p = "ol",
  a = "qwsz", s = "awedxz", d = "serfcx", f = "drtgvc", g = "ftyhbv", h = "gyujnb", j = "huikmn", k = "jiolm", l = "kop",
  z = "asx", x = "zsdc", c = "xdfv", v = "cfgb", b = "vghn", n = "bhjm", m = "njk",
}
local LETTERS = "abcdefghijklmnopqrstuvwxyz"

local SYL, PREFIX
local function build_syllables()
  if not D or not D.syllables then return false end
  SYL, PREFIX = {}, {}
  for s in pairs(D.syllables) do
    -- 不要 m、n、ng、hm 这类没有元音的叹词音节，纠出来全是噪音
    if s:find("[aeiouv]") and #s <= 6 then
      SYL[s] = true
      for i = 1, #s do PREFIX[s:sub(1, i)] = true end
    end
  end
  return true
end

-- 能否完整切成音节：返回最少「单个元音音节」(a/e/o) 的切法里单元音音节的个数和音节数；切不开返回 nil
local function split_cost(s)
  local n = #s
  local best = {}  -- best[i] = {lone, count} 从 i 开始的最好结果
  best[n + 1] = { 0, 0 }
  for i = n, 1, -1 do
    local b
    for j = i, math.min(n, i + 5) do
      local syl = s:sub(i, j)
      if not PREFIX[syl] then break end
      local rest = best[j + 1]
      if SYL[syl] and rest then
        local lone = rest[1] + (#syl == 1 and 1 or 0)
        local cnt = rest[2] + 1
        if not b or lone < b[1] or (lone == b[1] and cnt < b[2]) then b = { lone, cnt } end
      end
    end
    best[i] = b
  end
  local r = best[1]
  if r then return r[1], r[2] end
  return nil
end

local function sub_cost(a, b)
  if NEAR[a] and NEAR[a]:find(b, 1, true) then return 0.6 end
  if VOWEL[a] and VOWEL[b] then return 0.7 end
  return 1.0
end

-- 前缀/后缀能否切成音节：fwd[p] = code[1..p-1] 可切，bwd[k] = code[k..n] 可切
local function seg_tables(code)
  local n = #code
  local fwd, bwd = { [1] = true }, { [n + 1] = true }
  for p = 1, n do
    if fwd[p] then
      for q = p, math.min(n, p + 5) do
        local syl = code:sub(p, q)
        if not PREFIX[syl] then break end
        if SYL[syl] then fwd[q + 1] = true end
      end
    end
  end
  for k = n, 1, -1 do
    for q = k, math.min(n, k + 5) do
      local syl = code:sub(k, q)
      if not PREFIX[syl] then break end
      if SYL[syl] and bwd[q + 1] then bwd[k] = true; break end
    end
  end
  return fwd, bwd
end

-- 把 code[i..k-1] 换成 mid 之后整串能否切成音节：只在改动处附近做一小段动态规划，前后用 fwd/bwd
local function valid_edit(code, fwd, bwd, i, mid, k)
  local n = #code
  local ws = math.max(1, i - 6)
  local w = code:sub(ws, i - 1) .. mid .. code:sub(k, math.min(n, k + 6))
  local tstart = (i - ws) + #mid + 1 -- 窗口里第一个尾部字母的位置（对应原串 k）
  local reach = {}
  for j = 1, i - ws + 1 do
    if fwd[ws + j - 1] then reach[j] = true end
  end
  for j = 1, #w + 1 do
    if reach[j] then
      if j >= tstart and bwd[k + (j - tstart)] then return true end
      if j <= #w then
        for q = j, math.min(#w, j + 5) do
          local syl = w:sub(j, q)
          if not PREFIX[syl] then break end
          if SYL[syl] then reach[q + 1] = true end
        end
      end
    end
  end
  return false
end

-- 所有能切成音节的 1 次编辑：{串, 代价}，不动第 1 个字母
local function edits(code, deadline)
  local out, seen = {}, { [code] = true }
  local fwd, bwd = seg_tables(code)
  local n = #code
  local function try(i, mid, k, c)
    if valid_edit(code, fwd, bwd, i, mid, k) then
      local s = code:sub(1, i - 1) .. mid .. code:sub(k)
      if not seen[s] then seen[s] = true; out[#out + 1] = { s, c } end
    end
  end
  for i = 2, n do
    if os.clock() > deadline then return out end
    local a = code:sub(i, i)
    -- 多打一个字母；多打的 g（n/ng）最常见，代价低一些
    try(i, "", i + 1, (a == "g" and code:sub(i - 1, i - 1) == "n") and 0.7 or 0.9)
    for x = 1, 26 do
      local b = LETTERS:sub(x, x)
      if b ~= a then try(i, b, i + 1, sub_cost(a, b)) end
    end
    if i < n then
      local b = code:sub(i + 1, i + 1)
      if a ~= b then try(i, b .. a, i + 2, 0.6) end
    end
  end
  for i = 2, n + 1 do
    if os.clock() > deadline then return out end
    local prev = code:sub(i - 1, i - 1)
    for x = 1, 26 do
      local b = LETTERS:sub(x, x)
      try(i, b, i, (b == "g" and prev == "n") and 0.7 or 1.0)
    end
  end
  return out
end

-- 个人纠错规则：yiwei/corrections.txt，一行「错码 正码」（空格或 Tab 分隔，# 开头为注释），3 秒缓存。
-- 输入恰好等于错码时，把正码的整词补到第 2 位起（不看长度、不看有没有完整词），受 correct.disabled 开关管。
local rules, rules_at = {}, -10
local function load_rules()
  local now = os.time()
  if now - rules_at < 3 then return end
  rules_at = now
  rules = {}
  local sp = package.config:sub(1, 1)
  local f = io.open(rime_api.get_user_data_dir() .. sp .. "yiwei" .. sp .. "corrections.txt", "r")
  if not f then return end
  for line in f:lines() do
    local a, b = line:match("^%s*([a-z]+)[%s\t]+([a-z]+)%s*$")
    if a and b and a ~= b then rules[a] = b end
  end
  f:close()
end

local function personal_list(env, code, start)
  local target = rules[code]
  if not target then return nil end
  local seg = Segment(start, start + #target)
  seg.tags = Set({ "abc" })
  local ok, t = pcall(function() return env.tr:query(target, seg) end)
  local list = {}
  if ok and t then
    for c in t:iter() do
      if c._end ~= start + #target then break end
      if utf8.len(c.text) >= 1 then list[#list + 1] = { text = c.text, code = target } end
      if #list >= 2 then break end
    end
  end
  return list
end

function F.init(env)
  env.ok = build_syllables()
  if not env.ok then log.warning("yiwei_correct: 没有音节表，纠错不可用"); return end
  local ok, tr = pcall(function()
    return Component.ScriptTranslator(env.engine, env.engine.schema, "yiwei_correct", "script_translator")
  end)
  if ok and tr then env.tr = tr else env.ok = false; log.error("yiwei_correct: " .. tostring(tr)) end
  env.iq = env.engine.schema.config:get_double("yiwei_correct/initial_quality") or 0
  env.cache_code, env.cache = nil, nil
end

-- 纠错结果（按分数从高到低），同一输入复用
local function corrections(env, code, start)
  if env.cache_code == code then return env.cache end
  local function by_cost(x, y) if x[2] ~= y[2] then return x[2] < y[2] end return x[1] < y[1] end
  local deadline = os.clock() + BUDGET
  local all = edits(code, deadline)
  table.sort(all, by_cost)
  -- 只给排在前面的算完整切分：单独的 a/e/o 音节（yingbua → ying bu a）多半是凑出来的，加代价
  local cands = {}
  for i = 1, math.min(#all, MAX_QUERIES * 2) do
    local lone, cnt = split_cost(all[i][1])
    if lone and cnt >= 2 then cands[#cands + 1] = { all[i][1], all[i][2] + lone * 0.5 } end
  end
  table.sort(cands, by_cost)
  local best = {}
  for i = 1, math.min(#cands, MAX_QUERIES) do
    if os.clock() > deadline then break end
    local s, cost = cands[i][1], cands[i][2]
    local seg = Segment(start, start + #s)
    seg.tags = Set({ "abc" })
    local ok, t = pcall(function() return env.tr:query(s, seg) end)
    if ok and t then
      local got = 0
      for c in t:iter() do
        if c._end ~= start + #s then break end -- 只要整段的词，后面都是更短的
        if utf8.len(c.text) >= 2 then
          local p = c.quality - env.iq
          local score = math.log(p > 1e-12 and p or 1e-12) - cost * COST_SCALE
          if score >= MIN_SCORE and (not best[c.text] or best[c.text].score < score) then
            best[c.text] = { text = c.text, code = s, score = score }
          end
          got = got + 1
          if got >= 2 then break end
        end
      end
    end
  end
  local list = {}
  for _, v in pairs(best) do list[#list + 1] = v end
  table.sort(list, function(x, y) if x.score ~= y.score then return x.score > y.score end return x.text < y.text end)
  env.cache_code, env.cache = code, list
  return list
end

-- 候选的拼音是否都是「正常写法」：合法音节，或只打了声母的简拼（zh、b…）。
-- 雾凇的固定纠错规则（hso、ign…）凑出来的整词不算，例如 nihso → 你受（ni hso），这时仍然给出 你好。
local function plain_preedit(preedit)
  for piece in (preedit or ""):gmatch("[^%s']+") do
    if not SYL[piece] and not piece:match("^[bcdfghjklmnpqrstwxyz]h?$") then return false end
  end
  return true
end

function F.func(input, env)
  local ctx = env.engine.context
  local seg = ctx.composition:back()
  local code = seg and ctx.input:sub(seg.start + 1, seg._end) or ""
  load_rules()
  if not env.ok or not code:match("^[a-z]+$") or (not rules[code] and (#code < MIN_LEN or #code > MAX_LEN))
      or not seg:has_tag("abc") or flags.exists("correct.disabled") then
    for cand in input:iter() do yield(cand) end
    return
  end
  -- 先看前几个候选
  local held = {}
  local whole = false
  for cand in input:iter() do
    held[#held + 1] = cand
    local g = cand:get_genuine()
    if cand._end == seg._end and cand.start == seg.start and g.type ~= "sentence" and plain_preedit(g.preedit) then
      whole = true
      break
    end
    if #held >= LOOKAHEAD then break end
  end
  local list = {}
  load_rules()
  -- 输入本身能完整切成正常音节（没有凑出来的单独 a/e/o）时，多半是用户就想这么打：
  --   3 个音节以上（tazhule、douguoqile、dianfanle）是句子，交给造句，不纠；
  --   1–2 个音节（yinggua → ying gua）照纠（默认排第 2 位起）。
  -- 2026-11-10 体检：2000 句里 20 句正确输入被补了纠错候选，开「可排第一」时 13 句首选被改坏（他住了 → 他输了）。
  local lone, cnt = split_cost(code)
  local valid = lone == 0
  local pl = rules[code] and #held > 0 and personal_list(env, code, seg.start) or nil
  if pl and #pl > 0 then
    list = pl
  elseif not whole and #held > 0 and not (valid and cnt >= 3) then
    local ok, r = pcall(corrections, env, code, seg.start)
    if ok then list = r else log.error("yiwei_correct: " .. tostring(r)) end
  end
  -- 和首选重复的纠错不要；和后面候选重复的，用纠错候选（整段上屏）代替原来那个
  local out, n, dup = {}, 0, {}
  local first_text = held[1] and held[1].text
  -- 首选已经是最好的纠正结果（雾凇的固定纠错规则已经纠对了，如 beijign → 北京），就不再补
  if list[1] and list[1].text == first_text then list = {} end
  for _, v in ipairs(list) do
    if n >= MAX_OUT then break end
    if v.text ~= first_text and not dup[v.text] then
      dup[v.text] = true
      n = n + 1
      out[n] = Candidate("yiwei_correct", seg.start, seg._end, v.text, "纠 " .. v.code)
    end
  end
  local first_ok = n > 0 and flags.exists("correct_first.enabled")
  if first_ok then for _, c in ipairs(out) do yield(c) end end
  for i, cand in ipairs(held) do
    if i == 1 or not dup[cand.text] then yield(cand) end
    if i == 1 and not first_ok then for _, c in ipairs(out) do yield(c) end end
  end
  for cand in input:iter() do
    if not dup[cand.text] then yield(cand) end
  end
end

return F
