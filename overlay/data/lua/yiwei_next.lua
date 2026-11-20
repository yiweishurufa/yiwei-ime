-- 一维输入法 · 下一词联想（本机二元词频，默认关）。
-- 记下「上一次上屏的词 → 紧接着上屏的词」（15 秒内连着打才算，只存本机 yiwei/bigram.tsv）。
-- 之后刚上屏了某个词、接着打拼音时，如果前 8 个候选里有「以前常跟在它后面」的词：
-- 出现 2 次以上排第 2 位，4 次以上排第 1 位，注释〔联想〕。其它键照常打字，不拦截任何按键。
-- 开启：yiwei/next.enabled（设置 → 词库 →「下一词联想」）；清空学到的：删除 yiwei/bigram.tsv。
local flags = require("yiwei_flags")

local F = {}
local GAP = 15          -- 两次上屏间隔秒数上限
local LOOK = 8          -- 只在前几个候选里找
local MAXFILE = 2000000 -- bigram.tsv 超过 2MB 时合并旧记录（丢掉只出现 1 次的）再追加

-- 文件过大时合并成「前词\t后词\t次数」并丢掉只出现 1 次的，之后继续学。
local function compact(env)
  local lines, n = {}, 0
  for a, row in pairs(env.big) do
    for b, c in pairs(row) do
      if c >= 2 then
        local s = a .. "\t" .. b .. "\t" .. c .. "\n"
        lines[#lines + 1] = s; n = n + #s
      else row[b] = nil end
    end
  end
  local f = io.open(flags.path("bigram.tsv"), "w")
  if f then f:write(table.concat(lines)); f:close(); env.size = n end
end

local function isword(t)
  return t ~= "" and t:find("[\228-\233]") ~= nil and #t <= 24
end

function F.init(env)
  env.big = {}   -- big[prev][next] = n
  env.last = nil -- { text, at }
  env.size = 0
  local f = io.open(flags.path("bigram.tsv"), "r")
  if f then
    for line in f:lines() do
      local a, b, c = line:match("^([^\t]+)\t([^\t]+)\t?(%d*)$")
      if a then
        local row = env.big[a]; if not row then row = {}; env.big[a] = row end
        row[b] = (row[b] or 0) + (tonumber(c) or 1)
      end
      env.size = env.size + #line + 1
    end
    f:close()
  end
  env.conn = env.engine.context.commit_notifier:connect(function(ctx)
    local ok, err = pcall(function()
      local t = ctx:get_commit_text() or ""
      if not isword(t) then env.last = nil return end
      local now = os.time()
      local l = env.last
      if l and now - l.at <= GAP and flags.exists("next.enabled") then
        local row = env.big[l.text]; if not row then row = {}; env.big[l.text] = row end
        row[t] = (row[t] or 0) + 1
        if env.size >= MAXFILE then compact(env) end
        if env.size < MAXFILE then
          local f = io.open(flags.path("bigram.tsv"), "a")
          if f then
            local s = l.text .. "\t" .. t .. "\n"
            f:write(s); f:close(); env.size = env.size + #s
          end
        end
      end
      env.last = { text = t, at = now }
    end)
    if not ok then log.error("yiwei_next: " .. tostring(err)) end
  end)
end

function F.fini(env)
  if env.conn then env.conn:disconnect() end
end

function F.func(input, env)
  local l = env.last
  local ctx = env.engine.context
  local seg = ctx.composition:back()
  local row = l and seg and seg.start == 0 and os.time() - l.at <= GAP
    and flags.exists("next.enabled") and env.big[l.text]
  if not row then
    for cand in input:iter() do yield(cand) end
    return
  end
  local head, best, bn = {}, nil, 0
  local i = 0
  for cand in input:iter() do
    i = i + 1
    if i <= LOOK then
      local n = row[cand.text]
      if n and n > bn then best, bn = cand, n end
      head[#head + 1] = cand
    else
      if i == LOOK + 1 then
        -- 先把前 LOOK 个按需调序输出
        local out = {}
        for _, c in ipairs(head) do if c ~= best or bn < 2 then out[#out + 1] = c end end
        if best and bn >= 2 then
          table.insert(out, bn >= 4 and 1 or math.min(2, #out + 1), best)
        end
        for _, c in ipairs(out) do
          if c == best and bn >= 2 then
            yield(Candidate("yiwei_next", c.start, c._end, c.text, "〔联想〕"))
          else yield(c) end
        end
        head = nil
      end
      yield(cand)
    end
  end
  if head then
    local out = {}
    for _, c in ipairs(head) do if c ~= best or bn < 2 then out[#out + 1] = c end end
    if best and bn >= 2 then
      table.insert(out, bn >= 4 and 1 or math.min(2, #out + 1), best)
    end
    for _, c in ipairs(out) do
      if c == best and bn >= 2 then
        yield(Candidate("yiwei_next", c.start, c._end, c.text, "〔联想〕"))
      else yield(c) end
    end
  end
end

return F
