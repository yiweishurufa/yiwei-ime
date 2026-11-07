-- 一维输入法 · 候选置顶 / 隐藏（过滤）：按 yiwei/pins.tsv 把置顶的词放第一、隐藏的词拿掉
local store = require("yiwei_pin_store")
local F = {}

-- 全局屏蔽词：yiwei/blocked.txt，一行一个词，在任何拼音下都不再出现（3 秒缓存）
local blocked, blocked_at = {}, -10
local function load_blocked()
  local now = os.time()
  if now - blocked_at < 3 then return end
  blocked_at = now
  blocked = {}
  local sp = package.config:sub(1, 1)
  local f = io.open(rime_api.get_user_data_dir() .. sp .. "yiwei" .. sp .. "blocked.txt", "r")
  if not f then return end
  for line in f:lines() do
    line = line:gsub("^%s+", ""):gsub("%s+$", "")
    if line ~= "" and line:sub(1, 1) ~= "#" then blocked[line] = true end
  end
  f:close()
end

function F.init(env) store.load() end

function F.func(input, env)
  local code = env.engine.context.input
  store.maybe_reload()
  load_blocked()
  if next(blocked) then
    local rest = {}
    for cand in input:iter() do
      if not blocked[cand.text] then table.insert(rest, cand) end
    end
    local code0 = env.engine.context.input
    local pin0 = store.pins[code0]
    local hide0 = store.hidden[code0]
    if not pin0 and not hide0 then
      for _, c in ipairs(rest) do yield(c) end
      return
    end
    local i = 0
    input = { iter = function() i = 0; return function() i = i + 1; return rest[i] end end }
  end
  local pin = store.pins[code]
  local hide = store.hidden[code]
  if not pin and not hide then
    for cand in input:iter() do yield(cand) end
    return
  end
  local buffered, found = {}, nil
  local n = 0
  for cand in input:iter() do
    n = n + 1
    if pin and not found and cand.text == pin then
      found = cand
      break
    end
    if not (hide and hide[cand.text]) then table.insert(buffered, cand) end
    if n >= 120 then break end
  end
  if pin then
    if found then
      yield(found)
    else
      local c = Candidate("yiwei_pin", 0, #code, pin, "📌")
      yield(c)
    end
  end
  for _, cand in ipairs(buffered) do yield(cand) end
  for cand in input:iter() do
    if not (hide and hide[cand.text]) and cand.text ~= pin then yield(cand) end
  end
end

return F
