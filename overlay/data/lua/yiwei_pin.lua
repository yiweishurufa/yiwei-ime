-- 一维输入法 · 候选置顶 / 隐藏（按键）
--   Ctrl+T          把高亮的候选钉在这个拼音的第一位（再按一次取消）
--   Ctrl+Delete     这个拼音下不再显示高亮的候选（用户词同时从词库删除）；已置顶的词则取消置顶
--   Ctrl+Shift+Delete   撤销刚才的隐藏（把最近一次隐藏的候选放回来，可连按，最多记 20 个）
-- 在「一维输入法设置 → 词库 → 置顶与隐藏的候选」里可以查看和撤销。
local store = require("yiwei_pin_store")
local P = {}
local undo = {} -- 本次运行里最近隐藏的 {拼音, 词}

function P.init(env) store.load() end

function P.func(key, env)
  if key:release() then return 2 end
  local r = key:repr()
  local isundo = (r == "Shift+Control+Delete" or r == "Shift+Control+KP_Delete" or r == "Control+Shift+Delete")
  if isundo then
    local ctx0 = env.engine.context
    local last = table.remove(undo)
    if not last or not ctx0:has_menu() then return 2 end
    store.maybe_reload()
    if store.hidden[last[1]] then
      store.hidden[last[1]][last[2]] = nil
      if next(store.hidden[last[1]]) == nil then store.hidden[last[1]] = nil end
    end
    store.save()
    ctx0:refresh_non_confirmed_composition()
    return 1
  end
  if r ~= "Control+t" and r ~= "Control+Delete" and r ~= "Control+KP_Delete" then return 2 end
  local ctx = env.engine.context
  if not ctx:has_menu() then return 2 end
  local cand = ctx:get_selected_candidate()
  local input = ctx.input
  if not cand or cand.start ~= 0 or cand._end ~= #input or not input:match("^[a-z']+$") then return 2 end
  store.maybe_reload()
  local text = cand.text
  if r == "Control+t" then
    if store.pins[input] == text then store.pins[input] = nil else store.pins[input] = text end
    if store.hidden[input] then store.hidden[input][text] = nil end
    store.save()
    ctx:refresh_non_confirmed_composition()
    return 1
  end
  if store.pins[input] == text then
    store.pins[input] = nil
  else
    store.hidden[input] = store.hidden[input] or {}
    store.hidden[input][text] = true
    undo[#undo + 1] = { input, text }
    if #undo > 20 then table.remove(undo, 1) end
  end
  store.save()
  ctx:refresh_non_confirmed_composition()
  return 2 -- let RIME also delete it from the user dictionary when it is a learned word
end

return P
