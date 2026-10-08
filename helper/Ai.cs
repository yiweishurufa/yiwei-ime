using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Yiwei
{
    /// <summary>Calls an OpenAI-compatible chat completions endpoint (OpenAI, DeepSeek, 通义, Kimi, Ollama, LM Studio …).</summary>
    public static class Ai
    {
        static Ai()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; // TLS 1.3 where available
        }

        public static bool Configured
        {
            get
            {
                var s = Settings.Current;
                if (string.IsNullOrWhiteSpace(s.AiBaseUrl) || string.IsNullOrWhiteSpace(s.AiModel)) return false;
                var local = s.AiBaseUrl.Contains("localhost") || s.AiBaseUrl.Contains("127.0.0.1");
                return local || !string.IsNullOrEmpty(s.AiKey);
            }
        }

        public static Task<string> Run(string instruction, string text) => Task.Run(() => Complete(instruction, text));

        public static string Complete(string instruction, string text)
        {
            var s = Settings.Current;
            var url = s.AiBaseUrl.TrimEnd('/');
            if (!url.EndsWith("/chat/completions")) url += "/chat/completions";
            var body = Json.Write(new Dictionary<string, object>
            {
                ["model"] = s.AiModel,
                ["temperature"] = 0.3,
                ["messages"] = new object[]
                {
                    new Dictionary<string, string> { ["role"] = "system", ["content"] = "你是输入法里的写作助手。" + instruction },
                    new Dictionary<string, string> { ["role"] = "user", ["content"] = text },
                },
            });
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Timeout = 60000;
            req.UserAgent = "YiweiIME/0.1";
            var key = s.AiKey;
            if (!string.IsNullOrEmpty(key)) req.Headers["Authorization"] = "Bearer " + key;
            var bytes = Encoding.UTF8.GetBytes(body);
            using (var st = req.GetRequestStream()) st.Write(bytes, 0, bytes.Length);
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return Extract(r.ReadToEnd());
            }
            catch (WebException e) when (e.Response != null)
            {
                using (var r = new StreamReader(e.Response.GetResponseStream(), Encoding.UTF8))
                {
                    var err = r.ReadToEnd();
                    throw new Exception("接口返回错误：" + (err.Length > 200 ? err.Substring(0, 200) : err));
                }
            }
        }

        static string Extract(string json)
        {
            var root = Json.Parse(json) as Dictionary<string, object>;
            if (root != null && root.TryGetValue("choices", out var ch) && ch is object[] arr && arr.Length > 0
                && arr[0] is Dictionary<string, object> c0 && c0.TryGetValue("message", out var m)
                && m is Dictionary<string, object> msg && msg.TryGetValue("content", out var content))
            {
                var text = (content as string ?? "").Trim();
                // strip <think> blocks of reasoning models
                int t = text.IndexOf("</think>", StringComparison.Ordinal);
                if (text.StartsWith("<think>") && t > 0) text = text.Substring(t + 8).Trim();
                return text;
            }
            throw new Exception("无法解析接口返回");
        }
    }
}
