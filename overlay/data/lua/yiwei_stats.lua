-- 一维输入法 · 输入统计
-- 只有用户在「一维输入法设置 → 统计」里开启后（存在 yiwei/stats.enabled）才记录。
-- 记录只写在本机用户文件夹 yiwei/stats-YYYY-MM.tsv：时间戳<TAB>上屏文字。
local M = {}

local function sep()
  return package.config:sub(1, 1)
end

function M.init(env)
  local dir = rime_api.get_user_data_dir() .. sep() .. "yiwei"
  env.yiwei_dir = dir
  env.yiwei_flag = dir .. sep() .. "stats.enabled"
  env.yiwei_conn = env.engine.context.commit_notifier:connect(function(ctx)
    local f = io.open(env.yiwei_flag, "r")
    if not f then return end
    f:close()
    -- 浏览器无痕 / InPrivate 窗口里不统计（一维助手写的标记）
    local p = io.open(env.yiwei_dir .. sep() .. "private.now", "r")
    if p then p:close(); return end
    local text = ctx:get_commit_text()
    if not text or text == "" then return end
    local path = env.yiwei_dir .. sep() .. "stats-" .. os.date("%Y-%m") .. ".tsv"
    local out = io.open(path, "a")
    if out then
      out:write(tostring(os.time()), "\t", (text:gsub("[\t\r\n]", " ")), "\n")
      out:close()
    end
  end)
end

function M.fini(env)
  if env.yiwei_conn then env.yiwei_conn:disconnect() end
end

function M.func(key, env)
  return 2 -- kNoop
end

return M
