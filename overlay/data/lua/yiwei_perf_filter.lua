-- 一维输入法 · 性能体检（34）：第一个候选出来时停表，记下这次按键引擎用了多少毫秒。
local S = require("yiwei_perf_state")
local F = {}
function F.func(input, env)
  local first = true
  for cand in input:iter() do
    if first and S.t0 then
      S.add((os.clock() - S.t0) * 1000)
      S.t0 = nil
    end
    first = false
    yield(cand)
  end
end
return F
