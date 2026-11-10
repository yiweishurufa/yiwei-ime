-- 一维输入法 · 诗句联想（全拼）。
-- 1) 打出诗句上句（chuangqianmingyueguang → 床前明月光）时，首选不动，紧跟着补一个「上句，下句」的候选，
--    注释〔诗·李白《静夜思》〕；选它就整联上屏。
-- 2) 刚上屏了一句诗的上句（60 秒内），接着打下句的首字母（ysdss），下句排第一，注释〔下句〕。
-- 数据：yiwei_poetry_data.lua（scripts/data_gen/lexicon/build.py 由 chinese-poetry（MIT）生成：唐诗三百首、宋词三百首、千家诗、诗经）。
-- 关闭：yiwei/poetry.disabled（设置 → 词库 →「诗句联想」）。
-- 性能：表在 init 里加载一次（Lua 模块缓存，所有会话共用）；按键时只做几次查表，不读文件。
local flags = require("yiwei_flags")
local okd, DATA = pcall(require, "yiwei_poetry_data")
if not okd then DATA = nil end

local F = {}
local LOOKAHEAD = 3     -- 只看前几个候选是不是诗句
local NEXT_TTL = 60     -- 上句上屏后多少秒内认下句简拼

function F.init(env)
  env.ok = DATA ~= nil
  if not env.ok then log.warning("yiwei_poetry: 没有诗句表") return end
  env.next = nil -- { text, abbr, at }
  env.conn = env.engine.context.commit_notifier:connect(function(ctx)
    local ok, err = pcall(function()
      local t = ctx:get_commit_text() or ""
      local row = DATA[t]
      if row then
        env.next = { text = row[1], abbr = row[4], at = os.time() }
      elseif env.next and t ~= "" then
        env.next = nil
      end
    end)
    if not ok then log.error("yiwei_poetry: " .. tostring(err)) end
  end)
end

function F.fini(env)
  if env.conn then env.conn:disconnect() end
end

local function comment(row)
  local a, t = row[2] or "", row[3] or ""
  if a == "" then return "〔诗〕" end
  if t == "" or t == a then return "〔诗·" .. a .. "〕" end
  return "〔诗·" .. a .. "《" .. t .. "》〕"
end

function F.func(input, env)
  if not env.ok or flags.exists("poetry.disabled") then
    for cand in input:iter() do yield(cand) end
    return
  end
  local ctx = env.engine.context
  local seg = ctx.composition:back()
  if not seg then
    for cand in input:iter() do yield(cand) end
    return
  end
  local code = seg and ctx.input:sub(seg.start + 1, seg._end) or ""
  -- 2) 下句简拼
  local nx = env.next
  if nx and seg.start == 0 and code == nx.abbr and os.time() - nx.at <= NEXT_TTL then
    yield(Candidate("yiwei_poetry", seg.start, seg._end, nx.text, "〔下句〕"))
    for cand in input:iter() do
      if cand.text ~= nx.text then yield(cand) end
    end
    return
  end
  -- 1) 上句 → 补整联
  local i = 0
  local done = false
  for cand in input:iter() do
    yield(cand)
    if not done then
      i = i + 1
      local row = cand._end == seg._end and cand.start == seg.start and DATA[cand.text]
      if row then
        done = true
        yield(Candidate("yiwei_poetry", cand.start, cand._end, cand.text .. "，" .. row[1], comment(row)))
      elseif i >= LOOKAHEAD then
        done = true
      end
    end
  end
end

return F
