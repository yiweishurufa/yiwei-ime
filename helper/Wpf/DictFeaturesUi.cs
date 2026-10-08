using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>「词库」页：词库自动更新与语法模型的卡片（逻辑在 Features/DictUpdate.cs、Features/Grammar.cs）。</summary>
    sealed partial class SettingsWindow
    {
        void AddDictUpdateCards(StackPanel p)
        {
            p.Children.Add(K.Card("每天自动更新雾凇拼音词库",
                "每天检查一次 iDvel/rime-ice 的 GitHub 发布（只访问 GitHub）。SHA-256 校验通过才安装，先备份旧词库，失败自动回滚",
                K.Toggle(S.DictAutoUpdate, v => { S.DictAutoUpdate = v; Save(); }), Ui.SymbolRegular.ArrowDownload24));

            var info = K.Text(DictUpdater.Describe(), 12, true);
            var err = DictUpdater.Load();
            if (!string.IsNullOrEmpty(err.LastError) && DateTime.TryParse(err.LastErrorAt, out var et))
                info.Text += "\n上次失败（" + et.ToString("M月d日 HH:mm") + "）：" + err.LastError;

            Ui.Button now = null, back = null;
            var status = new Progress<string>(t => info.Text = t);
            now = K.Btn("立即检查", async () =>
            {
                now.IsEnabled = false; back.IsEnabled = false;
                try
                {
                    var r = await DictUpdater.Check(false, status);
                    info.Text = r.Message + "\n" + DictUpdater.Describe();
                    Toast(r.Message);
                }
                catch (Exception ex) { info.Text = "更新失败：" + ex.GetBaseException().Message + "\n" + DictUpdater.Describe(); }
                now.IsEnabled = true; back.IsEnabled = DictUpdater.CanRollback;
            }, Ui.ControlAppearance.Primary);
            back = K.Btn("回滚上一版", async () =>
            {
                if (!Dialogs.Confirm("把词库恢复到更新前的版本，并重新部署？")) return;
                now.IsEnabled = false; back.IsEnabled = false; info.Text = "正在回滚并重新部署…";
                try { var m = await DictUpdater.Rollback(); info.Text = m + "\n" + DictUpdater.Describe(); Toast(m); }
                catch (Exception ex) { info.Text = "回滚失败：" + ex.GetBaseException().Message; }
                now.IsEnabled = true; back.IsEnabled = DictUpdater.CanRollback;
            });
            back.IsEnabled = DictUpdater.CanRollback;

            var card = K.Card("词库版本", "", K.Row(now, back), Ui.SymbolRegular.BookOpen24);
            ((StackPanel)((Grid)card.Child).Children[1]).Children.Add(info);
            p.Children.Add(card);
        }

        CancellationTokenSource _grammarCts;

        void AddGrammarCards(StackPanel p)
        {
            var info = K.Text("", 12, true);
            var bar = new ProgressBar { Height = 4, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
            Ui.Button get = null, del = null;

            void Refresh()
            {
                bool on = Grammar.Installed;
                var part = Grammar.PartialBytes;
                info.Text = on
                    ? "已启用：" + Grammar.FileName + "（" + Grammar.SizeText(new System.IO.FileInfo(Grammar.ModelPath).Length) + "）"
                    : part > 0 ? "上次下载未完成（已下载 " + Grammar.SizeText(part) + "），可以继续" : "未安装。模型来自 amzxyz/RIME-LMDG（只访问 GitHub）";
                get.Content = on ? "重新下载" : part > 0 ? "继续下载" : "下载语法模型（约 " + Grammar.SizeText(Grammar.KnownSize) + "）";
                get.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
                del.Visibility = on || part > 0 ? Visibility.Visible : Visibility.Collapsed;
                del.Content = on ? "删除并恢复" : "删除已下载部分";
            }

            get = K.Btn("下载语法模型", async () =>
            {
                if (_grammarCts != null) { _grammarCts.Cancel(); return; } // second click cancels
                _grammarCts = new CancellationTokenSource();
                get.Content = "取消下载"; del.Visibility = Visibility.Collapsed;
                bar.Value = 0; bar.Visibility = Visibility.Visible;
                var pr = new Progress<double>(v => { bar.Value = v; info.Text = "正在下载… " + (int)(v * 100) + "%"; });
                var st = new Progress<string>(t => info.Text = t);
                string done = null;
                try { await Grammar.Install(pr, st, _grammarCts.Token); done = "语法模型已启用，长句更准"; }
                catch (OperationCanceledException) { done = "已取消下载，下次可以继续"; }
                catch (Exception ex) { Log.Write("grammar: " + ex.GetBaseException().Message); done = "下载失败：" + ex.GetBaseException().Message; }
                _grammarCts.Dispose(); _grammarCts = null;
                bar.Visibility = Visibility.Collapsed;
                Refresh();
                info.Text = done + "\n" + info.Text;
            }, Ui.ControlAppearance.Primary, Ui.SymbolRegular.ArrowDownload24);
            del = K.Btn("删除并恢复", async () =>
            {
                if (Grammar.Installed && !Dialogs.Confirm("删除语法模型并恢复为普通组句？\n\n会删除 " + Grammar.FileName + " 并去掉方案里的语法补丁，然后重新部署。")) return;
                del.IsEnabled = false;
                try { await Grammar.Remove(); Toast("已删除语法模型，正在重新部署…"); }
                catch (Exception ex) { Dialogs.Error(ex.GetBaseException().Message); }
                del.IsEnabled = true;
                Refresh();
            });
            Refresh();

            var card = K.Card("语法模型（万象 LMDG）", "长句更准：librime-octagram 用它给整句打分。模型较大，按需下载", K.Row(get, del), Ui.SymbolRegular.TextGrammarWand24);
            var texts = (StackPanel)((Grid)card.Child).Children[1];
            texts.Children.Add(info);
            texts.Children.Add(bar);
            p.Children.Add(card);
        }
    }
}
