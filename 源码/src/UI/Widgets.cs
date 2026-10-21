using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KanaSmallSteps
{
    /// <summary>细长的圆角进度条，Value 取 0–1。</summary>
    public sealed class LineBar : Control
    {
        private double ValueField;

        /// <summary>true 时用绿色（每日目标进度），否则用强调色。颜色在绘制时取当前主题。</summary>
        public bool Green;

        public LineBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Height = 8;
        }

        public double Value
        {
            get { return ValueField; }
            set
            {
                double v = Math.Max(0, Math.Min(1, value));
                if (Math.Abs(v - ValueField) > 0.0005)
                {
                    ValueField = v;
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color parentBack = Parent != null ? Parent.BackColor : Theme.Surface;
            e.Graphics.Clear(parentBack);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int h = Math.Max(2, Math.Min(Height, 8));
            int y = (Height - h) / 2;
            using (SolidBrush b = new SolidBrush(Theme.Track))
            {
                FillRound(e.Graphics, b, new RectangleF(0, y, Width - 1, h));
            }
            float w = (float)((Width - 1) * ValueField);
            if (w >= 1)
            {
                using (SolidBrush b = new SolidBrush(Green ? Theme.Green : Theme.Purple))
                {
                    FillRound(e.Graphics, b, new RectangleF(0, y, Math.Max(w, h), h));
                }
            }
        }

        internal static void FillRound(Graphics g, Brush brush, RectangleF r)
        {
            float d = Math.Min(r.Height, r.Width);
            if (d < 1)
            {
                return;
            }
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(r.X, r.Y, d, d, 90, 180);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }
    }

    /// <summary>近 7 天专注时长柱状图。</summary>
    public sealed class WeekChart : Control
    {
        public List<WeekDay> Days = new List<WeekDay>();

        public int Goal = 15;

        public WeekChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Days.Count == 0)
            {
                return;
            }
            int top = 0;
            foreach (WeekDay d in Days)
            {
                top = Math.Max(top, d.Minutes);
            }
            top = Math.Max(top, Goal);
            float pad = 16;
            float labelH = 26;
            float valueH = 22;
            float plotTop = pad + valueH;
            float plotBottom = Height - pad - labelH;
            float plotH = Math.Max(10, plotBottom - plotTop);
            float colW = (Width - pad * 2) / Days.Count;
            float barW = Math.Min(38, colW * 0.55f);
            float goalY = plotBottom - plotH * Goal / top;
            using (Pen goalPen = new Pen(Theme.Part, 1))
            {
                goalPen.DashStyle = DashStyle.Dash;
                g.DrawLine(goalPen, pad, goalY, Width - pad, goalY);
            }
            using (Font small = Theme.Font(8.5f, false))
            using (Font bold = Theme.Font(9f, true))
            using (SolidBrush met = new SolidBrush(Theme.Purple))
            using (SolidBrush part = new SolidBrush(Theme.Part))
            using (SolidBrush empty = new SolidBrush(Theme.Track))
            {
                TextRenderer.DrawText(g, "目标 " + Goal + " 分", small, new Rectangle((int)(Width - pad - 90), (int)goalY - 18, 90, 16), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.NoPadding);
                for (int i = 0; i < Days.Count; i++)
                {
                    WeekDay d = Days[i];
                    float cx = pad + colW * i + colW / 2;
                    float h = d.Minutes > 0 ? Math.Max(6, plotH * d.Minutes / top) : (d.Studied ? 6 : 3);
                    RectangleF bar = new RectangleF(cx - barW / 2, plotBottom - h, barW, h);
                    Brush brush = d.Minutes >= Goal ? met : (d.Minutes > 0 || d.Studied ? part : empty);
                    FillTopRound(g, brush, bar, 6);
                    string value = d.Minutes > 0 ? d.Minutes + " 分" : (d.Studied ? "✓" : "");
                    if (value.Length > 0)
                    {
                        TextRenderer.DrawText(g, value, small, new Rectangle((int)(cx - colW / 2), (int)(bar.Top - valueH), (int)colW, (int)valueH), Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom);
                    }
                    TextRenderer.DrawText(g, d.Label, d.IsToday ? bold : small, new Rectangle((int)(cx - colW / 2), (int)plotBottom + 4, (int)colW, (int)labelH), d.IsToday ? Theme.Ink : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
                }
            }
        }

        private static void FillTopRound(Graphics g, Brush brush, RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height * 2));
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }
    }

    /// <summary>
    /// 假名大字（2.3.0）：用 GDI+ 直接以内嵌的 Klee One 字体绘制。
    /// 不用 Label/Button 显示，因为它们走 GDI 绘制，对程序内加载的字体支持不稳定。
    /// </summary>
    public sealed class KanaGlyph : Control
    {
        private Font GlyphFont;

        public KanaGlyph(string text, float points)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Text = text;
            AccessibleName = text;
            GlyphFont = Theme.KanaFont(points);
            ForeColor = Theme.Ink;
            BackColor = Theme.Surface;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (SolidBrush brush = new SolidBrush(ForeColor))
            using (StringFormat fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                e.Graphics.DrawString(Text, GlyphFont, brush, new RectangleF(0, 0, Width, Height), fmt);
            }
            if (Clickable && Focused)
            {
                using (Pen focus = new Pen(Theme.Purple, 2))
                {
                    e.Graphics.DrawRectangle(focus, 3, 3, Width - 7, Height - 7);
                }
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        /// <summary>可点击时（记忆卡）支持 Tab 聚焦、空格 / 回车翻面，并显示焦点框。</summary>
        public bool Clickable
        {
            get { return TabStop; }
            set { TabStop = value; SetStyle(ControlStyles.Selectable, value); }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return Clickable && (keyData == Keys.Space || keyData == Keys.Enter) || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (Clickable && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Clickable)
            {
                Focus();
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && GlyphFont != null)
            {
                GlyphFont.Dispose();
                GlyphFont = null;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>白底、细边框的面板。</summary>
    public sealed class Card : Panel
    {
        public Color Border = Theme.Line;

        public Card()
        {
            BackColor = Theme.Surface;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Border))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
