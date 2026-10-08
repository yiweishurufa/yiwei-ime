using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
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

        /// <summary>
        /// Streams a completion (SSE, stream=true). onText receives the whole text so far (think blocks removed).
        /// Falls back to a normal JSON reply when the server does not stream.
        /// </summary>
        public static Task<string> Stream(string instruction, string text, Action<string> onText, CancellationToken ct) => Task.Run(() =>
        {
            var s = Settings.Current;
            var url = s.AiBaseUrl.TrimEnd('/');
            if (!url.EndsWith("/chat/completions")) url += "/chat/completions";
            var body = Json.Write(new Dictionary<string, object>
            {
                ["model"] = s.AiModel,
                ["temperature"] = 0.3,
                ["stream"] = true,
                ["messages"] = new object[]
                {
                    new Dictionary<string, string> { ["role"] = "system", ["content"] = "你是输入法里的写作助手。" + instruction },
                    new Dictionary<string, string> { ["role"] = "user", ["content"] = text },
                },
            });
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Accept = "text/event-stream";
            req.Timeout = 60000;
            req.ReadWriteTimeout = 60000;
            req.UserAgent = "YiweiIME/0.2";
            req.AllowReadStreamBuffering = false;
            var key = s.AiKey;
            if (!string.IsNullOrEmpty(key)) req.Headers["Authorization"] = "Bearer " + key;
            using (ct.Register(() => { try { req.Abort(); } catch { } }))
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                try
                {
                    using (var st = req.GetRequestStream()) st.Write(bytes, 0, bytes.Length);
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        if ((resp.ContentType ?? "").IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0 && (resp.ContentType ?? "").IndexOf("event-stream", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            var full = Extract(r.ReadToEnd());
                            onText?.Invoke(full);
                            return full;
                        }
                        var sb = new StringBuilder();
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            ct.ThrowIfCancellationRequested();
                            if (!line.StartsWith("data:")) continue;
                            var data = line.Substring(5).Trim();
                            if (data == "[DONE]") break;
                            if (data.Length == 0) continue;
                            var delta = Delta(data);
                            if (string.IsNullOrEmpty(delta)) continue;
                            sb.Append(delta);
                            onText?.Invoke(StripThink(sb.ToString(), false));
                        }
                        var result = StripThink(sb.ToString(), true);
                        onText?.Invoke(result);
                        return result;
                    }
                }
                catch (WebException e) when (e.Response != null)
                {
                    using (var r = new StreamReader(e.Response.GetResponseStream(), Encoding.UTF8))
                    {
                        var err = r.ReadToEnd();
                        throw new Exception("接口返回错误：" + (err.Length > 200 ? err.Substring(0, 200) : err));
                    }
                }
                catch (WebException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
            }
        }, ct);

        static string Delta(string json)
        {
            try
            {
                var root = Json.Parse(json) as Dictionary<string, object>;
                if (root != null && root.TryGetValue("choices", out var ch) && ch is object[] arr && arr.Length > 0 && arr[0] is Dictionary<string, object> c0)
                {
                    if (c0.TryGetValue("delta", out var d) && d is Dictionary<string, object> delta && delta.TryGetValue("content", out var c)) return c as string;
                    if (c0.TryGetValue("message", out var m) && m is Dictionary<string, object> msg && msg.TryGetValue("content", out var mc)) return mc as string;
                }
            }
            catch { }
            return null;
        }

        /// <summary>Hides a leading &lt;think&gt; block of reasoning models (while it is still open, shows nothing).</summary>
        static string StripThink(string text, bool final)
        {
            var t = text.TrimStart();
            if (!t.StartsWith("<think>")) return final ? text.Trim() : text;
            int end = t.IndexOf("</think>", StringComparison.Ordinal);
            if (end < 0) return "";
            var rest = t.Substring(end + 8);
            return final ? rest.Trim() : rest.TrimStart();
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
