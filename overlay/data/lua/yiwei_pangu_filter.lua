-- 一维输入法 · 中英之间自动加空格：过滤器（见 yiwei_pangu_core.lua）
local M = require("yiwei_pangu_core")

local F = {}
function F.func(input, env)
  local pad = M.enabled() and M.recent()
  for cand in input:iter() do
    if pad and cand.text ~= "" and M.needs_space(M.last, cand.text) then
      yield(ShadowCandidate(cand, cand.type, " " .. cand.text, cand.comment))
    else
      yield(cand)
    end
  end
end

return F
