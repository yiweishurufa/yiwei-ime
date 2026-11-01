-- 一维输入法 · 中英混输词条（backlog 7）：yiwei_mixed.txt 里的词（PPT、QQ、IC卡、C位……）不抢首选。
-- 词条由 table_translator@yiwei_mixed 给出、通常排第一；这里只在「整段输入正好是词条编码、且它排在第一」时，
-- 把它挪到第 2 位（只有它一个候选时不动）。只看前两个候选，按键时不读文件。
-- 关掉：设置 → 词库「中英混输词条」会在用户文件夹放一份没有词条的 yiwei_mixed.txt，这里读到空表就什么都不做。
-- 排在 yiwei_learn 之前：用户经常选 PPT，学习过滤器仍可以把它提回第一。
local F = {}

local function load_entries()
  local set, n = {}, 0
  local dirs = { rime_api.get_user_data_dir(), rime_api.get_shared_data_dir() }
  for _, dir in ipairs(dirs) do
    local f = dir and io.open(dir .. "/yiwei_mixed.txt", "r")
    if f then
      for line in f:lines() do
        local word, code = line:match("^([^#\t][^\t]*)\t([a-z]+)")
        if word then set[code .. "\t" .. word] = true; n = n + 1 end
      end
      f:close()
      return set, n -- 用户文件夹优先（关掉时是空文件），不再看安装目录
    end
  end
  return set, n
end

function F.init(env)
  local ok, set, n = pcall(load_entries)
  if ok then env.set, env.n = set, n else env.set, env.n = {}, 0; log.error("yiwei_mixed_filter: " .. tostring(set)) end
end

function F.func(input, env)
  if env.n == 0 then
    for cand in input:iter() do yield(cand) end
    return
  end
  local code = env.engine.context.input
  local first
  for cand in input:iter() do
    if not first then
      if env.set[code .. "\t" .. cand.text] then
        first = cand
      else
        yield(cand)
        for rest in input:iter() do yield(rest) end
        return
      end
    else
      yield(cand)
      yield(first)
      first = nil
      for rest in input:iter() do yield(rest) end
      return
    end
  end
  if first then yield(first) end
end

return F
