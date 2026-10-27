using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace KanaSmallSteps
{
    /// <summary>一张单词卡（2.6.0）。</summary>
    public sealed class Word
    {
        public string K;

        public string Zh;

        public string Kanji;

        public string Ex;

        public string ExZh;

        /// <summary>读这个词需要先熟悉的假名（拗音算一个；「っ / ッ」按「つ / ツ」算；长音「ー」不单独要求）。</summary>
        public List<string> Needs;

        public string Romaji;

        /// <summary>hira = 平假名词，kata = 片假名词。</summary>
        public string Script;

        public bool Sokuon;

        public bool Long;

        /// <summary>最晚学到的那个假名在五十音表中的位置，用于排序。</summary>
        public int Order;

        public int Index;
    }

    /// <summary>
    /// 单词卡词表（2.6.0）。来自内嵌资源 Words.words.json：JLPT N5 词汇（Jonathan Waller，CC BY），
    /// 只收能用本程序假名读出的词；中文释义、汉字写法说明和例句为本项目编写。与安卓版 core.js 的 Words 模块规则相同。
    /// </summary>
    public static class Words
    {
        private static List<Word> list;

        private static Dictionary<string, Word> byK;

        public static List<Word> All
        {
            get
            {
                if (list == null)
                {
                    Load();
                }
                return list;
            }
        }

        public static Word Get(string k)
        {
            Word w;
            if (list == null)
            {
                Load();
            }
            return byK.TryGetValue(k, out w) ? w : null;
        }

        private static void Load()
        {
            list = new List<Word>();
            byK = new Dictionary<string, Word>();
            string json = "";
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Words.words.json"))
                {
                    if (s != null)
                    {
                        using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                        {
                            json = r.ReadToEnd();
                        }
                    }
                }
            }
            catch
            {
                // 词表缺失时单词卡显示为空
            }
            int i = 0;
            foreach (Match m in Regex.Matches(json, "\\[\"([^\"]*)\",\"([^\"]*)\",\"([^\"]*)\",\"([^\"]*)\",\"([^\"]*)\"\\]"))
            {
                string k = m.Groups[1].Value;
                if (k.Length == 0 || byK.ContainsKey(k))
                {
                    continue;
                }
                List<string> needs = NeedsOf(k);
                Word w = new Word
                {
                    K = k,
                    Zh = m.Groups[2].Value,
                    Kanji = m.Groups[3].Value,
                    Ex = m.Groups[4].Value,
                    ExZh = m.Groups[5].Value,
                    Needs = needs,
                    Romaji = RomajiOf(k),
                    Script = Regex.IsMatch(k, "[\\u30A0-\\u30FF]") ? "kata" : "hira",
                    Sokuon = k.IndexOf('っ') >= 0 || k.IndexOf('ッ') >= 0,
                    Long = k.IndexOf('ー') >= 0,
                    Order = needs.Count == 0 ? -1 : needs.Max((string c) => Data.All.IndexOf(Data.ByKana[c])),
                    Index = i++
                };
                list.Add(w);
                byK[k] = w;
            }
            list = list.OrderBy((Word w) => w.Order).ThenBy((Word w) => w.Index).ToList();
        }

        /// <summary>按假名表的单位切分：拗音算一个单位，另外保留「っ ッ ー」，其他字符忽略。</summary>
        public static List<string> Tokenize(string text)
        {
            List<string> out1 = new List<string>();
            for (int i = 0; i < text.Length; i++)
            {
                if (i + 1 < text.Length && Data.ByKana.ContainsKey(text.Substring(i, 2)))
                {
                    out1.Add(text.Substring(i, 2));
                    i++;
                    continue;
                }
                string c = text.Substring(i, 1);
                if (Data.ByKana.ContainsKey(c) || c == "っ" || c == "ッ" || c == "ー")
                {
                    out1.Add(c);
                }
            }
            return out1;
        }

        public static List<string> NeedsOf(string text)
        {
            List<string> out1 = new List<string>();
            foreach (string u0 in Tokenize(text))
            {
                string u = u0;
                if (u == "ー")
                {
                    continue;
                }
                if (u == "っ")
                {
                    u = "つ";
                }
                if (u == "ッ")
                {
                    u = "ツ";
                }
                if (!out1.Contains(u))
                {
                    out1.Add(u);
                }
            }
            return out1;
        }

        public static string RomajiOf(string text)
        {
            StringBuilder sb = new StringBuilder();
            bool doubleNext = false;
            foreach (string u in Tokenize(text))
            {
                if (u == "っ" || u == "ッ")
                {
                    doubleNext = true;
                    continue;
                }
                if (u == "ー")
                {
                    if (sb.Length > 0 && "aiueo".IndexOf(sb[sb.Length - 1]) >= 0)
                    {
                        sb.Append(sb[sb.Length - 1]);
                    }
                    continue;
                }
                string r = Data.ByKana[u].R;
                if (doubleNext)
                {
                    sb.Append(r.StartsWith("ch") ? 't' : r[0]);
                    doubleNext = false;
                }
                sb.Append(r);
            }
            return sb.ToString();
        }

        public static bool Readable(Word w, Progress p)
        {
            return w.Needs.All((string c) => p.Learned.Contains(c));
        }

        public static bool CanRead(string text, Progress p)
        {
            return NeedsOf(text).All((string c) => p.Learned.Contains(c));
        }

        public static List<string> Missing(Word w, Progress p)
        {
            return w.Needs.Where((string c) => !p.Learned.Contains(c)).ToList();
        }

        private static bool Match(Word w, string filter, Progress p)
        {
            if (filter == "hira")
            {
                return w.Script == "hira";
            }
            if (filter == "kata")
            {
                return w.Script == "kata";
            }
            if (filter == "todo")
            {
                return !p.Words.Contains(w.K);
            }
            return true;
        }

        /// <summary>能读的词（按解锁顺序）。filter：all / hira / kata / todo（还没记住）。</summary>
        public static List<Word> Deck(Progress p, string filter)
        {
            return All.Where((Word w) => Readable(w, p) && Match(w, filter, p)).ToList();
        }

        public static List<Word> Locked(Progress p, string filter)
        {
            return All.Where((Word w) => !Readable(w, p) && Match(w, filter, p)).ToList();
        }

        /// <summary>学完某一组后能新解锁几个词。</summary>
        public static int UnlockBy(Progress p, KanaGroup g)
        {
            if (g == null)
            {
                return 0;
            }
            HashSet<string> extra = new HashSet<string>(g.Items.Select((Kana x) => x.K));
            return All.Count((Word w) => !Readable(w, p) && w.Needs.All((string c) => p.Learned.Contains(c) || extra.Contains(c)));
        }

        /// <summary>卡片背面的补充说明：促音、长音。</summary>
        public static List<string> Notes(Word w)
        {
            List<string> out1 = new List<string>();
            if (w.Sokuon)
            {
                out1.Add("小写的「" + (w.Script == "kata" ? "ッ" : "っ") + "」是促音：停顿一拍，再发下一个音。");
            }
            if (w.Long)
            {
                out1.Add("「ー」是长音：把前一个音拉长一拍。");
            }
            return out1;
        }
    }
}
