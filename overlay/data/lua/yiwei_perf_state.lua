-- 一维输入法 · 性能体检（34）共享状态：yiwei_perf（处理器，按键开始计时）和 yiwei_perf_filter（候选出来时停表）共用。
-- 只在本机记最近 200 次按键的引擎耗时，每 20 次写一次 yiwei/perf.tsv：次数、平均、最慢、超过 50ms 的次数（毫秒）。
local S = { t0 = nil, ring = {}, n = 0, total = 0 }

local function sep() return package.config:sub(1, 1) end

function S.flush()
  local ring = S.ring
  if #ring == 0 then return end
  local sum, max, slow = 0, 0, 0
  for _, v in ipairs(ring) do
    sum = sum + v
    if v > max then max = v end
    if v > 50 then slow = slow + 1 end
  end
  local path = rime_api.get_user_data_dir() .. sep() .. "yiwei" .. sep() .. "perf.tsv"
  local f = io.open(path, "w")
  if f then
    f:write(string.format("%d\t%.1f\t%.1f\t%d\t%d\t%d\n", #ring, sum / #ring, max, slow, S.total, os.time()))
    f:close()
  end
end

function S.add(ms)
  S.n = S.n % 200 + 1
  S.ring[S.n] = ms
  S.total = S.total + 1
  if S.total % 20 == 0 then S.flush() end
end

return S
