using System;
using System.Collections.Generic;
using System.Linq;
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
                "每天检查一次一维每周词库包（镜像雾凇，取不到时回退 iDvel/rime-ice 官方发布）（只访问 GitHub）。SHA-256 校验通过才安装，先备份旧词库，失败自动回滚",
                K.Toggle(S.DictAutoUpdate, v => { S.DictAutoUpdate = v; Save(); }), Ui.SymbolRegular.ArrowDownload24));

            p.Children.Add(K.Card("词库大小",
                "标准：不含腾讯词向量大词库（约 50 万个不常用词），部署快好几倍、占内存少，适合老电脑；完整：雾凇全部词库（默认）。切换后自动重新部署",
                K.Combo(new[] { new System.Collections.Generic.KeyValuePair<bool, string>(false, "完整"), new System.Collections.Generic.KeyValuePair<bool, string>(true, "标准（更快）") },
                    S.LiteDict, v => { S.LiteDict = v; Save(); if (DictLite.Apply(v)) Rime.ApplySoon(100); }, 160),
                Ui.SymbolRegular.Library24));

            p.Children.Add(K.Card("中英混输词条",
                "打 ppt、qq、icka、cwei 直接出 PPT、QQ、IC卡、C位 这类常用词，排在第 2 位、不抢首选（选多了会自动往前排）。切换后自动重新部署",
                K.Toggle(S.MixedWords, v => { S.MixedWords = v; Save(); if (MixedWords.Apply(v)) Rime.ApplySoon(100); }), Ui.SymbolRegular.TextT24));

            p.Children.Add(K.Card("分类词库",
                "一维按领域整理的词库：地名、国家首都、诗词、成语、历史、术语、口语短句等，权重按真实词频校准、不抢常用词首选。切换后自动重新部署",
                K.Toggle(S.DomainDictsOn, v => { S.DomainDictsOn = v; Save(); if (DomainDicts.Apply()) Rime.ApplySoon(100); }), Ui.SymbolRegular.Library24));
            p.Children.Add(K.Block("分类词库：按领域", "取消勾选的领域不再挂载（总开关打开时才生效）", DomainChoices(), Ui.SymbolRegular.Library24));
            p.Children.Add(K.Card("诗句联想",
                "打出诗句上句时，第 2 个候选是整联（注明作者和诗题）；上句上屏后接着打下句首字母（如 ysdss）直接出下句",
                K.Toggle(CoreInput.Poetry, v => { CoreInput.Poetry = v; Toast(v ? "已开启诗句联想" : "已关闭诗句联想"); }), Ui.SymbolRegular.BookOpen24));

            p.Children.Add(K.Block("我的词条", "一行一条「词 拼音 权重」，拼音和权重可省（拼音自动注音，权重默认 1000000，数字越大越靠前）。例：一维输入法 yi wei shu ru fa。保存后自动重新部署，雾凇每日更新不影响",
                CustomWordsEditor(), Ui.SymbolRegular.BookAdd24));

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

        WrapPanel DomainChoices()
        {
            var wrap = new WrapPanel();
            foreach (var d in DomainDicts.All)
            {
                var id = d.Id;
                var cb = new CheckBox { Content = d.Name, ToolTip = d.Desc, IsChecked = !DomainDicts.OffSet(S.DomainDictsOff).Contains(id), MinWidth = 96, Margin = new Thickness(0, 0, 12, 6) };
                RoutedEventHandler h = (s, e) =>
                {
                    DomainDicts.SetDomain(id, cb.IsChecked == true);
                    Save();
                    if (DomainDicts.Apply()) Rime.ApplySoon(1500); // 连续勾选时合并成一次部署
                };
                cb.Checked += h; cb.Unchecked += h;
                wrap.Children.Add(cb);
            }
            return wrap;
        }

        CancellationTokenSource _grammarCts;

        FrameworkElement CustomWordsEditor()
        {
            var box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, MaxHeight = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            try { box.Text = CustomWords.ToText(CustomWords.Load()); } catch (Exception ex) { Log.Write("custom words load: " + ex.Message); }
            var msg = K.Text("", 12, true);
            msg.TextWrapping = TextWrapping.Wrap;
            Ui.Button save = null;
            save = K.Btn("保存并生效", async () =>
            {
                var errors = new List<string>();
                List<CustomWords.Entry> entries = null;
                save.IsEnabled = false; msg.Text = "正在检查并注音…";
                try { entries = await System.Threading.Tasks.Task.Run(() => CustomWords.Parse(box.Text, errors)); }
                catch (Exception ex) { errors.Add("检查失败：" + ex.Message); }
                if (errors.Count > 0)
                {
                    msg.Text = "没保存，请先改这几行：\n" + string.Join("\n", errors.Take(6)) + (errors.Count > 6 ? "\n…还有 " + (errors.Count - 6) + " 处" : "");
                    save.IsEnabled = true; return;
                }
                try
                {
                    CustomWords.Save(entries);
                    box.Text = CustomWords.ToText(entries); // 回显自动补上的拼音
                    msg.Text = entries.Count == 0 ? "已清空" : "已保存 " + entries.Count + " 条，正在重新部署…";
                    Rime.ApplySoon(100);
                    Toast(msg.Text);
                }
                catch (Exception ex) { msg.Text = "保存失败：" + ex.Message; Log.Write("custom words save: " + ex); }
                save.IsEnabled = true;
            });
            var imp = K.Btn("导入 txt…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "文本文件 (*.txt)|*.txt|所有文件|*.*", Title = "导入词条（一行一条：词 拼音 权重）" };
                if (dlg.ShowDialog() != true) return;
                try
                {
                    var text = System.IO.File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
                    int n = text.Split('\n').Count(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("#"));
                    box.Text = (box.Text.Length > 0 && !box.Text.EndsWith("\n") ? box.Text + "\r\n" : box.Text) + text.TrimEnd();
                    msg.Text = "已读入 " + n + " 行，检查后点「保存并生效」（重复的词会自动去掉）";
                }
                catch (Exception ex) { msg.Text = "导入失败：" + ex.Message; Log.Write("custom words import: " + ex.Message); }
            });
            var learn = K.Btn("从文章学词…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "文本文件 (*.txt;*.md)|*.txt;*.md|所有文件|*.*", Title = "选一篇你常写的文章，找出反复出现的词" };
                if (dlg.ShowDialog() != true) return;
                try
                {
                    var words = TextLearn.Extract(System.IO.File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8));
                    var have = new HashSet<string>(box.Text.Split('\n').Select(l => l.Trim().Split(' ', '\t')[0]));
                    words = words.Where(w => !have.Contains(w)).ToList();
                    if (words.Count == 0) { msg.Text = "没找到反复出现（≥3 次）的新词"; return; }
                    box.Text = (box.Text.Length > 0 && !box.Text.EndsWith("\n") ? box.Text + "\r\n" : box.Text) + string.Join("\r\n", words);
                    msg.Text = "找到 " + words.Count + " 个常出现的词已加到末尾，删掉不要的行，再点「保存并生效」（拼音自动注音）";
                }
                catch (Exception ex) { msg.Text = "学词失败：" + ex.Message; Log.Write("text learn: " + ex.Message); }
            });
            var exp = K.Btn("导出 txt…", () =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "文本文件 (*.txt)|*.txt", FileName = "我的词条.txt", Title = "导出词条" };
                if (dlg.ShowDialog() != true) return;
                try { System.IO.File.WriteAllText(dlg.FileName, box.Text, new System.Text.UTF8Encoding(false)); msg.Text = "已导出到 " + dlg.FileName; }
                catch (Exception ex) { msg.Text = "导出失败：" + ex.Message; Log.Write("custom words export: " + ex.Message); }
            });
            var sp = new StackPanel();
            sp.Children.Add(box);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            imp.Margin = new Thickness(8, 0, 0, 0); exp.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(save); row.Children.Add(imp); row.Children.Add(exp); learn.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(learn);
            sp.Children.Add(row);
            msg.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(msg);
            return sp;
        }

        void AddGrammarCards(StackPanel p)
        {
            var info = K.Text("", 12, true);
            var bar = new ProgressBar { Height = 4, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
            Ui.Button get = null, del = null;

            void Refresh()
            {
                bool on = Grammar.Installed;
                bool upd = on && Grammar.UpdateAvailable;
                var part = Grammar.PartialBytes;
                info.Text = on
                    ? "已启用：" + Grammar.FileName + "（" + Grammar.SizeText(new System.IO.FileInfo(Grammar.ModelPath).Length) + "）" +
                      (upd ? "。作者发布了新版（" + Grammar.SizeText(Grammar.ExpectedSize) + "），可以更新" : "")
                    : part > 0 ? "上次下载未完成（已下载 " + Grammar.SizeText(part) + "），可以继续"
                    : "未安装（推荐下载：长句准确率明显更高，下载后完全离线使用）。模型来自万象 amzxyz/rime-wanxiang-data，CC BY 4.0，只访问 GitHub";
                get.Content = upd ? "更新模型（约 " + Grammar.SizeText(Grammar.ExpectedSize) + "）" : on ? "重新下载" : part > 0 ? "继续下载" : "下载语法模型（约 " + Grammar.SizeText(Grammar.ExpectedSize) + "）";
                get.Visibility = on && !upd ? Visibility.Collapsed : Visibility.Visible;
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
            // 打开本页时问一次 GitHub 模型有没有更新（上游原地覆盖 LTS 文件，本地大小对不上才知道）；失败不提示
            if (Grammar.Installed && !Grammar.Busy)
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try { await Grammar.CheckUpdate(); } catch (Exception ex) { Log.Write("grammar check: " + ex.GetBaseException().Message); return; }
                    await Dispatcher.InvokeAsync(() => { if (_grammarCts == null) Refresh(); });
                });

            var card = K.Card("语法模型（万象，推荐）", "长句更准：librime-octagram 用它给整句打分。约 400 MB，按需下载，不强制；下载后完全在本机运行", K.Row(get, del), Ui.SymbolRegular.TextGrammarWand24);
            var texts = (StackPanel)((Grid)card.Child).Children[1];
            texts.Children.Add(info);
            texts.Children.Add(bar);
            p.Children.Add(card);
        }
    }
}
