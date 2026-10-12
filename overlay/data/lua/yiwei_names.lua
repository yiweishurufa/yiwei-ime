-- 一维输入法 · 人名候选（W6）：打「姓 + 名」的全拼（2–4 个音节，如 liyiwei、ouyangxue），
-- 词库里没有整词（首选是拼出来的句子）时，在第 4 位后面补最多 3 个人名候选（注释「人名」）；已有的不重复。
-- 关闭：用户目录 yiwei/names.disabled（设置 → 输入 里的开关会写它）。
local flags = require("yiwei_flags")
local ok, D = pcall(require, "yiwei_names_data")
if not ok then D = nil end

local F = {}
local INSERT_AT = 4   -- 前 4 个候选不动
local MAX_NAMES = 3

-- 把输入切成拼音音节（所有切法，最多 4 个音节）
local function splits(s)
  local out = {}
  local function go(pos, acc)
    if #out >= 24 then return end
    if pos > #s then
      if #acc >= 2 then out[#out + 1] = { table.unpack(acc) } end
      return
    end
    if #acc >= 4 then return end
    for len = math.min(6, #s - pos + 1), 1, -1 do
      local syl = s:sub(pos, pos + len - 1)
      if D.syllables[syl] or (#acc == 0 and D.surnames[syl]) then
        acc[#acc + 1] = syl
        go(pos + len, acc)
        acc[#acc] = nil
      end
    end
  end
  go(1, {})
  return out
end

-- 每种切法：姓（单姓或复姓）+ 1–2 个名字音节，按「姓的排名 + 各字排名」打分
local function names_for(input)
  local scored = {}
  for _, sp in ipairs(splits(input)) do
    local sur = D.surnames[sp[1]]
    local rest = #sp - 1
    if sur and rest >= 1 and rest <= 2 then
      local g1 = D.given[sp[2]]
      local g2 = rest == 2 and D.given[sp[3]] or nil
      if g1 and (rest == 1 or g2) then
        for si = 1, math.min(2, #sur) do
          local base = si * 3 - (utf8.len(sur[si]) == 2 and 4 or 0) -- 复姓（欧阳、司马…）优先于拆成单姓
          for a = 1, math.min(3, #g1) do
            if rest == 1 then
              scored[#scored + 1] = { sur[si] .. g1[a], base + a * 2 }
            else
              for b = 1, math.min(3, #g2) do
                if g1[a] ~= g2[b] then
                  scored[#scored + 1] = { sur[si] .. g1[a] .. g2[b], base + a + b }
                end
              end
            end
          end
        end
      end
    end
  end
  table.sort(scored, function(x, y) return x[2] < y[2] end)
  return scored
end

function F.func(input, env)
  local ctx = env.engine.context
  local code = ctx.input or ""
  if D == nil or #code < 3 or #code > 18 or not code:match("^[a-z]+$") or flags.exists("names.disabled") then
    for cand in input:iter() do yield(cand) end
    return
  end
  local names = names_for(code)
  if #names == 0 then
    for cand in input:iter() do yield(cand) end
    return
  end
  local seen, n, done = {}, 0, false
  local start, stop
  local function emit_names()
    done = true
    if not start or start ~= 0 or stop ~= #code then return end
    local added = 0
    for _, s in ipairs(names) do
      if added >= MAX_NAMES then break end
      if not seen[s[1]] then
        seen[s[1]] = true
        yield(Candidate("yiwei_name", start, stop, s[1], "人名"))
        added = added + 1
      end
    end
  end
  local held = {}
  for cand in input:iter() do
    n = n + 1
    if n == 1 then
      start, stop = cand.start, cand._end
      -- 词库里有完整的词（不是拼出来的句子）就不加人名，避免干扰日常打字；三个字的除外（多半就是姓名）
      if cand:get_genuine().type ~= "sentence" and utf8.len(cand.text) ~= 3 then done = true end
    end
    if n <= INSERT_AT then
      seen[cand.text] = true
      yield(cand)
    else
      -- 先看后面一小段里有没有同名的词，避免重复
      if not done then
        held[#held + 1] = cand
        seen[cand.text] = true
        if #held >= 30 then
          emit_names()
          for _, c in ipairs(held) do yield(c) end
          held = nil
        end
      else
        yield(cand)
      end
    end
  end
  if not done then
    emit_names()
    for _, c in ipairs(held or {}) do yield(c) end
  end
end

return F
