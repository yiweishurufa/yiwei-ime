using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Yiwei
{
    /// <summary>
    /// 局域网互传：电脑上开一个只在本窗口打开期间运行的小网页服务，手机（同一 Wi-Fi）扫码打开网页即可互发文字。
    /// 不经过任何云端；地址里带一次性随机口令，口令不对一律 404。用 TcpListener 自己解析最简单的 HTTP，
    /// 这样普通用户权限即可监听（HttpListener 需要管理员先注册 URL）。Windows 防火墙第一次会询问是否允许。
    /// </summary>
    public sealed class LanShare : IDisposable
    {
        public const int FirstPort = 18650;
        const int MaxBody = 256 * 1024;

        public sealed class Message { public int Id; public string Text; public string At; public string File; public string FileName; }

        TcpListener _listener;
        CancellationTokenSource _cts;
        readonly List<Message> _toPhone = new List<Message>();
        int _nextId = 1;

        public string Token { get; } = NewToken();
        public int Port { get; private set; }
        public string Address { get; private set; } = "";
        public string Url => "http://" + Address + ":" + Port + "/" + Token + "/";

        /// <summary>Text sent from the phone (raised on a worker thread).</summary>
        public event Action<string> Received;
        /// <summary>A phone opened the page (remote address).</summary>
        public event Action<string> Connected;

        public void Start()
        {
            Address = LanAddress() ?? throw new Exception("没有找到局域网地址：请先连上 Wi-Fi 或网线");
            Exception last = null;
            for (int p = FirstPort; p < FirstPort + 20; p++)
            {
                try { _listener = new TcpListener(IPAddress.Any, p); _listener.Start(); Port = p; break; }
                catch (Exception e) { last = e; _listener = null; }
            }
            if (_listener == null) throw new Exception("端口都被占用：" + last?.Message);
            _cts = new CancellationTokenSource();
            Task.Run(() => AcceptLoop(_cts.Token));
            Log.Write("lan share: listening on " + Address + ":" + Port);
        }

        public void SendToPhone(string text)
        {
            lock (_toPhone)
            {
                _toPhone.Add(new Message { Id = _nextId++, Text = text, At = DateTime.Now.ToString("HH:mm") });
                if (_toPhone.Count > 50) _toPhone.RemoveAt(0);
            }
        }

        /// <summary>Offers a file for download on the phone page (e.g. the 词库 zip for Android).</summary>
        public void OfferFile(string path, string displayName)
        {
            lock (_toPhone)
            {
                _toPhone.Add(new Message { Id = _nextId++, Text = displayName, At = DateTime.Now.ToString("HH:mm"), File = path, FileName = displayName });
                if (_toPhone.Count > 50) _toPhone.RemoveAt(0);
            }
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;
        }

        async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient c;
                try { c = await _listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch { if (ct.IsCancellationRequested) return; await Task.Delay(200).ConfigureAwait(false); continue; }
                _ = Task.Run(() => Serve(c));
            }
        }

        void Serve(TcpClient c)
        {
            using (c)
            {
                try
                {
                    c.ReceiveTimeout = 8000; c.SendTimeout = 8000;
                    var s = c.GetStream();
                    // ---- request line + headers ----
                    var head = ReadHead(s, out var rest);
                    if (head == null) return;
                    var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    var first = lines[0].Split(' ');
                    if (first.Length < 2) return;
                    string method = first[0], target = first[1];
                    int len = 0;
                    foreach (var l in lines.Skip(1))
                        if (l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) int.TryParse(l.Substring(15).Trim(), out len);
                    var path = target.Split('?')[0];
                    var prefix = "/" + Token + "/";
                    if (!path.StartsWith(prefix)) { Reply(s, 404, "text/plain", "not found"); return; }
                    var route = path.Substring(prefix.Length);
                    var remote = ((IPEndPoint)c.Client.RemoteEndPoint).Address.ToString();

                    if (method == "GET" && route == "")
                    {
                        Connected?.Invoke(remote);
                        Reply(s, 200, "text/html; charset=utf-8", Page());
                        return;
                    }
                    if (method == "POST" && route == "send")
                    {
                        if (len <= 0 || len > MaxBody) { Reply(s, 413, "text/plain", "too large"); return; }
                        var body = new MemoryStream();
                        body.Write(rest, 0, rest.Length);
                        var buf = new byte[8192];
                        while (body.Length < len)
                        {
                            int n = s.Read(buf, 0, (int)Math.Min(buf.Length, len - body.Length));
                            if (n <= 0) break;
                            body.Write(buf, 0, n);
                        }
                        var text = Encoding.UTF8.GetString(body.ToArray()).Trim();
                        if (text.Length > 0) Received?.Invoke(text);
                        Reply(s, 200, "application/json", "{\"ok\":true}");
                        return;
                    }
                    if (method == "GET" && route == "pull")
                    {
                        int after = 0;
                        var q = target.Contains("?") ? target.Substring(target.IndexOf('?') + 1) : "";
                        foreach (var kv in q.Split('&')) if (kv.StartsWith("after=")) int.TryParse(kv.Substring(6), out after);
                        List<Message> list;
                        lock (_toPhone) list = _toPhone.Where(m => m.Id > after).ToList();
                        Reply(s, 200, "application/json; charset=utf-8", Json.Write(list.Select(m => new Dictionary<string, object> { { "id", m.Id }, { "text", m.Text }, { "at", m.At }, { "file", m.File != null } }).ToList()));
                        return;
                    }
                    if (method == "GET" && route.StartsWith("file/") && int.TryParse(route.Substring(5), out var fid))
                    {
                        Message m;
                        lock (_toPhone) m = _toPhone.FirstOrDefault(x => x.Id == fid && x.File != null);
                        if (m == null || !File.Exists(m.File)) { Reply(s, 404, "text/plain", "not found"); return; }
                        var bytes = File.ReadAllBytes(m.File);
                        var h = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/zip\r\nContent-Length: " + bytes.Length +
                            "\r\nContent-Disposition: attachment; filename=\"yiwei.zip\"; filename*=UTF-8''" + Uri.EscapeDataString(m.FileName) +
                            "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
                        s.Write(h, 0, h.Length);
                        s.Write(bytes, 0, bytes.Length);
                        return;
                    }
                    Reply(s, 404, "text/plain", "not found");
                }
                catch (Exception e) { Log.Write("lan share: " + e.Message); }
            }
        }

        static string ReadHead(Stream s, out byte[] rest)
        {
            var buf = new byte[16384];
            int total = 0;
            rest = new byte[0];
            while (total < buf.Length)
            {
                int n = s.Read(buf, total, buf.Length - total);
                if (n <= 0) return null;
                total += n;
                for (int i = 3; i < total; i++)
                    if (buf[i - 3] == '\r' && buf[i - 2] == '\n' && buf[i - 1] == '\r' && buf[i] == '\n')
                    {
                        rest = buf.Skip(i + 1).Take(total - i - 1).ToArray();
                        return Encoding.ASCII.GetString(buf, 0, i - 3);
                    }
            }
            return null;
        }

        static void Reply(Stream s, int code, string type, string body)
        {
            var b = Encoding.UTF8.GetBytes(body);
            var status = code == 200 ? "OK" : code == 404 ? "Not Found" : "Error";
            var h = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " " + status + "\r\nContent-Type: " + type + "\r\nContent-Length: " + b.Length +
                                            "\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n");
            s.Write(h, 0, h.Length);
            s.Write(b, 0, b.Length);
        }

        static string NewToken()
        {
            const string a = "abcdefghjkmnpqrstuvwxyz23456789";
            var bytes = new byte[10];
            using (var r = RandomNumberGenerator.Create()) r.GetBytes(bytes);
            return new string(bytes.Select(x => a[x % a.Length]).ToArray());
        }

        /// <summary>The most likely Wi-Fi / Ethernet IPv4 address (private ranges, physical adapters first).</summary>
        public static string LanAddress()
        {
            var best = new List<Tuple<int, string>>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                var name = (ni.Name + " " + ni.Description).ToLowerInvariant();
                bool virt = name.Contains("virtual") || name.Contains("vmware") || name.Contains("vethernet") || name.Contains("hyper-v")
                            || name.Contains("vpn") || name.Contains("tap") || name.Contains("wsl") || name.Contains("docker") || name.Contains("zerotier") || name.Contains("tailscale");
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var b = ua.Address.GetAddressBytes();
                    bool priv = b[0] == 192 && b[1] == 168 || b[0] == 10 || b[0] == 172 && b[1] >= 16 && b[1] <= 31;
                    if (!priv) continue;
                    int score = (virt ? 10 : 0) + (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 : 1) + (b[0] == 192 ? 0 : 2);
                    best.Add(Tuple.Create(score, ua.Address.ToString()));
                }
            }
            return best.OrderBy(t => t.Item1).Select(t => t.Item2).FirstOrDefault();
        }

        static string Page() => @"<!doctype html><html lang=zh-CN><head><meta charset=utf-8>
