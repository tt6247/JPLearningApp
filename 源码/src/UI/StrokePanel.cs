using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KanaSmallSteps
{
    /// <summary>
    /// 记忆卡右侧的笔顺面板（2.3.0）：自动逐笔书写（当前笔用强调色，圆点是起笔处，数字是第几画），
    /// 下方小格逐笔分解，点小格停在那一笔，点大框或「重播」再写一遍。外观与安卓版 app.js 的笔顺面板一致。
    /// </summary>
    public sealed class StrokePanel : Control
    {
        public const int PanelWidth = 340;

        private const int Pad = 18, HeaderH = 40, StepSize = 46, StepGap = 6, LegendH = 44;

        private const int StartDelay = 300, StrokeGap = 260;

        private readonly string Kana;

        private readonly List<Stroke> Strokes;

        private readonly float ViewW;

        private readonly Timer Anim = new Timer();

        private readonly Button Replay;

        private int[] Starts, Durations;

        private int AnimStart;

        /// <summary>-1：动画或写完的状态；≥0：停在第几笔（点了小格）。</summary>
        private int Pinned = -1;

        public StrokePanel(string kana) : this(kana, true)
        {
        }

        /// <param name="autoplay">false 时直接显示写完的样子（翻卡时不重播）。</param>
        public StrokePanel(string kana, bool autoplay)
        {
            Kana = kana;
            Strokes = StrokeData.Get(kana);
            ViewW = StrokeData.Width(kana);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            AccessibleName = kana + " 的笔顺，共 " + Strokes.Count + " 画";
            Anim.Interval = 16;
            Anim.Tick += delegate { Invalidate(); };
            Replay = Theme.Button("▶ 重播", Play, false);
            Replay.AutoSize = false;
            Replay.MinimumSize = Size.Empty;
            Replay.Padding = Padding.Empty;
            Replay.Size = new Size(76, 30);
            Replay.Font = Theme.Font(9f, false);
            Replay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(Replay);
            Width = PanelWidth;
            Height = PreferredHeightFor(PanelWidth);
            if (autoplay)
            {
                Play();
            }
            else
            {
                Starts = new int[Strokes.Count];
                Durations = new int[Strokes.Count];
                AnimStart = Environment.TickCount - 1000000; // 视为早已写完
            }
        }

        public int StrokeCount
        {
            get { return Strokes.Count; }
        }

        private int BoardSize
        {
            get { return Math.Max(80, Width - Pad * 2); }
        }

        private int StepColumns
        {
            get { return Math.Max(1, (BoardSize + StepGap) / (StepSize + StepGap)); }
        }

        public int PreferredHeightFor(int width)
        {
            int board = Math.Max(80, width - Pad * 2);
            int cols = Math.Max(1, (board + StepGap) / (StepSize + StepGap));
            int rows = (Strokes.Count + cols - 1) / cols;
            return Pad + HeaderH + board + 10 + rows * (StepSize + StepGap) + LegendH + Pad;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Replay.Location = new Point(Width - Pad - Replay.Width, Pad);
        }

        /// <summary>从第一笔开始自动书写。</summary>
        public void Play()
        {
            Pinned = -1;
            Starts = new int[Strokes.Count];
            Durations = new int[Strokes.Count];
            int t = StartDelay;
            for (int i = 0; i < Strokes.Count; i++)
            {
                Starts[i] = t;
                Durations[i] = (int)Math.Max(380, Math.Min(1100, Strokes[i].Length * 9));
                t += Durations[i] + StrokeGap;
            }
            AnimStart = Environment.TickCount;
            if (Strokes.Count > 0)
            {
                Anim.Start();
            }
            Invalidate();
        }

        /// <summary>当前进度：写到第几笔、这一笔写了多少；全部写完时 index = Count。</summary>
        private void Progress(out int index, out float t)
        {
            if (Pinned >= 0)
            {
                index = Pinned;
                t = 1;
                return;
            }
            int elapsed = Environment.TickCount - AnimStart;
            for (int i = 0; i < Strokes.Count; i++)
            {
                if (elapsed < Starts[i])
                {
                    index = i;
                    t = 0;
                    return;
                }
                if (elapsed < Starts[i] + Durations[i])
                {
                    index = i;
                    float x = (elapsed - Starts[i]) / (float)Durations[i];
                    t = x < 0.5f ? 2 * x * x : 1 - (float)Math.Pow(-2 * x + 2, 2) / 2; // ease-in-out
                    return;
                }
            }
            index = Strokes.Count;
            t = 1;
            Anim.Stop();
        }

        private Rectangle BoardRect
        {
            get { return new Rectangle(Pad, Pad + HeaderH, BoardSize, BoardSize); }
        }

        private Rectangle StepRect(int i)
        {
            int cols = StepColumns;
            int x = Pad + (i % cols) * (StepSize + StepGap);
            int y = BoardRect.Bottom + 10 + (i / cols) * (StepSize + StepGap);
            return new Rectangle(x, y, StepSize, StepSize);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            for (int i = 0; i < Strokes.Count; i++)
            {
                if (StepRect(i).Contains(e.Location))
                {
                    Anim.Stop();
                    Pinned = i;
                    Invalidate();
                    return;
                }
            }
            if (BoardRect.Contains(e.Location))
            {
                Play();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen line = new Pen(Theme.Line))
            {
                g.DrawLine(line, 0, 0, 0, Height); // 与左侧卡片的分隔线
            }

            int index;
            float t;
            Progress(out index, out t);

            using (Font title = Theme.Font(11f, true))
            using (Font sub = Theme.Font(9.5f, false))
            {
                TextRenderer.DrawText(g, "笔顺", title, new Point(Pad - 3, Pad + 4), Theme.Ink, Theme.Surface);
                Size tw = TextRenderer.MeasureText("笔顺", title);
                TextRenderer.DrawText(g, "共 " + Strokes.Count + " 画", sub, new Point(Pad + tw.Width, Pad + 7), Theme.Muted, Theme.Surface);
            }

            // 大框
            Rectangle board = BoardRect;
            using (SolidBrush bg = new SolidBrush(Theme.Paper))
            using (Pen border = new Pen(Theme.Line))
            {
                g.FillRectangle(bg, board);
                g.DrawRectangle(border, board);
            }
            DrawGlyph(g, board, index, t, true, Pinned >= 0 || index < Strokes.Count);

            // 逐笔小格
            using (Font small = new Font("Segoe UI", 7.5f))
            {
                for (int i = 0; i < Strokes.Count; i++)
                {
                    Rectangle r = StepRect(i);
                    bool current = (Pinned >= 0 ? Pinned : index) == i;
                    using (SolidBrush bg = new SolidBrush(Theme.Paper))
                    using (Pen border = new Pen(current ? Theme.Purple : Theme.Line))
                    {
                        g.FillRectangle(bg, r);
                        g.DrawRectangle(border, r);
                    }
                    DrawGlyph(g, r, i, 1, false, true);
                    TextRenderer.DrawText(g, (i + 1).ToString(), small, new Point(r.X + 2, r.Y + 1), Theme.Muted);
                }
            }

            // 图例
            int ly = StepRect(Math.Max(0, Strokes.Count - 1)).Bottom + 12;
            using (SolidBrush dot = new SolidBrush(Theme.Purple))
            {
                g.FillEllipse(dot, Pad, ly + 5, 8, 8);
            }
            using (Font legend = Theme.Font(8.5f, false))
            {
                TextRenderer.DrawText(g, "紫色是当前这一笔，圆点是起笔处，数字是第几画。\n点下面的小格可以单独看某一笔，点大框重播。", legend,
                    new Rectangle(Pad + 12, ly, Width - Pad * 2 - 12, LegendH), Theme.Muted, TextFormatFlags.WordBreak);
            }
        }

        /// <summary>在矩形里画字：index 之前的笔为墨色，index 这一笔画到 t（强调色），之后的笔为浅色底稿。</summary>
        private void DrawGlyph(Graphics g, Rectangle box, int index, float t, bool big, bool highlightCurrent)
        {
            float margin = big ? box.Width * 0.04f : 3;
            float scale = (box.Width - margin * 2) / ViewW;
            float ox = box.X + margin;
            float oy = box.Y + (box.Height - StrokeData.Size * scale) / 2;
            Func<PointF, PointF> map = p => new PointF(ox + p.X * scale, oy + p.Y * scale);

            if (big)
            {
                using (Pen guide = new Pen(Theme.Surface2, 1))
                {
                    guide.DashStyle = DashStyle.Dash;
                    float cx = box.X + box.Width / 2f, cy = box.Y + box.Height / 2f;
                    g.DrawLine(guide, cx, box.Y + 4, cx, box.Bottom - 4);
                    g.DrawLine(guide, box.X + 4, cy, box.Right - 4, cy);
                }
            }

            for (int i = 0; i < Strokes.Count; i++)
            {
                Stroke s = Strokes[i];
                if (s.Points.Length < 2)
                {
                    continue;
                }
                Color color;
                float width;
                PointF[] pts;
                if (i < index || (i == index && !highlightCurrent && t >= 1))
                {
                    color = Theme.Ink;
                    width = 3.2f;
                    pts = s.Points;
                }
                else if (i == index)
                {
                    // 当前笔：先画浅色底稿，再画已写出的部分
                    DrawPolyline(g, Array.ConvertAll(s.Points, p => map(p)), Theme.Line, 3f * scale);
                    color = Theme.Purple;
                    width = 3.6f;
                    pts = s.Partial(t);
                }
                else
                {
                    color = Theme.Line;
                    width = 3f;
                    pts = s.Points;
                }
                DrawPolyline(g, Array.ConvertAll(pts, p => map(p)), color, width * scale);
            }

            if (!big)
            {
                return;
            }
            // 起笔点和序号：已写的笔标灰色序号，当前笔标强调色序号和圆点
            using (Font numFont = new Font("Segoe UI", Math.Max(7f, 7f * scale), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font oldFont = new Font("Segoe UI", Math.Max(7f, 7f * scale), FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush accent = new SolidBrush(Theme.Purple))
            using (SolidBrush muted = new SolidBrush(Theme.Muted))
            {
                int last = Math.Min(index, Strokes.Count - 1);
                for (int i = 0; i <= last; i++)
                {
                    Stroke s = Strokes[i];
                    if (s.Points.Length < 2)
                    {
                        continue;
                    }
                    bool current = i == index && highlightCurrent;
                    PointF a = s.Points[0];
                    PointF b = s.Points[Math.Min(3, s.Points.Length - 1)];
                    float len = Math.Max(0.01f, Stroke.Distance(a, b));
                    PointF n = new PointF(a.X - (b.X - a.X) / len * 7, a.Y - (b.Y - a.Y) / len * 7);
                    PointF pa = map(a), pn = map(n);
                    if (current)
                    {
                        float r = 2.6f * scale;
                        g.FillEllipse(accent, pa.X - r, pa.Y - r, r * 2, r * 2);
                    }
                    string label = (i + 1).ToString();
                    Font f = current ? numFont : oldFont;
                    SizeF sz = g.MeasureString(label, f);
                    g.DrawString(label, f, current ? accent : muted, pn.X - sz.Width / 2, pn.Y - sz.Height / 2);
                }
            }
        }

        private static void DrawPolyline(Graphics g, PointF[] pts, Color color, float width)
        {
            if (pts.Length < 2)
            {
                return;
            }
            using (Pen pen = new Pen(color, Math.Max(1f, width)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, pts);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Anim.Stop();
                Anim.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
