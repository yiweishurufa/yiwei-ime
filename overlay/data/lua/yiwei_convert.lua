-- 一维输入法 · 单位 / 汇率换算候选（W27，Windows 与安卓共用，完全离线）
-- 用法：hs + 数字 + 单位，例如
--   hs5km      → 5 千米 = 5000 米 / 3.107 英里 / 10 里 …
--   hs100f     → 100 °F = 37.78 °C
--   hs2jin     → 2 斤 = 1 千克 / 2.205 磅
--   hs100usd   → 100 美元 ≈ 670.68 人民币 …（汇率见下）
-- 汇率：内置 2026-10-11 的参考汇率（open.er-api.com），离线可用、不会自动更新，候选注释里标明日期；
-- 用户目录 yiwei/rates.txt 存在时用它（每行「币种代码 每 1 人民币可换的数量」，如 USD 0.1491；首行可写 date 2026-10-11）。
local flags = require("yiwei_flags")

local PREFIX = "hs"

-- 每个单位：类别、换成基本单位的系数、中文名
local U = {
  -- 长度（米）
  km = { "len", 1000, "千米" }, m = { "len", 1, "米" }, dm = { "len", 0.1, "分米" }, cm = { "len", 0.01, "厘米" },
  mm = { "len", 0.001, "毫米" }, mi = { "len", 1609.344, "英里" }, yd = { "len", 0.9144, "码" },
  ft = { "len", 0.3048, "英尺" }, inch = { "len", 0.0254, "英寸" }, ["in"] = { "len", 0.0254, "英寸" },
  nmi = { "len", 1852, "海里" }, li = { "len", 500, "里" }, zhang = { "len", 10 / 3, "丈" },
  chi = { "len", 1 / 3, "尺" }, cun = { "len", 1 / 30, "寸" },
  -- 质量（千克）
  t = { "mass", 1000, "吨" }, kg = { "mass", 1, "千克" }, g = { "mass", 0.001, "克" }, mg = { "mass", 1e-6, "毫克" },
  jin = { "mass", 0.5, "斤" }, liang = { "mass", 0.05, "两" }, lb = { "mass", 0.45359237, "磅" },
  oz = { "mass", 0.028349523125, "盎司" }, ct = { "mass", 0.0002, "克拉" },
  -- 面积（平方米）
  sqm = { "area", 1, "平方米" }, pingfang = { "area", 1, "平方米" }, sqkm = { "area", 1e6, "平方千米" },
  ha = { "area", 1e4, "公顷" }, mu = { "area", 10000 / 15, "亩" }, acre = { "area", 4046.8564224, "英亩" },
  sqft = { "area", 0.09290304, "平方英尺" },
  -- 体积（升）
  l = { "vol", 1, "升" }, ml = { "vol", 0.001, "毫升" }, gal = { "vol", 3.785411784, "美制加仑" },
  floz = { "vol", 0.0295735295625, "美制液量盎司" }, cup = { "vol", 0.2365882365, "美制杯" },
  -- 数据（字节）
  b = { "data", 1, "字节" }, kb = { "data", 1024, "KB" }, mb = { "data", 1024 ^ 2, "MB" },
  gb = { "data", 1024 ^ 3, "GB" }, tb = { "data", 1024 ^ 4, "TB" },
  -- 速度（米/秒）
  kmh = { "speed", 1 / 3.6, "千米/时" }, mps = { "speed", 1, "米/秒" }, mph = { "speed", 0.44704, "英里/时" },
  kn = { "speed", 0.514444, "节" },
  -- 温度（单独换算）
  c = { "temp", 0, "°C" }, f = { "temp", 0, "°F" }, k = { "temp", 0, "K" },
}
-- 每类常用的目标单位（顺序即候选顺序）
local TARGETS = {
  len = { "m", "km", "cm", "mi", "ft", "inch", "li", "chi" },
  mass = { "kg", "g", "jin", "lb", "oz", "t" },
  area = { "sqm", "mu", "ha", "sqft", "acre", "sqkm" },
  vol = { "l", "ml", "gal", "floz" },
  data = { "mb", "gb", "kb", "tb", "b" },
  speed = { "kmh", "mps", "mph", "kn" },
  temp = { "c", "f", "k" },
}

