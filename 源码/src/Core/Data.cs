using System;
using System.Collections.Generic;
using System.Linq;

namespace KanaSmallSteps
{
    public sealed class Kana
    {
        public string K;

        public string R;

        public string Group;

        public string Type;

        public int Column;

        /// <summary>hira = 平假名，kata = 片假名（2.5.0）。</summary>
        public string Script = "hira";

        /// <summary>另一种写法（あ ↔ ア）。</summary>
        public string Pair;
    }

    public sealed class KanaGroup
    {
        public string Id;

        public string Name;

        public string Type;

        public string Script = "hira";

        public List<Kana> Items = new List<Kana>();
    }

    public static class Data
    {
        public static readonly List<KanaGroup> Groups = Build();

        public static readonly List<Kana> All = Groups.SelectMany((KanaGroup g) => g.Items).ToList();

        public static readonly Dictionary<string, Kana> ByKana = All.ToDictionary((Kana k) => k.K);

        private static List<KanaGroup> Build()
        {
            string[] array = new string[16]
            {
                "a|元音|あいうえお|a i u e o", "k|か行|かきくけこ|ka ki ku ke ko", "s|さ行|さしすせそ|sa shi su se so", "t|た行|たちつてと|ta chi tsu te to", "n|な行|なにぬねの|na ni nu ne no", "h|は行|はひふへほ|ha hi fu he ho", "m|ま行|まみむめも|ma mi mu me mo", "y|や行|や ゆ よ|ya - yu - yo", "r|ら行|らりるれろ|ra ri ru re ro", "w|わ行|わ   を|wa - - - wo",
                "nn|拨音|ん    |n - - - -", "g|が行|がぎぐげご|ga gi gu ge go", "z|ざ行|ざじずぜぞ|za ji zu ze zo", "d|だ行|だぢづでど|da ji zu de do", "b|ば行|ばびぶべぼ|ba bi bu be bo", "p|ぱ行|ぱぴぷぺぽ|pa pi pu pe po"
            };
            List<KanaGroup> list = new List<KanaGroup>();
            for (int i = 0; i < array.Length; i++)
            {
                string[] array2 = array[i].Split('|');
                KanaGroup kanaGroup = new KanaGroup();
                kanaGroup.Id = array2[0];
                kanaGroup.Name = array2[1];
                kanaGroup.Type = ((i < 11) ? "basic" : "voiced");
                KanaGroup kanaGroup2 = kanaGroup;
                string[] array3 = array2[3].Split(' ');
                for (int j = 0; j < array2[2].Length; j++)
                {
                    if (array2[2][j] != ' ')
                    {
                        kanaGroup2.Items.Add(new Kana
                        {
                            K = array2[2][j].ToString(),
                            R = array3[j],
                            Group = kanaGroup2.Id,
                            Type = kanaGroup2.Type,
                            Column = j,
                            Pair = ToKata(array2[2][j].ToString())
                        });
                    }
                }
                list.Add(kanaGroup2);
            }
            string[] array4 = new string[11]
            {
                "ky|きゃ きゅ きょ|kya kyu kyo", "sh|しゃ しゅ しょ|sha shu sho", "ch|ちゃ ちゅ ちょ|cha chu cho", "ny|にゃ にゅ にょ|nya nyu nyo", "hy|ひゃ ひゅ ひょ|hya hyu hyo", "my|みゃ みゅ みょ|mya myu myo", "ry|りゃ りゅ りょ|rya ryu ryo", "gy|ぎゃ ぎゅ ぎょ|gya gyu gyo", "j|じゃ じゅ じょ|ja ju jo", "by|びゃ びゅ びょ|bya byu byo",
                "py|ぴゃ ぴゅ ぴょ|pya pyu pyo"
            };
            string[] array5 = array4;
            foreach (string text in array5)
            {
                string[] array6 = text.Split('|');
                string[] array7 = array6[1].Split(' ');
                string[] array8 = array6[2].Split(' ');
                KanaGroup kanaGroup3 = new KanaGroup();
                kanaGroup3.Id = array6[0];
                kanaGroup3.Name = array7[0] + "组";
                kanaGroup3.Type = "combo";
                KanaGroup kanaGroup4 = kanaGroup3;
                for (int l = 0; l < array7.Length; l++)
                {
                    kanaGroup4.Items.Add(new Kana
                    {
                        K = array7[l],
                        R = array8[l],
                        Group = kanaGroup4.Id,
                        Type = kanaGroup4.Type,
                        Column = l,
                        Pair = ToKata(array7[l])
                    });
                }
                list.Add(kanaGroup4);
            }
            // 2.5.0：每个平假名组都有对应的片假名组（id 加前缀 K，type 加前缀 k），行列和读音相同
            foreach (KanaGroup h in list.ToList())
            {
                KanaGroup g = new KanaGroup { Id = "K" + h.Id, Name = ToKata(h.Name), Type = "k" + h.Type, Script = "kata" };
                foreach (Kana x in h.Items)
                {
                    g.Items.Add(new Kana { K = x.Pair, R = x.R, Group = g.Id, Type = g.Type, Column = x.Column, Script = "kata", Pair = x.K });
                }
                list.Add(g);
            }
            return list;
        }

