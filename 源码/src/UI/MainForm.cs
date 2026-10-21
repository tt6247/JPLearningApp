using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace KanaSmallSteps
{
    public sealed partial class MainForm : Form
    {
        private readonly Progress State;

        private readonly Panel Canvas;

        private readonly Label Title;

        private readonly Label Subtitle;

        private readonly Label Status;

        // 换主题时需要重新上色的常驻控件
        private readonly Panel StatusBar;
        private readonly Panel Sidebar;
        private readonly Label BrandLabel;
        private readonly Label TagLabel;
        private readonly Label NoteLabel;
        private Button ThemeButton;

        private readonly List<Button> Nav = new List<Button>();

        private TableLayoutPanel Stack;

        private string Page = "today";

        private string Category = "basic";

        private string Scope = "basic";

        private List<Kana> Deck = new List<Kana>();

        private int CardIndex;

        private bool Flipped;

        private Quiz Test;

        private readonly OfflineAudio Audio;

        private float DpiFactor = 1f;

        /// <summary>上一次显示笔顺的假名：同一张卡翻面时不重播动画。</summary>
        private string LastStrokeKana;

        public MainForm(Progress p)
        {
            base.AutoScaleMode = AutoScaleMode.None;
            State = p;
            Audio = new OfflineAudio(Path.Combine(Path.GetDirectoryName(p.Path), "audio"));
            Theme.Apply(p.Theme);
            Text = AppTitle;
            Font = Theme.Font(10f, false);
            BackColor = Theme.Paper;
            base.StartPosition = FormStartPosition.CenterScreen;
            base.ClientSize = new Size(1120, 780);
            MinimumSize = new Size(940, 650);
            // 状态栏（自绘面板，便于跟随深浅色）
            StatusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.Surface, Padding = new Padding(12, 0, 12, 0) };
            StatusBar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Line))
                {
                    e.Graphics.DrawLine(pen, 0, 0, StatusBar.Width, 0);
                }
            };
            Status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, Font = Theme.Font(9f, false), UseMnemonic = false };
            StatusBar.Controls.Add(Status);
            base.Controls.Add(StatusBar);
            TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205f));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            base.Controls.Add(tableLayoutPanel);
            tableLayoutPanel.BringToFront();
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                Padding = new Padding(18, 25, 18, 15)
            };
            Sidebar = panel;
            tableLayoutPanel.Controls.Add(panel, 0, 0);
            FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false
            };
            panel.Controls.Add(flowLayoutPanel);
            BrandLabel = new Label
            {
                Text = "あ  假名小步",
                Font = Theme.Font(20f, true),
                ForeColor = Theme.Purple,
                Width = 170,
                Height = 54
            };
            flowLayoutPanel.Controls.Add(BrandLabel);
            TagLabel = new Label
            {
                Text = "HIRAGANA / 桌面版",
                Font = Theme.Font(9f, false),
                ForeColor = Theme.Muted,
                Width = 170,
                Height = 30
            };
            flowLayoutPanel.Controls.Add(TagLabel);
            string[,] array = new string[7, 2]
            {
                { "today", "今日计划" },
                { "table", "五十音表" },
                { "groups", "分组学习" },
                { "cards", "记忆卡" },
                { "quiz", "辨认测验" },
                { "errors", "错题复习" },
                { "help", "学习指南" }
            };
            for (int i = 0; i < array.GetLength(0); i++)
            {
                string id = array[i, 0];
                Button button = Theme.Button(array[i, 1], delegate
                {
                    Navigate(id);
                }, false);
                button.AutoSize = false;
                button.Size = new Size(165, 44);
                button.TextAlign = ContentAlignment.MiddleLeft;
                button.Tag = id;
                flowLayoutPanel.Controls.Add(button);
                Nav.Add(button);
            }
            BuildSidebarTimer(flowLayoutPanel);
            ThemeButton = Theme.Button("", CycleTheme, false);
            ThemeButton.AutoSize = false;
            ThemeButton.MinimumSize = Size.Empty;
            ThemeButton.Size = new Size(165, 34);
            ThemeButton.Margin = new Padding(0, 10, 0, 0);
            ThemeButton.Font = Theme.Font(9f, false);
            ThemeButton.TextAlign = ContentAlignment.MiddleLeft;
            flowLayoutPanel.Controls.Add(ThemeButton);
            NoteLabel = new Label
            {
                Text = "每天一小步，读懂新世界。\n离线学习 · 本地进度",
                ForeColor = Theme.Muted,
                Width = 165,
                Height = 50,
                Margin = new Padding(8, 14, 0, 0),
                Font = Theme.Font(10f, false)
            };
            flowLayoutPanel.Controls.Add(NoteLabel);
            Panel panel2 = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(25, 20, 23, 12)
            };
            tableLayoutPanel.Controls.Add(panel2, 1, 0);
            Panel panel3 = new Panel
            {
                Dock = DockStyle.Top,
                Height = 112
            };
            Title = Theme.Label("", 25f, true, 50);
            Subtitle = Theme.Label("", 10f, false, 46);
            Subtitle.Dock = DockStyle.Bottom;
            panel3.Controls.Add(Title);
            panel3.Controls.Add(Subtitle);
            Canvas = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            panel2.Controls.Add(Canvas);
            panel2.Controls.Add(panel3);
            base.FormClosed += delegate
            {
                Audio.Dispose();
            };
            InitTheme();
            InitTimer();
            Render();
            using (Graphics graphics = CreateGraphics())
            {
                DpiFactor = graphics.DpiX / 96f;
                if (DpiFactor != 1f)
                {
                    Scale(new SizeF(DpiFactor, DpiFactor));
                }
            }
        }

        private void BeginPage(string title, string subtitle)
        {
            ClearTodayRefs();
            Title.Text = title;
            Subtitle.Text = subtitle;
            Canvas.SuspendLayout();
            while (Canvas.Controls.Count > 0)
            {
                Control control = Canvas.Controls[0];
                Canvas.Controls.RemoveAt(0);
                control.Dispose();
            }
            Canvas.AutoScrollPosition = Point.Empty;
            Stack = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 0,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 6, 12),
                Margin = new Padding(0)
            };
            Stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Canvas.Controls.Add(Stack);
        }

        private void Add(Control c)
        {
            int row = Stack.RowCount++;
            Stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            c.Dock = DockStyle.Top;
            c.Margin = new Padding(0, 0, 0, 14);
            Stack.Controls.Add(c, 0, row);
        }

        private FlowLayoutPanel Actions(params Control[] controls)
        {
            FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
            flowLayoutPanel.AutoSize = true;
            flowLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowLayoutPanel.WrapContents = true;
            flowLayoutPanel.Padding = new Padding(0, 3, 0, 3);
            flowLayoutPanel.Dock = DockStyle.Top;
            FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
            flowLayoutPanel2.Controls.AddRange(controls);
            return flowLayoutPanel2;
        }

        private void FinishPage()
        {
            if (DpiFactor != 1f)
            {
                Stack.Scale(new SizeF(DpiFactor, DpiFactor));
            }
            Canvas.ResumeLayout(performLayout: true);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            Status.Text = ((State.Warning.Length > 0) ? State.Warning : ("进度自动保存到本机  ·  已熟悉 " + State.Learned.Count + " / 104  ·  待复习 " + State.Errors.Count + " 个  ·  今日专注 " + (int)(State.Today.Seconds / 60) + " 分钟"));
            Status.ForeColor = ((State.Warning.Length > 0) ? Theme.BadFg : Theme.Muted);
        }

        private void Save()
        {
            State.Save();
            RefreshStatus();
        }

        public void Navigate(string page)
        {
            Page = page;
            if (page == "cards")
            {
                ResetDeck();
            }
            if (page == "quiz")
            {
                Test = new Quiz(Scope, State);
            }
            Render();
        }

        public void Render()
        {
            foreach (Button item in Nav)
            {
                bool flag = (string)item.Tag == Page;
                item.BackColor = (flag ? Theme.Light : Theme.Surface);
                item.ForeColor = (flag ? Theme.Purple : Theme.Muted);
            }
            if (Page == "today")
            {
                RenderToday();
            }
            else if (Page == "table")
            {
                RenderTable();
            }
            else if (Page == "groups")
            {
                RenderGroups();
            }
            else if (Page == "cards")
            {
                RenderCards();
            }
            else if (Page == "quiz")
            {
                RenderQuiz();
            }
            else if (Page == "errors")
            {
                RenderErrors();
            }
            else
            {
                RenderHelp();
            }
        }

        private FlowLayoutPanel Categories()
        {
            string[,] array = new string[3, 2]
            {
                { "basic", "清音 · 46" },
                { "voiced", "浊音 / 半浊音 · 25" },
                { "combo", "常用拗音 · 33" }
            };
            FlowLayoutPanel flowLayoutPanel = Actions();
            for (int i = 0; i < 3; i++)
            {
                string id = array[i, 0];
                flowLayoutPanel.Controls.Add(Theme.Button(array[i, 1], delegate
                {
                    Category = id;
                    Render();
                }, Category == id));
            }
            return flowLayoutPanel;
        }

        private void RenderStats()
        {
            StatsRow(new string[3] { "清音已熟悉", "测验正确率", "待复习假名" }, new string[3]
            {
                Data.Pool("basic", State).Count((Kana x) => State.Learned.Contains(x.K)) + " / 46",
                (State.Answered > 0) ? (Math.Round(100.0 * (double)State.Correct / (double)State.Answered) + "%  ·  " + State.Answered + "题") : "尚未测验",
                State.Errors.Count + " 个"
            });
        }

        /// <summary>三格统计卡片，返回数值标签（便于之后刷新）。</summary>
        private Label[] StatsRow(string[] array, string[] array2)
        {
            TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel { ColumnCount = array.Length, RowCount = 1, Height = 93 };
            Label[] values = new Label[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
                Panel panel = new Panel();
                panel.Dock = DockStyle.Fill;
                panel.BackColor = Theme.Surface;
                panel.Padding = new Padding(14, 8, 12, 7);
                panel.Margin = new Padding(0, 0, 10, 0);
                Panel panel2 = panel;
                Label value = Theme.Label(array[i], 9f, false, 25);
                Label label = Theme.Label(array2[i], 17f, true, 36);
                label.Dock = DockStyle.Bottom;
                values[i] = label;
                panel2.Controls.Add(value);
                panel2.Controls.Add(label);
                tableLayoutPanel2.Controls.Add(panel2, i, 0);
            }
            Add(tableLayoutPanel2);
            return values;
        }

        private void RenderTable()
        {
            BeginPage("从「あ」开始，读懂日语。", "点开一个假名，查看读音、听发音，再标记熟悉。今天先学一行就好。");
            RenderStats();
            Add(Categories());
            List<KanaGroup> list = Data.Groups.Where((KanaGroup g) => g.Type == Category).ToList();
            int num = ((Category == "combo") ? 3 : 5);
            TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
            tableLayoutPanel.ColumnCount = num + 1;
            tableLayoutPanel.RowCount = list.Count + 1;
            tableLayoutPanel.Height = 28 + list.Count * 84;
            tableLayoutPanel.BackColor = Theme.Paper;
            tableLayoutPanel.Margin = new Padding(0);
            TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58f));
            for (int i = 0; i < num; i++)
            {
                tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / (float)num));
            }
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            string[] array = ((num == 3) ? new string[3] { "ya", "yu", "yo" } : new string[5] { "a", "i", "u", "e", "o" });
            for (int j = 0; j < num; j++)
            {
                Label label = Theme.Label(array[j], 9f, false, 25);
                label.TextAlign = ContentAlignment.MiddleCenter;
                tableLayoutPanel2.Controls.Add(label, j + 1, 0);
            }
            for (int k = 0; k < list.Count; k++)
            {
                KanaGroup kanaGroup = list[k];
                tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 84f));
                Label label2 = Theme.Label(kanaGroup.Name, 9f, false, 80);
                label2.Dock = DockStyle.Fill;
                tableLayoutPanel2.Controls.Add(label2, 0, k + 1);
                foreach (Kana item2 in kanaGroup.Items)
                {
                    Kana item = item2;
                    KanaTile kanaTile = new KanaTile(item2, State.Learned.Contains(item2.K));
                    kanaTile.Click += delegate
                    {
                        ShowDetail(item);
                    };
                    tableLayoutPanel2.Controls.Add(kanaTile, item2.Column + 1, k + 1);
                }
            }
            Add(tableLayoutPanel2);
            Add(Actions(Theme.Button("学习当前范围 →", delegate
            {
                StartCards(Category);
            }, true), Theme.Button("零基础学习指南", delegate
            {
                Navigate("help");
            }, false)));
            Add(Theme.Label((Category == "basic") ? "「五十音」是表格名称；现代常用清音共 46 个。绿色 ✓ 表示已熟悉。" : ((Category == "voiced") ? "「\u309b」是浊点，「\u309c」是半浊点。じ/ぢ、ず/づ 通常同音，之后在词语中区别写法。" : "小写的ゃ・ゅ・ょ与前字合成一拍；きゃ（kya）和 きや（ki-ya）不同。"), 10f, false, 52));
            FinishPage();
        }

        private void RenderGroups()
        {
            BeginPage("一次一行，稳稳记住。", "推荐顺序：元音 → 其他清音 → 浊音 / 半浊音 → 拗音。选一组开始翻卡。");
            Add(Categories());
            List<KanaGroup> list = Data.Groups.Where((KanaGroup g) => g.Type == Category).ToList();
            TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
            tableLayoutPanel.ColumnCount = 3;
            tableLayoutPanel.RowCount = (list.Count + 2) / 3;
            tableLayoutPanel.Height = (list.Count + 2) / 3 * 132;
            TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
            for (int i = 0; i < 3; i++)
            {
                tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            }
            for (int j = 0; j < (list.Count + 2) / 3; j++)
            {
                tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));
            }
            for (int k = 0; k < list.Count; k++)
            {
                KanaGroup g2 = list[k];
                Button button = Theme.Button(g2.Name + "\n" + string.Join(" ", g2.Items.Select((Kana x) => x.K)) + "\n" + g2.Items.Count((Kana x) => State.Learned.Contains(x.K)) + " / " + g2.Items.Count + " 已熟悉", delegate
                {
                    StartCards(g2.Id);
                }, false);
                button.Dock = DockStyle.Fill;
                button.AutoSize = false;
                button.Font = Theme.Font(13f, false);
                button.BackColor = Theme.Surface;
                button.ForeColor = Theme.Ink;
                button.Margin = new Padding(0, 0, 10, 10);
                tableLayoutPanel2.Controls.Add(button, k % 3, k / 3);
            }
            Add(tableLayoutPanel2);
            FinishPage();
        }

        private FlowLayoutPanel ScopeBar(bool quiz)
        {
            ComboBox box = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 285,
                Height = 36,
                Font = Theme.Font(10f, false),
                Margin = new Padding(8, 3, 12, 4),
                AccessibleName = "学习范围"
            };
            Theme.StyleCombo(box);
            List<ScopeOption> list = new List<ScopeOption>();
            list.Add(new ScopeOption
            {
                Id = "basic",
                Name = "全部清音（46）"
            });
            list.Add(new ScopeOption
            {
                Id = "voiced",
                Name = "浊音 / 半浊音（25）"
            });
            list.Add(new ScopeOption
            {
                Id = "combo",
                Name = "常用拗音（33）"
            });
            list.Add(new ScopeOption
            {
                Id = "all",
                Name = "全部假名（104）"
            });
            list.Add(new ScopeOption
            {
                Id = "errors",
                Name = "待复习错题"
            });
            list.Add(new ScopeOption
            {
                Id = "learned",
                Name = "已熟悉假名（巩固）"
            });
            List<ScopeOption> list2 = list;
            list2.AddRange(Data.Groups.Select((KanaGroup g) => new ScopeOption
            {
                Id = g.Id,
                Name = g.Name + " · " + string.Join("", g.Items.Select((Kana x) => x.K))
            }));
            box.Items.AddRange(list2.Cast<object>().ToArray());
            box.SelectedIndex = list2.FindIndex((ScopeOption x) => x.Id == Scope);
            box.SelectedIndexChanged += delegate
            {
                ScopeOption scopeOption = box.SelectedItem as ScopeOption;
                if (scopeOption != null)
                {
                    Scope = scopeOption.Id;
                    if (quiz)
                    {
                        Test = new Quiz(Scope, State);
                    }
                    else
                    {
                        ResetDeck();
                    }
                    Render();
                }
            };
            Label label = Theme.Label("学习范围", 10f, false, 34);
            label.Width = 76;
            label.Dock = DockStyle.None;
            return Actions(label, box, Theme.Button(quiz ? "重新开始" : "重新洗牌", delegate
            {
                if (quiz)
                {
                    Test = new Quiz(Scope, State);
                }
                else
                {
                    ResetDeck();
                }
                Render();
            }, false));
        }

        private void ResetDeck()
        {
            Deck = Randomize.Shuffle(Data.Pool(Scope, State));
            CardIndex = 0;
            Flipped = false;
        }

        private void StartCards(string scope)
        {
            Scope = scope;
            Page = "cards";
            ResetDeck();
            Render();
        }

        private void StartQuiz(string scope)
        {
            Scope = scope;
            Test = new Quiz(scope, State);
            Page = "quiz";
            Render();
        }

        private void MoveCard(int by)
        {
            if (Deck.Count != 0)
            {
                CardIndex = (CardIndex + by + Deck.Count) % Deck.Count;
                Flipped = false;
                Render();
            }
        }

        private void RenderCards()
        {
            BeginPage("先回想，再翻开答案。", "看着假名试着读出来。点卡片核对答案，再决定是否熟悉。");
            Add(ScopeBar(false));
            if (Deck.Count == 0)
            {
                Add(Theme.Label("当前范围没有待学习假名，请切换范围。", 17f, true, 100));
                FinishPage();
                return;
            }
            Kana x = Deck[CardIndex];
            Add(Theme.Label("记忆卡  " + (CardIndex + 1) + " / " + Deck.Count, 10f, false, 30));
            bool hasStrokes = StrokeData.Has(x.K);
            StrokePanel strokes = null;
            if (hasStrokes)
            {
                // 换到新卡片才自动书写；翻开答案时保持写完的状态
                strokes = new StrokePanel(x.K, x.K != LastStrokeKana);
                LastStrokeKana = x.K;
            }
            KanaGlyph button2 = new KanaGlyph(x.K, 80f);
            button2.AccessibleName = x.K + "，" + (Flipped ? "点击隐藏答案" : "点击翻开答案");
            button2.AccessibleRole = AccessibleRole.PushButton;
            button2.Height = 195;
            button2.Dock = DockStyle.Top;
            button2.Cursor = Cursors.Hand;
            button2.Clickable = true;
            button2.Click += delegate
            {
                Flipped = !Flipped;
                Render();
            };
            Label label = Theme.Label(Flipped ? x.R : "· · ·  点击卡片，核对读音", Flipped ? 28 : 12, Flipped, 58);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.ForeColor = Theme.Purple;
            if (hasStrokes)
            {
                // 卡片左边是大字和读音，右边是笔顺面板（2.3.0）
                Card card = new Card { Height = Math.Max(380, strokes.Height), Padding = new Padding(1) };
                TableLayoutPanel row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), BackColor = Theme.Surface };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, StrokePanel.PanelWidth));
                row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                Panel face = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), BackColor = Theme.Surface };
                button2.Dock = DockStyle.Fill;
                label.Dock = DockStyle.Bottom;
                label.Height = 70;
                label.BackColor = Theme.Surface;
                label.Cursor = Cursors.Hand;
                label.Click += delegate
                {
                    Flipped = !Flipped;
                    Render();
                };
                face.Controls.Add(button2);
                face.Controls.Add(label);
                strokes.Dock = DockStyle.Fill;
                strokes.Margin = new Padding(0);
                row.Controls.Add(face, 0, 0);
                row.Controls.Add(strokes, 1, 0);
                card.Controls.Add(row);
                Add(card);
            }
            else
            {
                Card plain = new Card { Height = 197, Padding = new Padding(1) };
                button2.Dock = DockStyle.Fill;
                plain.Controls.Add(button2);
                Add(plain);
                Add(label);
            }
            Add(Actions(Theme.Button("听发音", delegate
            {
                Speak(x);
            }, false), Theme.Button("上一张", delegate
            {
                MoveCard(-1);
            }, false), Theme.Button("下一张", delegate
            {
                MoveCard(1);
            }, false)));
            if (Flipped)
            {
                Add(Actions(Theme.Button("还不熟，再练练", delegate
                {
                    State.CardAgain(x);
                    Save();
                    MoveCard(1);
                }, false), Theme.Button("我记住了 ✓", delegate
                {
                    State.CardKnown(x);
                    Save();
                    MoveCard(1);
                }, true)));
            }
            Add(Actions(Theme.Button("用测验检查这一组 →", delegate
            {
                StartQuiz(Scope);
            }, true)));
            FinishPage();
        }

        private void RenderQuiz()
        {
            if (Test == null)
            {
                Test = new Quiz(Scope, State);
            }
            BeginPage("这个假名，怎么读？", "选择正确的罗马音。答题后可以听发音；错题会自动进入复习。");
            Add(ScopeBar(true));
            if (Test.Items.Count == 0)
            {
                Add(Theme.Label(Scope == "learned" ? "还没有已熟悉的假名，先用记忆卡学一行吧。" : "当前没有待复习的错题。", 18f, true, 90));
                Add(Actions(Theme.Button("从清音开始测验", delegate
                {
                    StartQuiz("basic");
                }, true)));
                FinishPage();
                return;
            }
            if (Test.Done)
            {
                Add(Theme.Label("本轮完成！  " + Test.Score + " / " + Test.Items.Count, 28f, true, 100));
                Add(Theme.Label((Test.Score == Test.Items.Count) ? "全部答对！明天再复习一次，让记忆更牢。" : "答错的假名已经加入复习，再见几次就会更熟悉。", 12f, false, 60));
                Add(Actions(Theme.Button("再测一轮", delegate
                {
                    StartQuiz(Scope);
                }, true), Theme.Button("去错题复习", delegate
                {
                    Navigate("errors");
                }, false)));
                FinishPage();
                return;
            }
            Kana x = Test.Current;
            Add(Theme.Label("假名 → 读音     " + (Test.Index + 1) + " / " + Test.Items.Count, 10f, false, 28));
            KanaGlyph label = new KanaGlyph(x.K, 76f) { Height = 165, BackColor = Theme.Paper };
            Add(label);
            TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
            tableLayoutPanel.ColumnCount = 2;
            tableLayoutPanel.RowCount = 2;
            tableLayoutPanel.Height = 144;
            TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            for (int i = 0; i < Test.Options.Count; i++)
            {
                string r = Test.Options[i];
                Button button = Theme.Button(r, delegate
                {
                    if (Test.Answer(r))
                    {
                        Save();
                        Render();
                    }
                }, false);
                button.AutoSize = false;
                button.Font = Theme.Font(21f, false);
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(4);
                button.BackColor = Theme.Surface;
                button.ForeColor = Theme.Ink;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = Theme.Line;
                if (Test.Submitted)
                {
                    // 不用 Enabled = false：禁用的按钮文字会被系统画成灰色，深色模式下看不清
                    button.Tag = "locked";
                    button.Cursor = Cursors.Default;
                    if (r == x.R)
                    {
                        button.BackColor = Theme.OkBg;
                        button.ForeColor = Theme.OkFg;
                        button.FlatAppearance.BorderColor = Theme.OkFg;
                    }
                    else if (r == Test.Selected)
                    {
                        button.BackColor = Theme.BadBg;
                        button.ForeColor = Theme.BadFg;
                        button.FlatAppearance.BorderColor = Theme.BadFg;
                    }
                }
                tableLayoutPanel2.Controls.Add(button, i % 2, i / 2);
            }
            Add(tableLayoutPanel2);
            string text = "先凭记忆作答。";
            if (Test.Submitted)
            {
                text = ((Test.Selected == x.R) ? ("✓ 答对了！" + (State.Errors.ContainsKey(x.K) ? "还需再答对一次才能移出错题。" : "")) : ("再记一次：" + x.K + " 的读音是 " + x.R + "。已加入错题。"));
            }
            Add(Theme.Label(text, 12f, false, 48));
            if (Test.Submitted)
            {
                Add(Actions(Theme.Button("听发音", delegate
                {
                    Speak(x);
                }, false), Theme.Button((Test.Index + 1 == Test.Items.Count) ? "查看结果" : "下一题 →", delegate
                {
                    Test.Next();
                    Render();
                }, true)));
            }
            FinishPage();
        }

        private void RenderErrors()
        {
            BeginPage("把不熟悉的，变成熟悉。", "答错或标记「还不熟」的假名会保留在这里；连续答对两次后移出错题。");
            List<Kana> list = Data.Pool("errors", State);
            Add(Theme.Label("待复习 · " + list.Count + " 个", 18f, true, 44));
            if (list.Count == 0)
            {
                Add(Theme.Label("这里干干净净。来一轮测验，看看记住了多少。", 13f, false, 70));
                Add(Actions(Theme.Button("从清音开始测验", delegate
                {
                    StartQuiz("basic");
                }, true)));
            }
            else
            {
                Add(Actions(Theme.Button("先看记忆卡", delegate
                {
                    StartCards("errors");
                }, false), Theme.Button("开始错题测验 →", delegate
                {
                    StartQuiz("errors");
                }, true)));
                TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
                tableLayoutPanel.ColumnCount = 5;
                tableLayoutPanel.RowCount = (list.Count + 4) / 5;
                tableLayoutPanel.Height = (list.Count + 4) / 5 * 115;
                TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
                for (int i = 0; i < 5; i++)
                {
                    tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
                }
                for (int j = 0; j < (list.Count + 4) / 5; j++)
                {
                    tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 115f));
                }
                for (int k = 0; k < list.Count; k++)
                {
                    Kana x = list[k];
                    Button button = Theme.Button(x.K + "  " + x.R + "\n还需答对 " + State.Errors[x.K] + " 次", delegate
                    {
                        ShowDetail(x);
                    }, false);
                    button.AutoSize = false;
                    button.Dock = DockStyle.Fill;
                    button.BackColor = Theme.Surface;
                    button.ForeColor = Theme.Ink;
                    button.Margin = new Padding(4);
                    button.Font = Theme.Font(13f, false);
                    tableLayoutPanel2.Controls.Add(button, k % 5, k / 5);
                }
                Add(tableLayoutPanel2);
            }
            Add(Theme.Label("仅翻卡或标记熟悉不会清除错题；需要通过测验答对。", 10f, false, 50));
            FinishPage();
        }

        private void RenderHelp()
        {
            BeginPage("每天一小步，读懂新世界。", "面向中文母语、完全零基础、以阅读漫画和轻小说为目标的学习者。");
            Add(Theme.Label("每天 10–15 分钟，先从 5 个假名开始", 19f, true, 56));
            Add(Theme.Label("① 选一行：点开字形，看罗马音，听发音。\n② 翻记忆卡：先尝试回想，再点开答案。\n③ 做一轮测验：每轮最多 10 题，小组不足 10 个时全部练习。\n④ 第二天先复习错题，再学新的一行。", 12f, false, 155));
            Add(Theme.Label("认读时要注意", 17f, true, 44));
            Add(Theme.Label("罗马音是临时提示，不是中文拼音。し读 shi、ち读 chi、つ读 tsu、ふ读 fu；ら行不等于英语 r。を通常读 o；は、へ作助词时读 wa、e。\n\n先学 46 个清音，再学浊音、半浊音和常用拗音。拗音有小写的ゃ、ゅ、ょ，与前字一起占一拍。之后还要学习片假名、词汇与语法，才能逐步阅读喜欢的故事。", 11f, false, 172));
            Add(Theme.Label("使用与进度", 17f, true, 44));
            Add(Theme.Label("这是原生 Windows 桌面程序，不调用浏览器或 WebView，也无需联网。104 个发音片段内置在程序中，无需额外安装日语语音包。进度自动保存在本机；关闭再打开可继续学习。网页版本的浏览器进度不会自动迁移。", 11f, false, 115));
            Add(Theme.Label("录音来源与授权", 17f, true, 44));
            Add(Theme.Label("录音：FUN Japanese Learning（https://funjapaneselearning.com）。\n数据集整理：Arsalan Anwari，Kana Sounds。\nhttps://huggingface.co/datasets/arsalan-anwari/kana-sounds\n授权：Creative Commons Attribution 4.0（CC BY 4.0）。\nhttps://creativecommons.org/licenses/by/4.0/\n录音按原样内置；调整了文件标识以匹配本程序的罗马音显示。", 10f, false, 153));
            Add(Theme.Label("笔顺与字体", 17f, true, 44));
            Add(Theme.Label("笔顺数据：KanjiVG（https://kanjivg.tagaini.net），Copyright (C) 2009/2010/2011 Ulrich Apel，\n授权：Creative Commons Attribution-ShareAlike 3.0（CC BY-SA 3.0），https://creativecommons.org/licenses/by-sa/3.0/。\n假名字体：Klee One，Copyright 2020 The Klee Project Authors，授权：SIL Open Font License 1.1；\n本程序只内置了其中的假名部分。", 10f, false, 110));
            Add(Theme.Label("今日计划与专注计时", 17f, true, 44));
            Add(Theme.Label("「今日计划」会按你的进度给出当天建议：有错题先复习，再学下一行，做一轮测验，并完成每日专注时长目标。错题达到 8 个时，会建议当天先不学新内容。\n侧边栏的计时器在切换页面时不会停止；只有计时器走过的时间会计入学习时长。时间到会响铃并提醒休息。连续学习天数按每天是否有学习记录计算。", 11f, false, 120));
            Add(Theme.Label("外观与护眼", 17f, true, 44));
            Add(Theme.Label("可选「亮色护眼」「深色护眼」，或「跟随系统」（随 Windows 的应用深浅色设置自动切换）。两套配色都用暖色纸张底，不用纯白纯黑，正文对比度适中。长时间学习时，也建议每 20 分钟看看远处，让眼睛休息一下。\n当前：" + Theme.ModeName(State.Theme) + (State.Theme == "system" ? "（" + (Theme.IsDark ? "深色" : "亮色") + "）" : ""), 11f, false, 92));
            List<Control> themeButtons = new List<Control>();
            for (int i = 0; i < Theme.Modes.Length; i++)
            {
                string mode = Theme.Modes[i];
                themeButtons.Add(Theme.Button(Theme.ModeNames[i] + (State.Theme == mode ? " ✓" : ""), delegate { SetTheme(mode); }, State.Theme == mode));
            }
            Add(Actions(themeButtons.ToArray()));
            Label c = Theme.Label("进度文件：\n" + State.Path, 9f, false, 65);
            Add(c);
            FinishPage();
        }

        private void ShowDetail(Kana x)
        {
            Form form = new Form();
            form.AutoScaleMode = AutoScaleMode.None;
            form.Text = "假名读音 · " + x.K;
            form.ClientSize = new Size(460, 420);
            form.Font = Font;
            form.BackColor = Theme.Paper;
            form.HandleCreated += delegate { Theme.StyleTitleBar(form); };
            form.StartPosition = FormStartPosition.CenterParent;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;
            using (Form form2 = form)
            {
                TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
                tableLayoutPanel.Dock = DockStyle.Fill;
                tableLayoutPanel.ColumnCount = 1;
                tableLayoutPanel.Padding = new Padding(24);
                tableLayoutPanel.RowCount = 5;
                TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
                tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                int[] array = new int[5] { 35, 135, 55, 76, 66 };
                int[] array2 = array;
                foreach (int num in array2)
                {
                    tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, num));
                }
                Label label = Theme.Label(Data.Groups.First((KanaGroup g) => g.Id == x.Group).Name, 11f, false, 32);
                label.TextAlign = ContentAlignment.MiddleCenter;
                tableLayoutPanel2.Controls.Add(label, 0, 0);
                KanaGlyph label2 = new KanaGlyph(x.K, 72f) { Dock = DockStyle.Fill, BackColor = Theme.Paper };
                tableLayoutPanel2.Controls.Add(label2, 0, 1);
                Label label3 = Theme.Label(x.R, 26f, true, 55);
                label3.TextAlign = ContentAlignment.MiddleCenter;
                label3.ForeColor = Theme.Purple;
                tableLayoutPanel2.Controls.Add(label3, 0, 2);
                Label label4 = Theme.Label(Data.Note(x), 10f, false, 74);
                label4.TextAlign = ContentAlignment.MiddleCenter;
                tableLayoutPanel2.Controls.Add(label4, 0, 3);
                Button mark = null;
                mark = Theme.Button(State.Learned.Contains(x.K) ? "取消熟悉标记" : "标记为已熟悉 ✓", delegate
                {
                    State.ToggleLearned(x);
                    Save();
                    mark.Text = (State.Learned.Contains(x.K) ? "取消熟悉标记" : "标记为已熟悉 ✓");
                    mark.AccessibleName = mark.Text;
                }, true);
                tableLayoutPanel2.Controls.Add(Actions(Theme.Button("听发音", delegate
                {
                    Speak(x);
                }, false), mark), 0, 4);
                form2.Controls.Add(tableLayoutPanel2);
                using (Graphics graphics = form2.CreateGraphics())
                {
                    float num2 = graphics.DpiX / 96f;
                    if (num2 != 1f)
                    {
                        form2.Scale(new SizeF(num2, num2));
                    }
                }
                form2.ShowDialog(this);
            }
            Render();
        }

        private void Speak(Kana x)
        {
            try
            {
                Audio.Play(x);
                Status.Text = "正在播放内置发音：" + x.K + "（" + x.R + "）  ·  无需联网";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "暂时无法播放内置录音，请检查音频输出及本地缓存文件夹的写入权限。\n\n" + ex.Message, "日语发音", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }
    }
}