-- 汇率：1 人民币 = x 外币（2026-10-11，open.er-api.com）
local RATES_DATE = "2026-10-11"
local RATES = {
  CNY = 1, USD = 0.149102, EUR = 0.133217, JPY = 23.612922, GBP = 0.112784, HKD = 1.16994,
  KRW = 200.160128, TWD = 4.764173, MOP = 1.205031, SGD = 0.190881, AUD = 0.214058,
  CAD = 0.212555, RUB = 12.690355, THB = 5.003318,
}
local CUR_NAME = {
  CNY = "人民币", USD = "美元", EUR = "欧元", JPY = "日元", GBP = "英镑", HKD = "港币", KRW = "韩元",
  TWD = "新台币", MOP = "澳门元", SGD = "新加坡元", AUD = "澳元", CAD = "加元", RUB = "卢布", THB = "泰铢",
}
local CUR_ALIAS = { rmb = "CNY", yuan = "CNY", meiyuan = "USD", ouyuan = "EUR", riyuan = "JPY", yingbang = "GBP", gangbi = "HKD" }
local CUR_TARGETS = { "CNY", "USD", "EUR", "JPY", "HKD", "GBP" }

local user_rates_loaded
local function load_user_rates()
  if user_rates_loaded then return end
  user_rates_loaded = true
  local f = io.open(flags.path("rates.txt"), "r")
  if not f then return end
  for line in f:lines() do
    local d = line:match("^date%s+(%S+)")
    if d then RATES_DATE = d end
    local code, v = line:match("^(%u%u%u)%s+([%d.]+)")
    if code and tonumber(v) then RATES[code] = tonumber(v) end
  end
  f:close()
end

local function fmt(x)
  if x == 0 then return "0" end
  local a = math.abs(x)
  local s
  if a >= 1e9 or a < 1e-4 then s = string.format("%.4g", x)
  elseif a >= 100 then s = string.format("%.2f", x)
  elseif a >= 1 then s = string.format("%.4f", x)
  else s = string.format("%.6f", x) end
  if s:find("%.") and not s:find("e") then s = s:gsub("0+$", ""):gsub("%.$", "") end
  return s
end

local function temp(v, from, to)
  local c = from == "c" and v or from == "f" and (v - 32) * 5 / 9 or v - 273.15
  if to == "c" then return c elseif to == "f" then return c * 9 / 5 + 32 else return c + 273.15 end
end

local function convert(num, unit)
  local out = {}
  local cur = CUR_ALIAS[unit] or (#unit == 3 and unit:upper()) or nil
  if cur and RATES[cur] and not U[unit] then
    load_user_rates()
    local cny = num / RATES[cur]
    for _, t in ipairs(CUR_TARGETS) do
      if t ~= cur and RATES[t] then
        out[#out + 1] = { fmt(cny * RATES[t]), fmt(num) .. " " .. CUR_NAME[cur] .. " ≈ " .. fmt(cny * RATES[t]) .. " " .. CUR_NAME[t],
                          "〔汇率 " .. RATES_DATE .. "，仅供参考〕" }
      end
    end
    return out
  end
  local u = U[unit]
  if not u then return out end
  local cat = u[1]
  for _, t in ipairs(TARGETS[cat]) do
    if t ~= unit and not (U[t][3] == u[3]) then
      local v = cat == "temp" and temp(num, unit, t) or num * u[2] / U[t][2]
      out[#out + 1] = { fmt(v), fmt(num) .. " " .. u[3] .. " = " .. fmt(v) .. " " .. U[t][3], "〔" .. U[t][3] .. "〕" }
    end
  end
  return out
end

local T = {}

function T.func(input, seg, env)
  if input:sub(1, #PREFIX) ~= PREFIX then return end
  if flags.exists("convert.disabled") then return end
  local num, unit = input:sub(#PREFIX + 1):match("^([%d.]+)(%a+)$")
  num = tonumber(num)
  if not num then return end
  unit = unit:lower()
  local list = convert(num, unit)
  -- 先给带单位的整句，再给纯数字
  for _, r in ipairs(list) do yield(Candidate("yiwei_convert", seg.start, seg._end, r[2], "")) end
  for _, r in ipairs(list) do yield(Candidate("yiwei_convert", seg.start, seg._end, r[1], r[3])) end
end

return T
