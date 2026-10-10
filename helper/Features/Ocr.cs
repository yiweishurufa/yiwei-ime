using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yiwei
{
    /// <summary>
    /// 截图识字：Windows 自带的离线 OCR（Windows.Media.Ocr，Win10 起），不联网。
    /// 优先用简体中文识别器；没装中文 OCR 时提示到「设置 → 时间和语言 → 语言 → 中文 → 语言选项」里装「光学字符识别」。
    /// </summary>
    public static class Ocr
    {
        public static bool Supported
        {
            get { try { return Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages.Count > 0; } catch { return false; } }
        }

        public static string Languages()
        {
            try { return string.Join("、", Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages.Select(l => l.DisplayName)); }
            catch (Exception e) { return "不可用（" + e.Message + "）"; }
        }

        static Windows.Media.Ocr.OcrEngine Engine(out bool chinese)
        {
            chinese = false;
            foreach (var tag in new[] { "zh-Hans-CN", "zh-CN", "zh-Hans", "zh-Hant-TW", "zh-TW", "zh-Hant" })
            {
                try
                {
                    var lang = new Windows.Globalization.Language(tag);
                    if (Windows.Media.Ocr.OcrEngine.IsLanguageSupported(lang)) { chinese = true; return Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(lang); }
                }
                catch { }
            }
            return Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
        }

        /// <summary>Recognises the text in a bitmap. Chinese lines come back without the spaces OCR puts between characters.</summary>
        public static async Task<string> Recognize(Bitmap bmp)
        {
            var engine = Engine(out bool chinese);
            if (engine == null) throw new Exception("这台电脑没有可用的 OCR 识别器：请在 Windows 设置 → 时间和语言 → 语言和区域 → 中文 → 语言选项 里安装「光学字符识别」");
            // OCR wants at least ~40 px high text lines; small selections are upscaled.
            using (var scaled = Upscale(bmp))
            using (var ms = new MemoryStream())
            {
                scaled.Save(ms, ImageFormat.Png);
                var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                using (var w = new Windows.Storage.Streams.DataWriter(ras))
                {
                    w.WriteBytes(ms.ToArray());
                    await w.StoreAsync();
                    await w.FlushAsync();
                    w.DetachStream();
                }
                ras.Seek(0);
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ras);
                using (var sb = await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied))
                {
                    var result = await engine.RecognizeAsync(sb);
                    var text = new StringBuilder();
                    foreach (var line in result.Lines) text.AppendLine(JoinWords(line.Words.Select(x => x.Text).ToArray()));
                    return text.ToString().TrimEnd();
                }
            }
        }

        static bool IsCjk(char c) => c >= 0x3000 && c <= 0x9FFF || c >= 0xF900 && c <= 0xFAFF || c >= 0xFF00 && c <= 0xFFEF;

        /// <summary>Joins OCR words: no space between CJK words, one space between Latin ones.</summary>
        public static string JoinWords(string[] words)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < words.Length; i++)
            {
                var w = words[i];
                if (w.Length == 0) continue;
                if (sb.Length > 0)
                {
                    char prev = sb[sb.Length - 1];
                    if (!(IsCjk(prev) || IsCjk(w[0]))) sb.Append(' ');
                }
                sb.Append(w);
            }
            return sb.ToString();
        }

        static Bitmap Upscale(Bitmap src)
        {
            double f = src.Height < 60 ? 3 : src.Height < 200 ? 2 : 1;
            var max = Windows.Media.Ocr.OcrEngine.MaxImageDimension;
            f = Math.Min(f, Math.Min((double)max / src.Width, (double)max / src.Height));
            if (f <= 1.01) return new Bitmap(src);
            var b = new Bitmap((int)(src.Width * f), (int)(src.Height * f));
            using (var g = Graphics.FromImage(b))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, b.Width, b.Height);
            }
            return b;
        }
    }
}