<meta name=viewport content='width=device-width,initial-scale=1'><title>一维输入法 · 互传</title>
<style>
*{box-sizing:border-box}body{margin:0;font-family:-apple-system,'PingFang SC','Microsoft YaHei',sans-serif;background:#F4F6FB;color:#1C2233}
header{background:#2B5BD7;color:#fff;padding:18px 20px;font-size:18px;font-weight:600}
main{padding:16px;max-width:640px;margin:auto}
textarea{width:100%;min-height:120px;border:1px solid #D5DBEA;border-radius:14px;padding:12px;font-size:16px;resize:vertical;background:#fff}
button{border:0;border-radius:999px;background:#2B5BD7;color:#fff;font-size:16px;padding:12px 22px;margin-top:10px;width:100%}
button.s{background:#fff;color:#2B5BD7;border:1px solid #2B5BD7;width:auto;padding:6px 14px;font-size:14px;margin:6px 0 0}
h2{font-size:14px;color:#5B6478;margin:22px 4px 8px;font-weight:600}
.m{background:#fff;border-radius:14px;padding:12px;margin-bottom:10px;white-space:pre-wrap;word-break:break-all;font-size:15px}
.t{color:#8A93A6;font-size:12px}#ok{color:#2B5BD7;font-size:13px;height:18px;margin-top:6px}
</style></head><body><header>一维输入法 · 局域网互传</header><main>
<textarea id=tx placeholder='输入或粘贴文字，发到电脑（电脑上会自动复制）'></textarea>
<button onclick=send()>发送到电脑</button><div id=ok></div>
<h2>电脑发来的</h2><div id=list><div class=t>电脑在「局域网互传」窗口里发送的文字会出现在这里</div></div>
</main><script>
var last=0,first=true;
function send(){var t=document.getElementById('tx').value;if(!t.trim())return;
fetch('send',{method:'POST',headers:{'Content-Type':'text/plain;charset=utf-8'},body:t}).then(function(){document.getElementById('tx').value='';flash('已发送')}).catch(function(){flash('发送失败，请确认和电脑在同一网络')})}
function flash(s){var o=document.getElementById('ok');o.textContent=s;setTimeout(function(){o.textContent=''},2000)}
function copy(btn){var r=document.createRange();r.selectNodeContents(btn.previousSibling);var s=getSelection();s.removeAllRanges();s.addRange(r);
try{document.execCommand('copy');btn.textContent='已复制'}catch(e){btn.textContent='长按文字复制'}}
function pull(){fetch('pull?after='+last).then(function(r){return r.json()}).then(function(a){var l=document.getElementById('list');
if(a.length&&first){l.innerHTML=''}a.forEach(function(m){first=false;last=m.id;var d=document.createElement('div');d.className='m';
var p=document.createElement('div');p.textContent=m.text;var b=document.createElement('button');b.className='s';
if(m.file){b.textContent='下载';b.onclick=function(){location.href='file/'+m.id}}else{b.textContent='复制';b.onclick=function(){copy(b)}}
var t=document.createElement('div');t.className='t';t.textContent=m.at;d.appendChild(t);d.appendChild(p);d.appendChild(b);l.insertBefore(d,l.firstChild)})}).catch(function(){})}
setInterval(pull,1500);pull();
</script></body></html>";
    }
}
