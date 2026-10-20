using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KanaSmallSteps
{
    /// <summary>
    /// 主窗口：外观切换（2.2.0 新增）。亮色护眼 / 深色护眼 / 跟随系统。
    /// 页面内容每次 Render 都会按当前配色重建；这里负责常驻控件（侧边栏、状态栏、标题栏、滚动条）的重新上色。
    /// </summary>
    public sealed partial class MainForm
    {
        private UserPreferenceChangedEventHandler SystemThemeHandler;

        private void InitTheme()
        {
            HandleCreated += delegate { Theme.StyleTitleBar(this); };
            Canvas.HandleCreated += delegate { Theme.StyleScrollbars(Canvas); };
            // 跟随系统时，Windows 切换深浅色后立即生效
            SystemThemeHandler = delegate(object s, UserPreferenceChangedEventArgs e)
            {
                if (e.Category == UserPreferenceCategory.General && State.Theme == "system" && IsHandleCreated)
                {
                    BeginInvoke(new Action(ApplyTheme));
                }
            };
            try
            {
                SystemEvents.UserPreferenceChanged += SystemThemeHandler;
            }
            catch
            {
                SystemThemeHandler = null;
            }
            FormClosed += delegate
            {
                if (SystemThemeHandler != null)
                {
                    SystemEvents.UserPreferenceChanged -= SystemThemeHandler;
                }
            };
            ApplyChrome();
        }

        /// <summary>侧边栏按钮：跟随系统 → 亮色护眼 → 深色护眼 → 跟随系统。</summary>
        private void CycleTheme()
        {
            int i = Array.IndexOf(Theme.Modes, State.Theme);
            SetTheme(Theme.Modes[(i + 1) % Theme.Modes.Length]);
        }

        /// <summary>供自检使用。</summary>
        public void UseTheme(string mode)
        {
            SetTheme(mode);
        }

        private void SetTheme(string mode)
        {
            State.Theme = mode;
            Save();
            ApplyTheme();
            Status.Text = "外观：" + Theme.ModeName(mode) + (mode == "system" ? "（当前为" + (Theme.IsDark ? "深色" : "亮色") + "）" : "");
        }

        private void ApplyTheme()
        {
            Theme.Apply(State.Theme);
            SuspendLayout();
            ApplyChrome();
            Render();
            ResumeLayout(true);
            Theme.StyleTitleBar(this);
            Theme.StyleScrollbars(Canvas);
            Invalidate(true);
        }

        /// <summary>给构造函数里创建、换页时不会重建的控件上色。</summary>
        private void ApplyChrome()
        {
            BackColor = Theme.Paper;
            Sidebar.BackColor = Theme.Surface;
            BrandLabel.ForeColor = Theme.Purple;
            TagLabel.ForeColor = Theme.Muted;
            NoteLabel.ForeColor = Theme.Muted;
            StatusBar.BackColor = Theme.Surface;
            Status.ForeColor = State.Warning.Length > 0 ? Theme.BadFg : Theme.Muted;
            Title.ForeColor = Theme.Ink;
            Subtitle.ForeColor = Theme.Muted;
            Canvas.BackColor = Theme.Paper;

            ThemeButton.Text = (Theme.IsDark ? "☾  " : "☀  ") + "外观：" + Theme.ModeName(State.Theme);
            ThemeButton.AccessibleName = ThemeButton.Text + "（点击切换）";
            Theme.Outline(ThemeButton);
            ThemeButton.ForeColor = Theme.Muted;

            if (SidePanel != null)
            {
                SidePanel.BackColor = Theme.Surface;
                SidePanel.Border = Theme.Line;
                SideHead.ForeColor = Theme.Muted;
                SideInfo.ForeColor = Theme.Muted;
                SideTime.ForeColor = Theme.Ink;
                SideToggle.BackColor = Theme.Purple;
                SideToggle.ForeColor = Theme.OnAccent;
                SidePanel.Invalidate(true);
            }
            StatusBar.Invalidate();
            UpdateTimerUI();
        }
    }
}
