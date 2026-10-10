using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>「语音」页：离线语音输入的模型下载、按键与整理选项（逻辑在 Features/Voice.cs）。</summary>
    sealed partial class SettingsWindow
    {
        CancellationTokenSource _voiceCts;

        StackPanel PageVoice()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("语音"));
            p.Children.Add(K.Text("按住说话，松开上屏。识别完全在本机进行，声音不出电脑。", 13, true));

            // ---- 模型 ----
            var info = K.Text("", 12, true);
            var bar = new ProgressBar { Height = 4, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
            Ui.Button get = null, del = null;
            void Refresh()
            {
                bool on = VoiceModel.Installed;
                var part = VoiceModel.PartialBytes;
                info.Text = on
                    ? "已就绪：sherpa-onnx " + VoiceModel.EngineVersion + " + SenseVoice-Small int8（占用 " + Grammar.SizeText(VoiceModel.DiskBytes) + "）"
                    : part > 0 ? "上次下载未完成（已下载 " + Grammar.SizeText(part) + "），可以继续"
                    : "未安装。识别程序与模型来自 k2-fsa/sherpa-onnx 的 GitHub 发布，国内自动走加速镜像，SHA-256 校验";
                get.Content = part > 0 ? "继续下载" : "下载语音模型（约 " + Grammar.SizeText(VoiceModel.TotalSize) + "）";
                get.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
                del.Visibility = on || part > 0 ? Visibility.Visible : Visibility.Collapsed;
                del.Content = on ? "删除模型" : "删除已下载部分";
            }
            get = K.Btn("下载语音模型", async () =>
            {
                if (_voiceCts != null) { _voiceCts.Cancel(); return; } // second click cancels
                _voiceCts = new CancellationTokenSource();
                get.Content = "取消下载"; del.Visibility = Visibility.Collapsed;
                bar.Value = 0; bar.Visibility = Visibility.Visible;
                var pr = new Progress<double>(v => bar.Value = v);
                var st = new Progress<string>(t => info.Text = t);
                string done;
                try { await VoiceModel.Install(pr, st, _voiceCts.Token); done = "语音输入已就绪：按住" + KeyName(S.VoiceKey) + "说话试试"; }
                catch (OperationCanceledException) { done = "已取消下载，下次可以继续"; }
                catch (Exception ex) { Log.Write("voice: " + ex.GetBaseException().Message); done = "下载失败：" + ex.GetBaseException().Message; }
                _voiceCts.Dispose(); _voiceCts = null;
                bar.Visibility = Visibility.Collapsed;
                Refresh();
                info.Text = done + "\n" + info.Text;
            }, Ui.ControlAppearance.Primary, Ui.SymbolRegular.ArrowDownload24);
            del = K.Btn("删除模型", async () =>
            {
                if (VoiceModel.Installed && !Dialogs.Confirm("删除语音识别程序和模型？之后按住说话不再可用，可以随时重新下载。")) return;
                del.IsEnabled = false;
                try { await VoiceModel.Remove(); Toast("已删除语音模型"); }
                catch (Exception ex) { Dialogs.Error(ex.GetBaseException().Message); }
                del.IsEnabled = true;
                Refresh();
            });
            Refresh();
            var card = K.Card("离线语音模型", "SenseVoice：普通话、英语、粤语、日语、韩语，自带标点；CPU 即可实时识别", K.Row(get, del), Ui.SymbolRegular.Mic24);
            var texts = (StackPanel)((Grid)card.Child).Children[1];
            texts.Children.Add(info);
            texts.Children.Add(bar);
            p.Children.Add(card);

            // ---- 按键与选项 ----
            p.Children.Add(K.Section("按住说话"));
            p.Children.Add(K.Card("说话键", "按住开始录音（胶囊出现在光标旁），松开识别上屏；Esc 取消。按住时按了别的键则当作普通快捷键",
                K.Segmented(new[]
                {
                    new KeyValuePair<string, string>("rctrl", "右 Ctrl"),
                    new KeyValuePair<string, string>("ralt", "右 Alt"),
                    new KeyValuePair<string, string>("off", "关闭"),
                }, S.VoiceKey, v => { S.VoiceKey = v; Save(); }), Ui.SymbolRegular.Keyboard24));
            p.Children.Add(K.Card("识别语言", "自动判断即可；只说一种语言时固定下来更准",
                K.Combo(new[]
                {
                    new KeyValuePair<string, string>("auto", "自动"),
                    new KeyValuePair<string, string>("zh", "普通话"),
                    new KeyValuePair<string, string>("en", "英语"),
                    new KeyValuePair<string, string>("yue", "粤语"),
                    new KeyValuePair<string, string>("ja", "日语"),
                    new KeyValuePair<string, string>("ko", "韩语"),
                }, S.VoiceLanguage, v => { S.VoiceLanguage = v; Save(); }, 160), Ui.SymbolRegular.Globe24));
            p.Children.Add(K.Card("去掉口水词", "删掉句首和标点后的「嗯、呃、啊」",
                K.Toggle(S.VoiceRemoveFillers, v => { S.VoiceRemoveFillers = v; Save(); }), Ui.SymbolRegular.TextClearFormatting24));
            p.Children.Add(K.Card("中英文之间加空格", "例如「用 Python 写」",
                K.Toggle(S.VoicePanguSpacing, v => { S.VoicePanguSpacing = v; Save(); }), Ui.SymbolRegular.Translate24));
            p.Children.Add(K.Text("游戏、远程桌面等「不响应 Alt 手势的应用」里同样不会触发语音。第一次使用时 Windows 可能需要你在「隐私和安全性 → 麦克风」里允许桌面应用访问麦克风。", 12, true));
            return p;
        }

        static string KeyName(string key) => key == "ralt" ? "右 Alt " : key == "off" ? "说话键（当前已关闭）" : "右 Ctrl ";
    }
}
