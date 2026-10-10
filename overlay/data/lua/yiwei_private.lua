-- 一维输入法 · 无痕窗口不学词
-- 一维助手在前台是浏览器无痕 / InPrivate 窗口时写 yiwei/private.now；
-- 这时用空格或数字选中一个覆盖全部拼音的候选，直接上屏而不经过用户词库（不记词频、不造新词）。
local P = {}

local function sep() return package.config:sub(1, 1) end

local function private(env)
  local now = os.clock()
  if env.checked and now - env.checked < 0.5 then return env.on end
  env.checked = now
  local f = io.open(env.flag, "r")
  env.on = f ~= nil
  if f then f:close() end
  return env.on
end

function P.init(env)
  env.flag = rime_api.get_user_data_dir() .. sep() .. "yiwei" .. sep() .. "private.now"
end

function P.func(key, env)
  if key:release() or key:ctrl() or key:alt() or key:super() then return 2 end
  local ctx = env.engine.context
  if not ctx:has_menu() or not private(env) then return 2 end
  local code = key.keycode
  local index
  if code == 0x20 then
    index = nil -- highlighted candidate
  elseif code >= 0x31 and code <= 0x39 then
    index = code - 0x31
  else
    return 2
  end
  local seg = ctx.composition:back()
  if not seg or not seg.menu then return 2 end
  local page_size = env.engine.schema.page_size
  local selected = seg.selected_index
  local i = index and (math.floor(selected / page_size) * page_size + index) or selected
  local cand = seg:get_candidate_at(i)
  if not cand then return 2 end
  if cand._end < #ctx.input or cand.start > 0 then return 2 end -- partial selection: leave it to RIME
  env.engine:commit_text(cand.text)
  ctx:clear()
  return 1
end

return P
