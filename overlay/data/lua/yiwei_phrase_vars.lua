-- 一维输入法 · 常用语时间变量：候选文字里的 {date} {time} {year} {month} {day} {hour} {minute} {second} {weekday}
-- 换成当时的值（如常用语「今天是{date}」→ 今天是2026-11-02）。和 helper/Features/PhraseVars.cs 同一套写法。
-- 只处理含「{」的候选（普通候选只做一次 find），不认识的 {xxx} 原样保留；只有用户自己写了这些词才会生效。
local F = {}
local WEEK = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" }

local function expand(text)
  local t = os.date("*t")
  local vals = {
    date = os.date("%Y-%m-%d"), time = os.date("%H:%M"),
    year = tostring(t.year), month = tostring(t.month), day = tostring(t.day),
    hour = os.date("%H"), minute = os.date("%M"), second = os.date("%S"),
    weekday = WEEK[t.wday],
  }
  return (text:gsub("{(%a+)}", function(k) return vals[k] end))
end

function F.func(input)
  for cand in input:iter() do
    if cand.text:find("{", 1, true) then
      local ok, out = pcall(expand, cand.text)
      if ok and out ~= cand.text then
        local c = Candidate(cand.type, cand.start, cand._end, out, cand.comment)
        c.quality = cand.quality
        c.preedit = cand.preedit
        yield(c)
      else
        yield(cand)
      end
    else
      yield(cand)
    end
  end
end

return F
