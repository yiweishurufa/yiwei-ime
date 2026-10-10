using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Yiwei
{
    /// <summary>
    /// 手写识别：Windows 自带的离线手写识别（Windows.UI.Input.Inking，中文系统通常已装中文手写）。
    /// 笔画是鼠标 / 触控笔画出来的点串，按笔交给 InkRecognizerContainer，返回候选字。
    /// </summary>
    public static class Handwriting
    {
        static Windows.UI.Input.Inking.InkRecognizerContainer _rec;
        static bool _chinese;

        public static bool Ready(out string message)
        {
            message = "";
            try
            {
                if (_rec == null)
                {
                    _rec = new Windows.UI.Input.Inking.InkRecognizerContainer();
                    var list = _rec.GetRecognizers();
                    var zh = list.FirstOrDefault(r => r.Name.Contains("中文") || r.Name.Contains("Chinese") || r.Name.Contains("简体"));
                    if (zh != null) { _rec.SetDefaultRecognizer(zh); _chinese = true; }
                    if (list.Count == 0) { _rec = null; message = "没有找到手写识别器"; return false; }
                }
                if (!_chinese) message = "没有中文手写识别器，可在 Windows 设置 → 时间和语言 → 语言 → 中文 → 语言选项 里安装「手写」";
                return true;
            }
            catch (Exception e) { _rec = null; message = "手写识别不可用：" + e.Message; return false; }
        }

        /// <summary>Strokes in device-independent pixels → up to <paramref name="max"/> candidate strings.</summary>
        public static async Task<List<string>> Recognize(IEnumerable<IList<System.Windows.Point>> strokes, int max = 9)
        {
            if (!Ready(out var msg)) throw new Exception(msg);
            var builder = new Windows.UI.Input.Inking.InkStrokeBuilder();
            var container = new Windows.UI.Input.Inking.InkStrokeContainer();
            int n = 0;
            foreach (var s in strokes)
            {
                if (s.Count < 2) continue;
                var pts = s.Select(p => new Windows.Foundation.Point(p.X, p.Y)).ToList();
                container.AddStroke(builder.CreateStroke(pts));
                n++;
            }
            if (n == 0) return new List<string>();
            var results = await _rec.RecognizeAsync(container, Windows.UI.Input.Inking.InkRecognitionTarget.All);
            var output = new List<string>();
            if (results.Count == 1)
                output.AddRange(results[0].GetTextCandidates());
            else if (results.Count > 1)
            {
                // several words: first choice of each joined, then the alternatives of the first word
                output.Add(string.Concat(results.Select(r => r.GetTextCandidates().FirstOrDefault() ?? "")));
                output.AddRange(results[0].GetTextCandidates());
            }
            return output.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(max).ToList();
        }
    }
}
