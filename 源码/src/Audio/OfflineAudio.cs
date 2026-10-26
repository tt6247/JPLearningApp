using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace KanaSmallSteps
{
    public sealed class OfflineAudio : IDisposable
    {
        private readonly string Folder;

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
        private static extern uint Send(string command, StringBuilder output, int length, IntPtr callback);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciGetErrorStringW")]
        private static extern bool ErrorString(uint error, StringBuilder output, int length);

        public OfflineAudio(string folder)
        {
            Folder = folder;
        }

        public static string Key(Kana x)
        {
            if (x.Script == "kata")
            {
                x = Data.ByKana[x.Pair]; // 片假名与平假名读音相同，共用录音
            }
            string text = ((x.Type == "basic") ? "seion" : ((x.Type == "combo") ? "yoon" : ((x.Group == "p") ? "handakuon" : "dakuon")));
            string text2 = x.R;
            if (x.K == "ぢ")
            {
                text2 = "di";
            }
            if (x.K == "づ")
            {
                text2 = "du";
            }
            if (x.Group == "sh")
            {
                text2 = "sy" + x.R.Substring(x.R.Length - 1);
            }
            if (x.Group == "ch")
            {
                text2 = "cy" + x.R.Substring(x.R.Length - 1);
            }
            if (x.Group == "j")
            {
                text2 = "zy" + x.R.Substring(x.R.Length - 1);
            }
            return text + "." + text2;
        }

        public static byte[] Bytes(Kana x)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("KanaAudio." + Key(x)))
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    if (stream == null)
                    {
                        throw new InvalidDataException("找不到内置发音：" + x.K);
                    }
                    stream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
        }

        public int Open(Kana x)
        {
            Directory.CreateDirectory(Folder);
            string text = Path.Combine(Folder, Key(x) + ".mp3");
            byte[] array = Bytes(x);
            if (!File.Exists(text) || !File.ReadAllBytes(text).SequenceEqual(array))
            {
                File.WriteAllBytes(text, array);
            }
            Send("close kanaSmallStepsAudio", null, 0, IntPtr.Zero);
            Require(Send("open \"" + text + "\" type mpegvideo alias kanaSmallStepsAudio", null, 0, IntPtr.Zero));
            Require(Send("set kanaSmallStepsAudio time format milliseconds", null, 0, IntPtr.Zero));
            StringBuilder stringBuilder = new StringBuilder(64);
            Require(Send("status kanaSmallStepsAudio length", stringBuilder, stringBuilder.Capacity, IntPtr.Zero));
            return int.Parse(stringBuilder.ToString());
        }

        private static void Require(uint error)
        {
            if (error == 0)
            {
                return;
            }
            StringBuilder stringBuilder = new StringBuilder(256);
            ErrorString(error, stringBuilder, stringBuilder.Capacity);
            throw new InvalidOperationException(stringBuilder.ToString());
        }

        public void Play(Kana x)
        {
            Open(x);
            Require(Send("play kanaSmallStepsAudio from 0", null, 0, IntPtr.Zero));
        }

        public void Dispose()
        {
            try
            {
                Send("close kanaSmallStepsAudio", null, 0, IntPtr.Zero);
            }
            catch (DllNotFoundException)
            {
                // 非 Windows 环境（如 Mono 自检）没有 winmm.dll
            }
        }
    }
}
