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
    }

    public sealed class KanaGroup
    {
        public string Id;

        public string Name;

        public string Type;

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
                            Column = j
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
                        Column = l
                    });
                }
                list.Add(kanaGroup4);
            }
            return list;
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
                return All.ToList();
            }
            return Groups.Where((KanaGroup g) => g.Id == scope || g.Type == scope).SelectMany((KanaGroup g) => g.Items).ToList();
        }

        public static string Note(Kana x)
        {
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
