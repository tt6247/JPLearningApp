using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KanaSmallSteps
{
    /// <summary>
    /// 主窗口：「今日计划」页面与专注计时器（v2.1 新增）。
    /// 计时器在侧边栏常驻，切换页面不会中断；时长计入 Progress 的当日记录。
    /// </summary>
    public sealed partial class MainForm
    {
        private const string AppTitle = "假名小步 · 独立桌面版";

        /// <summary>未写盘的计时秒数达到该值时自动保存一次。</summary>
        private const double AutoSaveSeconds = 30;

        private StudyTimer Clock;

        private Timer Ticker;

        private double Unsaved;

        // 侧边栏计时器
        private Card SidePanel;

        private Label SideHead;

        private Label SideTime;

        private Label SideInfo;

        private Button SideToggle;

        private LineBar SideBar;

        // 「今日计划」页上的控件（换页时会被释放，用前检查）
        private Label BigTime;

        private Label BigState;

        private Label BigTotal;

        private Button BigToggle;

        private LineBar BigBar;

        private LineBar GoalBar;

        private Label TodayMinutes;

        private readonly List<Button> PresetButtons = new List<Button>();

        [StructLayout(LayoutKind.Sequential)]
        private struct FlashInfo
        {
            public uint Size;
            public IntPtr Window;
            public uint Flags;
            public uint Count;
            public uint Timeout;
        }

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FlashInfo info);

        private void InitTimer()
        {
            Clock = new StudyTimer(State.TimerMinutes);
            Ticker = new Timer();
            Ticker.Interval = 500;
            Ticker.Tick += delegate { OnTick(); };
            Ticker.Start();
            FormClosing += delegate
            {
                Ticker.Stop();
                if (Clock.Running)
                {
                    State.AddStudySeconds(Clock.Pause(DateTime.UtcNow));
                    Unsaved = 1;
                }
                if (Unsaved > 0)
                {
                    State.Save();
                }
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Ticker != null)
            {
                Ticker.Dispose();
                Ticker = null;
            }
            base.Dispose(disposing);
        }

        private void BuildSidebarTimer(FlowLayoutPanel sidebar)
        {
            SidePanel = new Card { Width = 165, Height = 100, Margin = new Padding(0, 14, 0, 0) };
            SideHead = new Label { Location = new Point(11, 8), Size = new Size(92, 20), Font = Theme.Font(9f, false), ForeColor = Theme.Muted, UseMnemonic = false };
            SideTime = new Label { Location = new Point(9, 27), Size = new Size(96, 32), Font = new Font("Segoe UI", 17f, FontStyle.Bold), ForeColor = Theme.Ink, Cursor = Cursors.Hand, AccessibleName = "剩余时间" };
            SideInfo = new Label { Location = new Point(11, 64), Size = new Size(148, 20), Font = Theme.Font(9f, false), ForeColor = Theme.Muted, UseMnemonic = false };
            SideToggle = Theme.Button("开始", ToggleTimer, true);
            SideToggle.AutoSize = false;
            SideToggle.MinimumSize = Size.Empty;
            SideToggle.Padding = Padding.Empty;
            SideToggle.Font = Theme.Font(9f, false);
            SideToggle.Location = new Point(105, 18);
            SideToggle.Size = new Size(50, 34);
            SideBar = new LineBar { Location = new Point(11, 88), Size = new Size(143, 6) };
            SideTime.Click += delegate { Navigate("today"); };
            SideHead.Click += delegate { Navigate("today"); };
            SidePanel.Controls.AddRange(new Control[] { SideHead, SideTime, SideInfo, SideToggle, SideBar });
            sidebar.Controls.Add(SidePanel);
        }

        private void ClearTodayRefs()
        {
            BigTime = null;
            BigState = null;
            BigTotal = null;
            BigToggle = null;
            BigBar = null;
            GoalBar = null;
            TodayMinutes = null;
            PresetButtons.Clear();
        }

        // ---------- 计时逻辑 ----------

        private void OnTick()
        {
            bool finished;
            double credit = Clock.Tick(DateTime.UtcNow, out finished);
            if (credit > 0)
            {
                State.AddStudySeconds(credit);
                Unsaved += credit;
            }
            if (finished)
            {
                OnTimerFinished();
                return;
            }
            if (Unsaved >= AutoSaveSeconds)
            {
                Unsaved = 0;
                Save();
            }
            UpdateTimerUI();
        }

        private void ToggleTimer()
        {
            if (Clock.Running)
            {
                State.AddStudySeconds(Clock.Pause(DateTime.UtcNow));
                Unsaved = 0;
                Save();
                Status.Text = "已暂停计时，休息一下也没关系。";
            }
            else
            {
                Clock.Start(DateTime.UtcNow);
                Status.Text = "开始专注 " + Math.Round(Clock.Remaining / 60) + " 分钟。计时在切换页面时会继续。";
            }
            UpdateTimerUI();
        }

        private void ResetTimer()
        {
            if (Clock.Running)
            {
                State.AddStudySeconds(Clock.Pause(DateTime.UtcNow));
                Unsaved = 0;
                Save();
            }
            Clock.Reset();
            UpdateTimerUI();
        }

        private void AddFiveMinutes()
        {
            Clock.AddSeconds(300);
            UpdateTimerUI();
        }

        private void SetTimerMinutes(int minutes)
        {
            if (Clock.Set(minutes))
            {
                State.TimerMinutes = minutes;
                Save();
            }
            UpdateTimerUI();
        }

        /// <summary>「专注学习 N 分钟」任务：按剩余目标时长开始一段计时。</summary>
        private void StartGoalTimer()
        {
            if (Clock.Running)
            {
                Status.Text = "计时正在进行中，继续加油！";
                return;
            }
            Clock.Set(DailyPlan.SuggestedTimerMinutes(State));
            Clock.Start(DateTime.UtcNow);
            UpdateTimerUI();
        }

        private void OnTimerFinished()
        {
            Unsaved = 0;
            Save();
            bool goalMet = State.Today.Seconds / 60 >= State.Goal;
            string message = goalMet
                ? "时间到！今日目标已达成，好好休息一下。"
                : "时间到！起身活动 5 分钟，喝口水，再开始下一段。";
            Status.Text = message;
            if (Page == "today")
            {
                Render();
            }
            UpdateTimerUI();
            try
            {
                SystemSounds.Exclamation.Play();
                FlashInfo info = new FlashInfo();
                info.Size = (uint)Marshal.SizeOf(typeof(FlashInfo));
                info.Window = Handle;
                info.Flags = 3 | 12; // FLASHW_ALL | FLASHW_TIMERNOFG
                FlashWindowEx(ref info);
            }
            catch
            {
            }
            MessageBox.Show(this, message, "专注计时", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateTimerUI()
        {
            if (Clock == null || SideTime == null)
            {
                return;
            }
            string time = StudyTimer.Format(Clock.Remaining);
            int minutes = (int)(State.Today.Seconds / 60);
            string state = Clock.Running ? "专注中…" : (Clock.Started ? "已暂停" : (Clock.Finished ? "时间到 ✓" : "专注计时"));
            double bar = Clock.Finished ? 1 : (Clock.Started ? Clock.Elapsed : 0);

            SideHead.Text = state;
            SideTime.Text = time;
            SideInfo.Text = "今日 " + minutes + " / " + State.Goal + " 分钟";
            SideToggle.Text = Clock.Running ? "暂停" : (Clock.Started ? "继续" : "开始");
            SideToggle.AccessibleName = SideToggle.Text + "计时";
            SideBar.Value = bar;
            Color back = Clock.Running ? Theme.Light : Theme.Surface;
            if (SidePanel.BackColor != back)
            {
                SidePanel.BackColor = back;
                SidePanel.Border = Clock.Running ? Theme.Part : Theme.Line;
                SidePanel.Invalidate();
            }

            if (BigTime != null && !BigTime.IsDisposed)
            {
                BigTime.Text = time;
                BigState.Text = Clock.Started || Clock.Finished ? state : "准备开始";
                BigTotal.Text = "本段共 " + Clock.Total / 60 + " 分钟";
                BigToggle.Text = Clock.Running ? "暂停" : (Clock.Started ? "继续" : (Clock.Finished ? "再来一段" : "开始专注"));
                BigToggle.AccessibleName = BigToggle.Text;
                BigBar.Value = bar;
                foreach (Button b in PresetButtons)
                {
                    bool active = (int)b.Tag * 60 == Clock.Total;
                    b.Enabled = !Clock.Running;
                    b.BackColor = active ? Theme.Light : Theme.Surface;
                    b.ForeColor = active ? Theme.Purple : Theme.Ink;
                }
            }
            if (TodayMinutes != null && !TodayMinutes.IsDisposed)
            {
                TodayMinutes.Text = minutes + " / " + State.Goal + " 分钟";
            }
            if (GoalBar != null && !GoalBar.IsDisposed)
            {
                GoalBar.Value = State.Today.Seconds / 60 / State.Goal;
            }
            string title = Clock.Running ? time + " · 假名小步" : AppTitle;
            if (Text != title)
            {
                Text = title;
            }
        }

        // ---------- 「今日计划」页面 ----------

        private void RenderToday()
        {
            DateTime now = State.Clock();
            BeginPage(DailyPlan.Greeting(now) + "，今天也学一小步。", DailyPlan.Coach(State));
            DayLog today = State.Today;

            Label[] values = StatsRow(
                new string[] { "连续学习", "今日专注时长", "今日答题" },
                new string[]
                {
                    State.Streak() + " 天",
                    (int)(today.Seconds / 60) + " / " + State.Goal + " 分钟",
                    today.Answered > 0 ? today.Answered + " 题 · " + Math.Round(100.0 * today.Correct / today.Answered) + "%" : "0 题"
                });
            TodayMinutes = values[1];

            List<PlanTask> tasks = DailyPlan.Build(State);
            const int rowHeight = 78;
            int planHeight = 32 + 50 + tasks.Count * rowHeight;
            TableLayoutPanel grid = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Height = Math.Max(planHeight, 400) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Card plan = BuildPlanCard(tasks, rowHeight);
            plan.Margin = new Padding(0, 0, 12, 0);
            grid.Controls.Add(plan, 0, 0);
            Card timer = BuildTimerCard();
            timer.Margin = new Padding(0);
            grid.Controls.Add(timer, 1, 0);
            Add(grid);

            Add(Theme.Label("近 7 天专注时长", 14f, true, 34));
            WeekChart chart = new WeekChart { Height = 180, Days = DailyPlan.Week(State), Goal = State.Goal };
            Add(chart);
            Label tip = Theme.Label("小贴士：" + DailyPlan.TipOfDay(now), 10.5f, false, 44);
            tip.ForeColor = Theme.NoticeFg;
            Add(tip);
            FinishPage();
            UpdateTimerUI();
        }

        private Card BuildPlanCard(List<PlanTask> tasks, int rowHeight)
        {
            Card card = new Card { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 14, 10) };
            TableLayoutPanel list = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = tasks.Count + 2, BackColor = Theme.Surface };
            list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            list.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));

            Panel head = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Label title = Theme.Label("今日学习建议", 14f, true, 36);
            title.Dock = DockStyle.Fill;
            Label count = Theme.Label(tasks.Count(t => t.Done) + " / " + tasks.Count + " 完成", 9.5f, false, 36);
            count.Dock = DockStyle.Right;
            count.Width = 90;
            count.TextAlign = ContentAlignment.MiddleRight;
            head.Controls.Add(title);
            head.Controls.Add(count);
            list.Controls.Add(head, 0, 0);

            for (int i = 0; i < tasks.Count; i++)
            {
                list.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
                list.Controls.Add(BuildTaskRow(tasks[i]), 0, i + 1);
            }
            list.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 吸收多余高度，避免最后一行被拉长
            card.Controls.Add(list);
            return card;
        }

        private Control BuildTaskRow(PlanTask task)
        {
            TableLayoutPanel row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0), BackColor = Theme.Surface };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            row.Padding = new Padding(0, 1, 0, 0);
            row.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Line))
                {
                    e.Graphics.DrawLine(pen, 0, 0, row.Width, 0);
                }
            };

            Label mark = new Label
            {
                Text = task.Done ? "✓" : "○",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Symbol", 14f, FontStyle.Bold),
                ForeColor = task.Done ? Theme.Green : Theme.Part,
                AccessibleName = task.Done ? "已完成" : "未完成"
            };
            row.Controls.Add(mark, 0, 0);

            Panel text = new Panel { Dock = DockStyle.Fill, Margin = new Padding(2, 8, 8, 4) };
            Label title = Theme.Label(task.Title, 11f, true, 26);
            title.Margin = new Padding(0);
            if (task.Done)
            {
                title.ForeColor = Theme.Muted;
            }
            Label detail = Theme.Label(task.Detail, 9.5f, false, 40);
            detail.Dock = DockStyle.Fill;
            detail.TextAlign = ContentAlignment.TopLeft;
            text.Controls.Add(detail);
            text.Controls.Add(title);
            row.Controls.Add(text, 1, 0);

            if (task.Action != PlanAction.None)
            {
                PlanTask t = task;
                Button b = Theme.Button(task.Done ? task.DoneButton : task.Button, delegate { RunPlanTask(t); }, !task.Done);
                b.MinimumSize = new Size(96, 38);
                b.Height = 38;
                b.Anchor = AnchorStyles.Right;
                b.Margin = new Padding(0, 0, 2, 0);
                if (task.Done)
                {
                    Theme.Outline(b);
                }
                row.Controls.Add(b, 2, 0);
            }
            return row;
        }

        private void RunPlanTask(PlanTask t)
        {
            switch (t.Action)
            {
                case PlanAction.ReviewQuiz:
                    StartQuiz("errors");
                    break;
                case PlanAction.Warmup:
                    StartCards("learned");
                    break;
                case PlanAction.LearnGroup:
                    StartCards(t.Scope);
                    break;
                case PlanAction.Quiz:
                    StartQuiz(t.Scope);
                    break;
                case PlanAction.AllQuiz:
                    StartQuiz("all");
                    break;
                case PlanAction.StartTimer:
                    StartGoalTimer();
                    break;
            }
        }

        private static TableLayoutPanel EvenRow(int columns)
        {
            TableLayoutPanel t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = 1, Margin = new Padding(0), BackColor = Theme.Surface };
            for (int i = 0; i < columns; i++)
            {
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            }
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            return t;
        }

        private static void FillCell(Button b, bool gap)
        {
            b.AutoSize = false;
            b.MinimumSize = Size.Empty;
            b.Padding = Padding.Empty;
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(0, 0, gap ? 6 : 0, 0);
        }

        private Card BuildTimerCard()
        {
            Card card = new Card { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 12) };
            TableLayoutPanel col = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Theme.Surface };
            col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            int[] heights = new int[] { 44, 84, 24, 22, 52, 54, 42, 16, 46 };
            col.RowCount = heights.Length;
            foreach (int h in heights)
            {
                col.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            }

            Panel head = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Label title = Theme.Label("专注计时", 14f, true, 36);
            title.Dock = DockStyle.Fill;
            BigState = Theme.Label("", 9.5f, false, 36);
            BigState.Dock = DockStyle.Right;
            BigState.Width = 90;
            BigState.TextAlign = ContentAlignment.MiddleRight;
            head.Controls.Add(title);
            head.Controls.Add(BigState);
            col.Controls.Add(head, 0, 0);

            BigTime = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 40f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                AccessibleName = "剩余时间",
                Margin = new Padding(0)
            };
            col.Controls.Add(BigTime, 0, 1);

            BigTotal = Theme.Label("", 9.5f, false, 22);
            BigTotal.Dock = DockStyle.Fill;
            BigTotal.TextAlign = ContentAlignment.MiddleCenter;
            col.Controls.Add(BigTotal, 0, 2);

            BigBar = new LineBar { Dock = DockStyle.Fill, Margin = new Padding(24, 6, 24, 6) };
            col.Controls.Add(BigBar, 0, 3);

            TableLayoutPanel presets = EvenRow(Progress.TimerChoices.Length);
            presets.Padding = new Padding(0, 8, 0, 6);
            for (int i = 0; i < Progress.TimerChoices.Length; i++)
            {
                int m = Progress.TimerChoices[i];
                int minutes = m;
                Button b = Theme.Button(m + " 分钟", delegate { SetTimerMinutes(minutes); }, false);
                FillCell(b, i < Progress.TimerChoices.Length - 1);
                b.Font = Theme.Font(9f, false);
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Theme.Line;
                b.Tag = m;
                PresetButtons.Add(b);
                presets.Controls.Add(b, i, 0);
            }
            col.Controls.Add(presets, 0, 4);

            BigToggle = Theme.Button("开始专注", ToggleTimer, true);
            Button plus = Theme.Button("+5 分钟", AddFiveMinutes, false);
            Button reset = Theme.Button("重置", ResetTimer, false);
            TableLayoutPanel actions = EvenRow(3);
            actions.Padding = new Padding(0, 6, 0, 2);
            actions.ColumnStyles[0].Width = 40f;
            actions.ColumnStyles[1].Width = 32f;
            actions.ColumnStyles[2].Width = 28f;
            Button[] row = new Button[] { BigToggle, plus, reset };
            for (int i = 0; i < row.Length; i++)
            {
                FillCell(row[i], i < row.Length - 1);
                actions.Controls.Add(row[i], i, 0);
            }
            col.Controls.Add(actions, 0, 5);

            FlowLayoutPanel goalRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
            Label goalLabel = Theme.Label("每日目标", 10f, false, 30);
            goalLabel.Dock = DockStyle.None;
            goalLabel.Width = 72;
            ComboBox goal = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 110,
                Font = Theme.Font(10f, false),
                AccessibleName = "每日目标分钟数"
            };
            Theme.StyleCombo(goal);
            foreach (int g in Progress.GoalChoices)
            {
                goal.Items.Add(g + " 分钟");
            }
            goal.SelectedIndex = Array.IndexOf(Progress.GoalChoices, State.Goal);
            goal.SelectedIndexChanged += delegate
            {
                if (goal.SelectedIndex >= 0)
                {
                    State.Goal = Progress.GoalChoices[goal.SelectedIndex];
                    Save();
                    BeginInvoke(new Action(Render));
                }
            };
            goalRow.Controls.Add(goalLabel);
            goalRow.Controls.Add(goal);
            col.Controls.Add(goalRow, 0, 6);

            GoalBar = new LineBar { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4), Green = true };
            col.Controls.Add(GoalBar, 0, 7);

            Label hint = Theme.Label("时长按计时器统计。切换页面计时不停，时间到会响铃提醒。", 9f, false, 44);
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.TopLeft;
            col.Controls.Add(hint, 0, 8);

            card.Controls.Add(col);
            return card;
        }
    }
}
