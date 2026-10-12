-- 一维输入法 · 性能体检（34）：按键时开始计时（不处理按键，总是交给后面的组件）。
local S = require("yiwei_perf_state")
local P = {}
function P.func(key, env)
  if not key:release() then S.t0 = os.clock() end
  return 2
end
return P
