using System;
using System.IO;
using System.Windows.Forms;

namespace KanaSmallSteps
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(defaultValue: false);
            if (args.Length == 2 && args[0] == "--self-test")
            {
                return SelfTest.Run(args[1]);
            }
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KanaSmallStepsDesktop");
            if (args.Length == 2 && args[0] == "--data-dir")
            {
                path = Path.GetFullPath(args[1]);
            }
            try
            {
                Application.Run(new MainForm(Progress.Load(Path.Combine(path, "progress.xml"))));
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("应用无法启动：\n" + ex.Message, "假名小步", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
