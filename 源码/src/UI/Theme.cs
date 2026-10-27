using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
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
        /// <summary>对照模式下片假名的颜色（2.5.0，与安卓版 --kata 相同）。</summary>
        public Color Kata;

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
            Track = C(0xE4DDCE), Part = C(0xCEC6E6), Notice = C(0xE9E3EE), NoticeFg = C(0x5A5180), Kata = C(0xA2563F)
        };

        /// <summary>深色护眼：暖灰黑底、米白文字。</summary>
        public static readonly Palette DarkPalette = new Palette
        {
            Name = "深色护眼", Dark = true,
            Bg = C(0x1D1C1A), Surface = C(0x272522), Surface2 = C(0x302D29), Line = C(0x3B3833),
            Ink = C(0xE3DCCF), Muted = C(0xA2998B),
            Accent = C(0xA99DDC), AccentSoft = C(0x35314A), OnAccent = C(0x1D1C1A),
            Green = C(0x83BE9B), GreenBg = C(0x25332B), OkBg = C(0x243A2D), OkFg = C(0x9DD4B1), BadBg = C(0x40292A), BadFg = C(0xE9A79B),
            Track = C(0x35322D), Part = C(0x544C78), Notice = C(0x2C2A36), NoticeFg = C(0xC3BBE6), Kata = C(0xE4A48C)
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
        public static Color Kata { get { return P.Kata; } }

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

        // ---------- 假名字体：Klee One（教科书体，SIL OFL 1.1），内嵌资源 Fonts.KleeOne-SemiBold-kana.ttf ----------
        // 只保留了假名和日文标点（约 70 KB）。先写到本机缓存文件夹再按文件加载（Windows 和 Mono 都可靠），
        // 失败时改用内存加载，再失败退回 Yu Gothic。假名一律用 GDI+（Graphics.DrawString）绘制。

        private const string KanaFontResource = "Fonts.KleeOne-SemiBold-kana.ttf";

        private static PrivateFontCollection KanaFonts;

        private static FontFamily KanaFamily;

        private static IntPtr KanaFontMemory = IntPtr.Zero;

        public static bool KanaFontLoaded
        {
            get { EnsureKanaFont(); return KanaFamily != null; }
        }

        private static byte[] ReadResource(string name)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null)
                {
                    return null;
                }
                using (MemoryStream m = new MemoryStream())
                {
                    s.CopyTo(m);
                    return m.ToArray();
                }
            }
        }

        private static void EnsureKanaFont()
        {
            if (KanaFonts != null)
            {
                return;
            }
            KanaFonts = new PrivateFontCollection();
            byte[] data = null;
            try
            {
                data = ReadResource(KanaFontResource);
            }
            catch
            {
            }
            if (data == null)
            {
                return;
            }
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KanaSmallStepsDesktop", "fonts");
                Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir, "KleeOne-SemiBold-kana-" + data.Length + ".ttf");
                if (!File.Exists(file) || new FileInfo(file).Length != data.Length)
                {
                    File.WriteAllBytes(file, data);
                }
                KanaFonts.AddFontFile(file);
            }
            catch
            {
                try
                {
                    // 缓存文件夹不可写时改用内存加载（内存需在程序运行期间一直有效，故不释放）
                    KanaFontMemory = Marshal.AllocCoTaskMem(data.Length);
                    Marshal.Copy(data, 0, KanaFontMemory, data.Length);
                    KanaFonts.AddMemoryFont(KanaFontMemory, data.Length);
                }
                catch
                {
                }
            }
            if (KanaFonts.Families.Length > 0)
            {
                KanaFamily = KanaFonts.Families[0];
            }
        }

        /// <summary>假名大字用的字体（磅）。</summary>
        public static Font KanaFont(float size)
        {
            EnsureKanaFont();
            return KanaFamily != null ? new Font(KanaFamily, size, FontStyle.Regular) : new Font("Yu Gothic", size);
        }

        /// <summary>假名字体（像素，用于自绘控件）。</summary>
        public static Font KanaFontPx(float pixels)
        {
            EnsureKanaFont();
            return KanaFamily != null ? new Font(KanaFamily, pixels, FontStyle.Regular, GraphicsUnit.Pixel) : new Font("Yu Gothic", pixels, FontStyle.Regular, GraphicsUnit.Pixel);
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

        /// <summary>平片对照模式（2.5.0）：同一格里右边再画片假名。</summary>
        public Kana PairItem;

        public bool PairLearned;

        public KanaTile(Kana x, bool learned, Kana pair, bool pairLearned) : this(x, learned)
        {
            PairItem = pair;
            PairLearned = pairLearned;
            LearnedHira = learned;
            Learned = false;
            Text = x.K + pair.K;
            AccessibleName = x.K + "，" + pair.K + "，" + x.R + (learned ? "，平假名已熟悉" : "") + (pairLearned ? "，片假名已熟悉" : "");
        }

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
            if (PairItem != null)
            {
                // 平片对照（2.6.1）：「あ　a　ア」排成一行，罗马音夹在两种写法中间
                PaintPair(e.Graphics, r, fill);
            }
            else
            {
                using (Font font = Theme.KanaFontPx(Math.Max(12f, r.Height * 0.42f)))
                using (SolidBrush ink = new SolidBrush(Theme.Ink))
                using (StringFormat fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    // 假名用 GDI+ 绘制，直接使用内嵌字体
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    e.Graphics.DrawString(Item.K, font, ink, new RectangleF(0, 2, r.Width, top), fmt);
                }
                using (Font font2 = new Font("Segoe UI", Math.Max(9f, r.Height * 0.17f), FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    TextRenderer.DrawText(e.Graphics, Item.R, font2, new Rectangle(0, top, r.Width, bottom), Theme.Muted, fill, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
            if (Learned)
            {
                using (Font font3 = new Font("Segoe UI", Math.Max(10f, r.Height * 0.18f), FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    TextRenderer.DrawText(e.Graphics, "✓", font3, new Rectangle(r.Width - bottom, 2, bottom, bottom), Theme.Green, fill);
                }
            }
        }

        /// <summary>
        /// 平片对照：左平假名、中间罗马音、右片假名，三者排成一行、整体居中；
        /// 已熟悉的写法下面画一条绿色短线。格子太窄时整体按比例缩小。
        /// </summary>
        private void PaintPair(Graphics g, Rectangle r, Color fill)
        {
            bool combo = Item.K.Length > 1;
            float size = Math.Max(11f, r.Height * (combo ? 0.30f : 0.36f));
            float romSize = Math.Max(9f, r.Height * 0.17f);
            float gap = Math.Max(8f, r.Width * 0.05f);
            float glyphW = size * (combo ? 1.9f : 1.05f);
            float romW;
            using (Font rf = new Font("Segoe UI", romSize, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                romW = TextRenderer.MeasureText(g, Item.R, rf, Size.Empty, TextFormatFlags.NoPadding).Width + 4;
            }
            float total = glyphW * 2 + romW + gap * 2;
            float avail = r.Width * 0.92f;
            if (total > avail)
            {
                float k = avail / total;
                size *= k;
                romSize = Math.Max(8f, romSize * k);
                gap *= k;
                glyphW *= k;
                romW *= k;
                total = avail;
            }
            float x0 = (r.Width - total) / 2f, cy = r.Height / 2f;
            RectangleF left = new RectangleF(x0, 0, glyphW, r.Height);
            RectangleF mid = new RectangleF(x0 + glyphW + gap, 0, romW, r.Height);
            RectangleF right = new RectangleF(x0 + glyphW + gap + romW + gap, 0, glyphW, r.Height);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (Font font = Theme.KanaFontPx(size))
            using (StringFormat fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (SolidBrush ink = new SolidBrush(Theme.Ink))
            using (SolidBrush kata = new SolidBrush(Theme.Kata))
            {
                g.DrawString(Item.K, font, ink, left, fmt);
                g.DrawString(PairItem.K, font, kata, right, fmt);
            }
            using (Font rf = new Font("Segoe UI", romSize, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(g, Item.R, rf, Rectangle.Round(mid), Theme.Muted, fill, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            float lineY = cy + size * 0.62f;
            using (Pen pen = new Pen(Theme.Green, Math.Max(2f, r.Height * 0.03f)))
            {
                if (LearnedHira)
                {
                    g.DrawLine(pen, left.X + left.Width * 0.12f, lineY, left.Right - left.Width * 0.12f, lineY);
                }
                if (PairLearned)
                {
                    g.DrawLine(pen, right.X + right.Width * 0.12f, lineY, right.Right - right.Width * 0.12f, lineY);
                }
            }
        }

        /// <summary>对照模式下平假名是否已熟悉（Learned 在对照模式下用于整格底色，所以单独记）。</summary>
        public bool LearnedHira;

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
