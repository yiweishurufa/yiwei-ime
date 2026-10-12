-- 一维输入法 · 搜索前缀（W28）：sS + 拼音，候选是要搜的词；空格 / 数字 / 回车选中后不上屏，
-- 交给一维助手用浏览器搜索（搜索引擎在 设置 → 工具 里选）。助手不在时把词上屏，不丢输入。
local P = {}
local PIPE = "\\\\.\\pipe\\YiweiHelper.Commands"

local function send(msg)
  local ok, sent = pcall(function()
    local f = io.open(PIPE, "wb")
    if f then f:write(msg); f:close(); return true end
    return false
  end)
  return ok and sent
end

function P.init(env)
  env.page_size = env.engine.schema.page_size or 5
end

function P.func(key, env)
  if key:release() or key:ctrl() or key:alt() or key:super() then return 2 end
  local ctx = env.engine.context
  if not ctx:is_composing() then return 2 end
  local seg = ctx.composition:back()
  if not seg or not seg:has_tag("yiwei_search") then return 2 end
  local kc = key.keycode
  local idx
  if kc == 0x20 or kc == 0xff0d or kc == 0xff8d then
    idx = seg.selected_index
  elseif kc >= 0x31 and kc <= 0x39 then
    local ps = env.page_size
    idx = math.floor(seg.selected_index / ps) * ps + (kc - 0x31)
  else
    return 2
  end
  local cand = seg:get_candidate_at(idx)
  local q = cand and cand.text or ""
  if q == "" then
    q = (ctx.input or ""):gsub("^sS", "")
  end
  if q == "" then return 2 end
  ctx:clear()
  if not send("search:" .. q) then env.engine:commit_text(q) end
  return 1
end

return P