        /// <summary>平假名 → 片假名（两者在 Unicode 中相差 0x60）。</summary>
        public static string ToKata(string s)
        {
            char[] a = s.ToCharArray();
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] >= '\u3041' && a[i] <= '\u3096')
                {
                    a[i] = (char)(a[i] + 0x60);
                }
            }
            return new string(a);
        }

        public static string ToHira(string s)
        {
            char[] a = s.ToCharArray();
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] >= '\u30A1' && a[i] <= '\u30F6')
                {
                    a[i] = (char)(a[i] - 0x60);
                }
            }
            return new string(a);
        }

        public static KanaGroup GroupById(string id)
        {
            return Groups.FirstOrDefault((KanaGroup g) => g.Id == id);
        }

        /// <summary>不区分平片的类别：basic / voiced / combo。</summary>
        public static string BaseType(Kana x)
        {
            return x.Type.StartsWith("k") ? x.Type.Substring(1) : x.Type;
        }

        /// <summary>某类别在某种写法下的范围 id：("basic", "kata") → "kbasic"。</summary>
        public static string TypeFor(string category, string script)
        {
            return (script == "kata" ? "k" : "") + category;
        }

        /// <summary>用于列表和计划的组名：片假名组加前缀，避免「元音」「拨音」两种写法同名。</summary>
        public static string GroupLabel(KanaGroup g)
        {
            return (g.Script == "kata" ? "片假名 · " : "") + g.Name;
        }

        public static List<Kana> Pool(string scope, Progress p)
        {
            if (scope == "errors")
            {
                return All.Where((Kana k) => p.Errors.ContainsKey(k.K)).ToList();
            }
            if (scope == "learned")
            {
                return All.Where((Kana k) => p.Learned.Contains(k.K)).ToList();
            }
            if (scope == "all")
            {
                return All.Where((Kana k) => k.Script == "hira").ToList();
            }
            if (scope == "kall")
            {
                return All.Where((Kana k) => k.Script == "kata").ToList();
            }
            if (scope == "mix")
            {
                return All.ToList();
            }
            return Groups.Where((KanaGroup g) => g.Id == scope || g.Type == scope).SelectMany((KanaGroup g) => g.Items).ToList();
        }

        /// <summary>
        /// 记忆卡的牌组（2.4.0）：默认与五十音表相同的顺序——先按行（あ行、か行……），行内按 a i u e o 列；
        /// shuffled 为 true 时打乱。
        /// </summary>
        public static List<Kana> Deck(string scope, Progress p, bool shuffled)
        {
            List<Kana> pool = Pool(scope, p).OrderBy((Kana k) => All.IndexOf(k)).ToList();
            return shuffled ? Randomize.Shuffle(pool) : pool;
        }

        private static readonly Dictionary<string, string> KataConfuse = new Dictionary<string, string>
        {
            { "シ", "シ 和 ツ 形近：シ 的两点在左边上下排，笔势偏横，最后一笔从左下往右上挑；ツ 的两点在上方左右排，笔势偏竖，最后一笔从右上往左下撇。" },
            { "ツ", "ツ 和 シ 形近：ツ 的两点在上方左右排，笔势偏竖，最后一笔从右上往左下撇；シ 的两点在左边上下排，最后一笔从左下往右上挑。" },
            { "ソ", "ソ 和 ン 形近：ソ 的点偏竖，第二笔从右上往左下撇；ン 的点偏横，第二笔从左下往右上挑。" },
            { "ン", "ン 和 ソ 形近：ン 的点偏横，第二笔从左下往右上挑；ソ 的点偏竖，第二笔从右上往左下撇。" }
        };

        private const string KataLookalike = "カキヘリモヤセ";

        /// <summary>片假名的额外提示（形近字、与平假名相像的字），没有时返回空字符串。与安卓版 kataHint 相同。</summary>
        public static string KataHint(Kana x)
        {
            string k = x.Script == "kata" ? x.K : x.Pair;
            string t;
            if (KataConfuse.TryGetValue(k, out t))
            {
                return t;
            }
            if (k.Length == 1 && KataLookalike.IndexOf(k, StringComparison.Ordinal) >= 0)
            {
                return k + " 和平假名「" + ToHira(k) + "」长得很像，可以对照着记。";
            }
            return "";
        }

        public static string Note(Kana x)
        {
            if (x.Script == "kata")
            {
                if (x.K == "ヲ")
                {
                    return "通常读 o，现代多只出现在特殊用法中；罗马音写作 wo。";
                }
                if (x.K == "ン")
                {
                    return "拨音，占一拍；" + KataConfuse["ン"];
                }
                if (x.K == "ヂ" || x.K == "ヅ")
                {
                    return "ヂ通常与ジ同音，ヅ通常与ズ同音，实际很少用到。";
                }
                if (x.K.Length > 1)
                {
                    return "后一个字是小写的ャ、ュ或ョ；两个字合起来占一拍，例如 キャ。";
                }
                string hint = KataHint(x);
                return hint.Length > 0 ? hint : "片假名多用于外来语、拟声词和强调，读音与平假名「" + x.Pair + "」相同。";
            }
            if (x.K == "を")
            {
                return "通常读 o；罗马音写作 wo，用于区别「お」。";
            }
            if (x.K == "は")
            {
                return "通常读 ha；作为助词时读 wa。";
            }
            if (x.K == "へ")
            {
                return "通常读 he；作为助词时读 e。";
            }
            if (x.K == "ん")
            {
                return "拨音，占一拍；实际发音会随后续音变化。";
            }
            if (x.K == "ぢ" || x.K == "づ")
            {
                return "现代标准日语中，ぢ通常与じ同音，づ通常与ず同音。";
            }
            if (x.K.Length > 1)
            {
                return "后一个字是小写的ゃ、ゅ或ょ；两个字合起来占一拍。";
            }
            return "观察字形，再听发音。罗马音只是辅助提示，不按中文拼音读。";
        }
    }
}
