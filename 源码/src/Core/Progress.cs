using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace KanaSmallSteps
{
    /// <summary>某一天的学习记录。</summary>
    public sealed class DayLog
    {
        /// <summary>计时器累计的专注秒数。</summary>
        public double Seconds;

        /// <summary>记忆卡上点「我记住了」或「还不熟」的次数。</summary>
        public int Cards;

        public int Answered;

        public int Correct;

        /// <summary>当天新标记为「已熟悉」的假名数。</summary>
        public int Learned;

        /// <summary>当天「学习新的一行」的目标行 Id；当天内固定，学完显示为已完成。</summary>
        public string Group;

        public bool Active
        {
            get { return Seconds >= 30 || Cards > 0 || Answered > 0 || Learned > 0; }
        }
    }

    /// <summary>
    /// 学习进度。保存为 XML（progress.xml），写入时先写临时文件再原子替换，并保留 .bak 备份。
    /// 版本 3 在版本 2 的基础上增加 goal / timer 属性和 &lt;day&gt; 元素；旧版程序会忽略这些新内容。
    /// </summary>
    public sealed class Progress
    {
        public const int FileVersion = 3;

        public const int KeepDays = 120;

        public static readonly int[] GoalChoices = new int[] { 5, 10, 15, 20, 30, 45, 60 };

        public static readonly int[] TimerChoices = new int[] { 5, 10, 15, 25 };

        public HashSet<string> Learned = new HashSet<string>();

        /// <summary>单词卡里已记住的词（2.6.0）。</summary>
        public HashSet<string> Words = new HashSet<string>();

        public Dictionary<string, int> Errors = new Dictionary<string, int>();

        public Dictionary<string, DayLog> Days = new Dictionary<string, DayLog>();

        public int Answered;

        public int Correct;

        /// <summary>每日目标（分钟）。</summary>
        public int Goal = 15;

        /// <summary>上次选择的计时时长（分钟）。</summary>
        public int TimerMinutes = 15;

        /// <summary>外观：system 跟随系统 / light 亮色护眼 / dark 深色护眼（2.2.0）。</summary>
        public string Theme = "system";

        public static readonly string[] ThemeChoices = new string[] { "system", "light", "dark" };

        /// <summary>五十音表显示的字形：hira 平假名 / kata 片假名 / both 平片对照（2.5.0）。</summary>
        public string Script = "hira";

        public static readonly string[] ScriptChoices = new string[] { "hira", "kata", "both" };

        public readonly string Path;

        public string Warning = "";

        /// <summary>可替换的「今天」，便于自检测试。</summary>
        public Func<DateTime> Clock = delegate { return DateTime.Now; };

        public Progress(string path)
        {
            Path = path;
        }

        public static string DayKey(DateTime d)
        {
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static Progress Load(string path)
        {
            Progress progress = new Progress(path);
            if (!File.Exists(path))
            {
                return progress;
            }
            try
            {
                progress.Read(path);
            }
            catch
            {
                progress = new Progress(path);
                try
                {
                    progress.Read(path + ".bak");
                    progress.Warning = "主进度文件损坏，已读取备份。";
                }
                catch
                {
                    progress.Warning = "进度文件无法读取。原文件已保留，本次从新进度开始。";
                }
            }
            return progress;
        }

        private static int IntAttr(XElement e, string name)
        {
            int v;
            int.TryParse((string)e.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
            return Math.Max(0, v);
        }

        private void Read(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Prohibit;
            settings.XmlResolver = null;
            XDocument doc;
            using (XmlReader reader = XmlReader.Create(path, settings))
            {
                doc = XDocument.Load(reader);
            }
            if (doc.Root == null || doc.Root.Name != "kanaProgress")
            {
                throw new InvalidDataException();
            }
            XElement root = doc.Root;
            Answered = IntAttr(root, "answered");
            Correct = Math.Min(Answered, IntAttr(root, "correct"));
            int goal = IntAttr(root, "goal");
            if (GoalChoices.Contains(goal))
            {
                Goal = goal;
            }
            int timer = IntAttr(root, "timer");
            if (timer >= 1 && timer <= 120)
            {
                TimerMinutes = timer;
            }
            string theme = (string)root.Attribute("theme");
            if (theme != null && ThemeChoices.Contains(theme))
            {
                Theme = theme;
            }
            string script = (string)root.Attribute("script");
            if (script != null && ScriptChoices.Contains(script))
            {
                Script = script;
            }
            foreach (XElement item in root.Elements("word"))
            {
                string w = (string)item.Attribute("value");
                if (w != null && KanaSmallSteps.Words.Get(w) != null)
                {
                    Words.Add(w);
                }
            }
            foreach (XElement item in root.Elements("kana"))
            {
                string text = (string)item.Attribute("value");
                if (text != null && Data.ByKana.ContainsKey(text))
                {
                    if ((string)item.Attribute("learned") == "true")
                    {
                        Learned.Add(text);
                    }
                    int review = IntAttr(item, "review");
                    if (review > 0)
                    {
                        Errors[text] = Math.Min(2, review);
                    }
                }
            }
            foreach (XElement item in root.Elements("day"))
            {
                DateTime date;
                if (!DateTime.TryParseExact((string)item.Attribute("date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    continue;
                }
                DayLog log = new DayLog();
                log.Seconds = Math.Min(86400, IntAttr(item, "sec"));
                log.Cards = IntAttr(item, "cards");
                log.Answered = IntAttr(item, "answered");
                log.Correct = Math.Min(log.Answered, IntAttr(item, "correct"));
                log.Learned = IntAttr(item, "learned");
                string group = (string)item.Attribute("group");
                if (group != null && Data.Groups.Any(g => g.Id == group))
                {
                    log.Group = group;
                }
                Days[DayKey(date)] = log;
            }
        }

        public bool Save()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                XElement root = new XElement("kanaProgress",
                    new XAttribute("version", FileVersion),
                    new XAttribute("answered", Answered),
                    new XAttribute("correct", Correct),
                    new XAttribute("goal", Goal),
                    new XAttribute("timer", TimerMinutes),
                    new XAttribute("theme", Theme),
                    new XAttribute("script", Script));
                foreach (string w in Words.OrderBy(k => k, StringComparer.Ordinal))
                {
                    root.Add(new XElement("word", new XAttribute("value", w)));
                }
                foreach (Kana item in Data.All)
                {
                    int review;
                    Errors.TryGetValue(item.K, out review);
                    if (Learned.Contains(item.K) || review > 0)
                    {
                        root.Add(new XElement("kana",
                            new XAttribute("value", item.K),
                            new XAttribute("learned", Learned.Contains(item.K)),
                            new XAttribute("review", review)));
                    }
                }
                foreach (string key in Days.Keys.OrderBy(k => k, StringComparer.Ordinal).Reverse().Take(KeepDays).Reverse())
                {
                    DayLog log = Days[key];
                    if (log.Seconds < 1 && log.Cards == 0 && log.Answered == 0 && log.Learned == 0 && log.Group == null)
                    {
                        continue;
                    }
                    XElement day = new XElement("day",
                        new XAttribute("date", key),
                        new XAttribute("sec", (int)Math.Round(log.Seconds)),
                        new XAttribute("cards", log.Cards),
                        new XAttribute("answered", log.Answered),
                        new XAttribute("correct", log.Correct),
                        new XAttribute("learned", log.Learned));
                    if (log.Group != null)
                    {
                        day.Add(new XAttribute("group", log.Group));
                    }
                    root.Add(day);
                }
                string temp = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                new XDocument(root).Save(temp);
                if (File.Exists(Path))
                {
                    File.Replace(temp, Path, Path + ".bak");
                }
                else
                {
                    File.Move(temp, Path);
                }
                Warning = "";
                return true;
            }
            catch
            {
                Warning = "暂时无法保存进度，本次仍可继续学习。请检查本地文件夹写入权限。";
                return false;
            }
        }

        // ---------- 每日记录 ----------

        public DayLog Today
        {
            get { return Day(Clock()); }
        }

        public DayLog Day(DateTime date)
        {
            string key = DayKey(date);
            DayLog log;
            if (!Days.TryGetValue(key, out log))
            {
                log = new DayLog();
                Days[key] = log;
            }
            return log;
        }

        public bool StudiedOn(DateTime date)
        {
            DayLog log;
            return Days.TryGetValue(DayKey(date), out log) && log.Active;
        }

        /// <summary>连续学习天数：今天还没学时，从昨天开始往前数。</summary>
        public int Streak()
        {
            DateTime d = Clock().Date;
            if (!StudiedOn(d))
            {
                d = d.AddDays(-1);
            }
            int n = 0;
            while (StudiedOn(d) && n < 3660)
            {
                n++;
                d = d.AddDays(-1);
            }
            return n;
        }

        /// <summary>今天之前是否有学习记录。</summary>
        public bool HasEarlierDays()
        {
            string today = DayKey(Clock());
            return Days.Any(p => string.CompareOrdinal(p.Key, today) < 0 && p.Value.Active);
        }

        /// <summary>近 7 天正确率（至少 10 题），不足时用累计正确率（至少 20 题），都不足返回 -1。</summary>
        public double RecentAccuracy()
        {
            int answered = 0;
            int correct = 0;
            DateTime d = Clock().Date;
            for (int i = 0; i < 7; i++)
            {
                DayLog log;
                if (Days.TryGetValue(DayKey(d.AddDays(-i)), out log))
                {
                    answered += log.Answered;
                    correct += log.Correct;
                }
            }
            if (answered >= 10)
            {
                return (double)correct / answered;
            }
            if (Answered >= 20)
            {
                return (double)Correct / Answered;
            }
            return -1;
        }

        public void AddStudySeconds(double seconds)
        {
            if (seconds > 0)
            {
                DayLog log = Today;
                log.Seconds = Math.Min(86400, log.Seconds + seconds);
            }
        }

        // ---------- 学习动作 ----------

        public void MarkAgain(Kana x)
        {
            Learned.Remove(x.K);
            Errors[x.K] = 2;
        }

        /// <summary>记忆卡「我记住了」。</summary>
        /// <summary>单词卡：记住了 / 还不熟（翻卡次数计入当天记录）。</summary>
        public void WordKnown(Word w)
        {
            Today.Cards++;
            Words.Add(w.K);
        }

        public void WordAgain(Word w)
        {
            Today.Cards++;
            Words.Remove(w.K);
        }

        public void CardKnown(Kana x)
        {
            DayLog log = Today;
            log.Cards++;
            if (Learned.Add(x.K))
            {
                log.Learned++;
            }
        }

        /// <summary>详情窗口里的「标记为已熟悉 / 取消熟悉标记」。</summary>
        public void ToggleLearned(Kana x)
        {
            DayLog log = Today;
            if (Learned.Remove(x.K))
            {
                log.Learned = Math.Max(0, log.Learned - 1);
            }
            else
            {
                Learned.Add(x.K);
                log.Learned++;
            }
        }

        /// <summary>记忆卡「还不熟，再练练」。</summary>
        public void CardAgain(Kana x)
        {
            Today.Cards++;
            MarkAgain(x);
        }

        public void Record(Kana x, bool right)
        {
            DayLog log = Today;
            if (Answered < int.MaxValue)
            {
                Answered++;
                log.Answered++;
            }
            if (right)
            {
                if (Correct < Answered)
                {
                    Correct++;
                }
                if (log.Correct < log.Answered)
                {
                    log.Correct++;
                }
                if (Errors.ContainsKey(x.K))
                {
                    Errors[x.K]--;
                    if (Errors[x.K] <= 0)
                    {
                        Errors.Remove(x.K);
                    }
                }
                // 2.1.1：测验答对且没有待复习次数时，视为已熟悉（与答错会取消熟悉相对应）
                if (!Errors.ContainsKey(x.K) && Learned.Add(x.K))
                {
                    log.Learned++;
                }
            }
            else
            {
                MarkAgain(x);
            }
        }
    }
}
