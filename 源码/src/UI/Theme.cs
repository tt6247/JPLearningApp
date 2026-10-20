using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KanaSmallSteps
{
    /// <summary>
    /// 一套配色。数值与安卓版 app.css 的 CSS 变量一一对应，改动时两边同步。
    /// 护眼思路：暖色纸张底、不用纯白纯黑、正文对比度约 11:1、强调色降低饱和度。
    /// </summary>
    public sealed class Palette
    {
        public string Name;
        public bool Dark;
        public Color Bg, Surface, Surface2, Line, Ink, Muted, Accent, AccentSoft, OnAccent;
        public Color Green, GreenBg, OkBg, OkFg, BadBg, BadFg, Track, Part, Notice, NoticeFg;

        private static Color C(int rgb)
        {
            return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }

        /// <summary>亮色护眼：暖色纸张。</summary>
        public static readonly Palette Light = new Palette
        {
            Name = "亮色护眼", Dark = false,
            Bg = C(0xF2EDE1), Surface = C(0xFAF7F0), Surface2 = C(0xE9E2D2), Line = C(0xDED5C2),
            Ink = C(0x3B362F), Muted = C(0x6F6659),
            Accent = C(0x5F57A8), AccentSoft = C(0xE7E1F0), OnAccent = C(0xFAF7F0),
            Green = C(0x3E7A58), GreenBg = C(0xE2ECDC), OkBg = C(0xD6E8D1), OkFg = C(0x2F6B4A), BadBg = C(0xF3DDD3), BadFg = C(0x9A4436),
            Track = C(0xE4DDCE), Part = C(0xCEC6E6), Notice = C(0xE9E3EE), NoticeFg = C(0x5A5180)
        };

        /// <summary>深色护眼：暖灰黑底、米白文字。</summary>
        public static readonly Palette DarkPalette = new Palette
        {
            Name = "深色护眼", Dark = true,
            Bg = C(0x1D1C1A), Surface = C(0x272522), Surface2 = C(0x302D29), Line = C(0x3B3833),
            Ink = C(0xE3DCCF), Muted = C(0xA2998B),
            Accent = C(0xA99DDC), AccentSoft = C(0x35314A), OnAccent = C(0x1D1C1A),
            Green = C(0x83BE9B), GreenBg = C(0x25332B), OkBg = C(0x243A2D), OkFg = C(0x9DD4B1), BadBg = C(0x40292A), BadFg = C(0xE9A79B),
            Track = C(0x35322D), Part = C(0x544C78), Notice = C(0x2C2A36), NoticeFg = C(0xC3BBE6)
        };
    }

    public static class Theme
    {
        public static readonly string[] Modes = new string[] { "system", "light", "dark" };

        public static readonly string[] ModeNames = new string[] { "跟随系统", "亮色护眼", "深色护眼" };

        /// <summary>当前生效的配色。</summary>
        public static Palette P = Palette.Light;

        /// <summary>用户选择：system / light / dark。</summary>
        public static string Mode = "system";

        public static bool IsDark { get { return P.Dark; } }

        // 旧名称保留（Purple = 强调色，Light = 强调色浅底，Paper = 页面底色），新增的直接用语义名称
        public static Color Ink { get { return P.Ink; } }
        public static Color Muted { get { return P.Muted; } }
        public static Color Purple { get { return P.Accent; } }
        public static Color Light { get { return P.AccentSoft; } }
        public static Color Paper { get { return P.Bg; } }
        public static Color Line { get { return P.Line; } }
        public static Color Green { get { return P.Green; } }
        public static Color Surface { get { return P.Surface; } }
        public static Color Surface2 { get { return P.Surface2; } }
        public static Color OnAccent { get { return P.OnAccent; } }
        public static Color GreenBg { get { return P.GreenBg; } }
        public static Color OkBg { get { return P.OkBg; } }
        public static Color OkFg { get { return P.OkFg; } }
        public static Color BadBg { get { return P.BadBg; } }
        public static Color BadFg { get { return P.BadFg; } }
        public static Color Track { get { return P.Track; } }
        public static Color Part { get { return P.Part; } }
        public static Color NoticeFg { get { return P.NoticeFg; } }

        public static string ModeName(string mode)
        {
            int i = Array.IndexOf(Modes, mode);
            return i >= 0 ? ModeNames[i] : ModeNames[0];
        }

        /// <summary>按选择和系统设置确定配色；返回配色是否发生变化。</summary>
        public static bool Apply(string mode)
        {
            Mode = Array.IndexOf(Modes, mode) >= 0 ? mode : "system";
            bool dark = Mode == "dark" || (Mode == "system" && SystemPrefersDark());
            Palette next = dark ? Palette.DarkPalette : Palette.Light;
            bool changed = next != P;
            P = next;
            return changed;
        }

        /// <summary>Windows 10/11「选择默认应用模式」是否为深色。读取失败时按亮色处理。</summary>
        public static bool SystemPrefersDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        /// <summary>标题栏跟随深浅色（Windows 10 1809+ / 11；更早的系统忽略）。</summary>
        public static void StyleTitleBar(Form form)
        {
            if (!form.IsHandleCreated)
            {
                return;
            }
            try
            {
                int on = IsDark ? 1 : 0;
                if (DwmSetWindowAttribute(form.Handle, 20, ref on, 4) != 0)
                {
                    DwmSetWindowAttribute(form.Handle, 19, ref on, 4);
                }
            }
            catch
            {
                // 非 Windows 或旧系统
            }
        }

        /// <summary>滚动条等系统控件跟随深浅色。</summary>
        public static void StyleScrollbars(Control c)
        {
            if (!c.IsHandleCreated)
            {
                return;
            }
            try
            {
                SetWindowTheme(c.Handle, IsDark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch
            {
            }
        }

        /// <summary>下拉框配色。</summary>
        public static void StyleCombo(ComboBox box)
        {
            box.FlatStyle = FlatStyle.Flat;
            box.BackColor = Surface;
            box.ForeColor = Ink;
        }

        public static Font Font(float size, bool bold)
        {
            return new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
        }

        public static Button Button(string text, Action action, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.AccessibleName = text;
            button.AutoSize = true;
            button.MinimumSize = new Size(100, 40);
            button.Height = 42;
            button.Padding = new Padding(14, 5, 14, 5);
            button.Margin = new Padding(0, 0, 10, 6);
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = primary ? Purple : Light;
            button.ForeColor = primary ? OnAccent : Purple;
            button.Cursor = Cursors.Hand;
            button.UseMnemonic = false;
            button.Font = Font(10f, false);
            button.FlatAppearance.BorderSize = 0;
            button.Click += delegate
            {
                action();
            };
            return button;
        }

        /// <summary>描边的次要按钮（卡片底色 + 细边框）。</summary>
        public static void Outline(Button b)
        {
            b.BackColor = Surface;
            b.ForeColor = Purple;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Line;
        }

        public static Label Label(string text, float size, bool bold, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = Font(size, bold);
            label.ForeColor = bold ? Ink : Muted;
            label.AutoSize = false;
            label.Height = height;
            label.Dock = DockStyle.Top;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0, 0, 0, 8);
            label.UseMnemonic = false;
            return label;
        }
    }

    public sealed class KanaTile : Button
    {
        public Kana Item;

        public bool Learned;

        public KanaTile(Kana x, bool learned)
        {
            Item = x;
            Learned = learned;
            Text = x.K;
            AccessibleName = x.K + "，" + x.R + (learned ? "，已熟悉" : "");
            Dock = DockStyle.Fill;
            Margin = new Padding(4);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle r = ClientRectangle;
            e.Graphics.Clear(Theme.Paper);
            Color fill = Learned ? Theme.GreenBg : Theme.Surface;
            using (SolidBrush b = new SolidBrush(fill))
            {
                e.Graphics.FillRectangle(b, 1, 1, Math.Max(0, r.Width - 2), Math.Max(0, r.Height - 2));
            }
            using (Pen pen = new Pen(Focused ? Theme.Purple : (Learned ? Theme.GreenBg : Theme.Line), Focused ? 2 : 1))
            {
                e.Graphics.DrawRectangle(pen, 1, 1, Math.Max(0, r.Width - 3), Math.Max(0, r.Height - 3));
            }
            int top = (int)(r.Height * 0.72f);
            int bottom = r.Height - top;
            using (Font font = new Font("Yu Gothic", Math.Max(12f, r.Height * 0.45f), FontStyle.Regular, GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(e.Graphics, Item.K, font, new Rectangle(0, 0, r.Width, top), Theme.Ink, fill, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
            }
            using (Font font2 = new Font("Segoe UI", Math.Max(9f, r.Height * 0.17f), FontStyle.Regular, GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(e.Graphics, Item.R, font2, new Rectangle(0, top, r.Width, bottom), Theme.Muted, fill, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            if (Learned)
            {
                using (Font font3 = new Font("Segoe UI", Math.Max(10f, r.Height * 0.18f), FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    TextRenderer.DrawText(e.Graphics, "✓", font3, new Rectangle(r.Width - bottom, 2, bottom, bottom), Theme.Green, fill);
                }
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
    }

    public sealed class ScopeOption
    {
        public string Id;

        public string Name;

        public override string ToString()
        {
            return Name;
        }
    }
}
