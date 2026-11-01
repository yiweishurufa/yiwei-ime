using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Yiwei
{
    /// <summary>
    /// 常用语里的时间变量：{date} {time} {year} {month} {day} {hour} {minute} {second} {weekday}，上屏时换成当时的值。
    /// 和 overlay/data/lua/yiwei_phrase_vars.lua（打编码出的候选）同一套写法；写错的、不认识的 {xxx} 原样保留。
    /// 只有内容里写了这些词才生效，相当于用户自己打开。
    /// </summary>
    static class PhraseVars
    {
        static readonly Regex Var = new Regex(@"\{(date|time|year|month|day|hour|minute|second|weekday)\}", RegexOptions.Compiled);
        static readonly string[] Week = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };

        public static string Expand(string text) => Expand(text, DateTime.Now);

        public static string Expand(string text, DateTime now)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            return Var.Replace(text, m =>
            {
                switch (m.Groups[1].Value)
                {
                    case "date": return now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    case "time": return now.ToString("HH:mm", CultureInfo.InvariantCulture);
                    case "year": return now.Year.ToString(CultureInfo.InvariantCulture);
                    case "month": return now.Month.ToString(CultureInfo.InvariantCulture);
                    case "day": return now.Day.ToString(CultureInfo.InvariantCulture);
                    case "hour": return now.ToString("HH", CultureInfo.InvariantCulture);
                    case "minute": return now.ToString("mm", CultureInfo.InvariantCulture);
                    case "second": return now.ToString("ss", CultureInfo.InvariantCulture);
                    case "weekday": return Week[(int)now.DayOfWeek];
                }
                return m.Value;
            });
        }
    }
}
