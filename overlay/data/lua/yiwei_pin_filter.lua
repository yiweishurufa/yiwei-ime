-- 一维输入法 · 候选置顶 / 隐藏（过滤）：按 yiwei/pins.tsv 把置顶的词放第一、隐藏的词拿掉
local store = require("yiwei_pin_store")
local F = {}

function F.init(env) store.load() end

function F.func(input, env)
  local code = env.engine.context.input
  store.maybe_reload()
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
