using System;
using System.Collections.Generic;
using System.Linq;

namespace KanaSmallSteps
{
    public static class Randomize
    {
        private static readonly Random Rng = new Random();

        public static List<T> Shuffle<T>(IEnumerable<T> source)
        {
            List<T> list = source.ToList();
            for (int num = list.Count - 1; num > 0; num--)
            {
                int index = Rng.Next(num + 1);
                T value = list[num];
                list[num] = list[index];
                list[index] = value;
            }
            return list;
        }
    }

    public sealed class Quiz
    {
        public List<Kana> Items;

        public List<string> Options;

        public int Index;

        public int Score;

        public bool Submitted;

        public string Selected;

        private readonly string Scope;

        private readonly Progress State;

        /// <summary>read = 看假名选罗马音；pair = 看一种写法选另一种写法（2.5.0），成绩记在片假名上。</summary>
        public readonly string Mode;

        /// <summary>当前题的正确选项。</summary>
        public string Expected
        {
            get
            {
                Kana x = Current;
                if (x == null)
                {
                    return null;
                }
                return Mode == "pair" ? x.Pair : x.R;
            }
        }

        /// <summary>本题成绩记在哪个假名上。</summary>
        public Kana Target
        {
            get
            {
                Kana x = Current;
                if (x != null && Mode == "pair" && x.Script == "hira")
                {
                    return Data.ByKana[x.Pair];
                }
                return x;
            }
        }

        public bool LastRight
        {
            get { return Submitted && Selected == Expected; }
        }

        public bool Done
        {
            get { return Index >= Items.Count; }
        }

        public Kana Current
        {
            get
            {
                if (!Done)
                {
                    return Items[Index];
                }
                return null;
            }
        }

        public Quiz(string scope, Progress p) : this(scope, p, "read")
        {
        }

        public Quiz(string scope, Progress p, string mode)
        {
            Scope = scope;
            State = p;
            Mode = mode == "pair" ? "pair" : "read";
            Items = Randomize.Shuffle(Data.Pool(scope, p)).Take(10).ToList();
            SetOptions();
        }

        private void SetOptions()
        {
            if (Done)
            {
                Options = new List<string>();
                return;
            }
            Kana cur = Current;
            bool pair = Mode == "pair";
            string want = Expected;
            IEnumerable<Kana> source = Randomize.Shuffle(Data.Pool(Scope, State)).Concat(Randomize.Shuffle(Data.Pool(cur.Type, State))).Concat(Randomize.Shuffle(Data.All.Where((Kana k) => k.Script == cur.Script)));
            // 配对题：干扰项取同一种写法的其他假名的另一种写法，保证四个选项字形一致
            IEnumerable<string> first = (from x in source
                where !pair || x.Script == cur.Script
                select pair ? x.Pair : x.R into r
                where r != want
                select r).Distinct().Take(3);
            Options = Randomize.Shuffle(first.Concat(new string[1] { want }));
        }

        public bool Answer(string r)
        {
            if (Done || Submitted || !Options.Contains(r))
            {
                return false;
            }
            Submitted = true;
            Selected = r;
            bool flag = r == Expected;
            if (flag)
            {
                Score++;
            }
            State.Record(Target, flag);
            return true;
        }

        public void Next()
        {
            if (!Done && Submitted)
            {
                Index++;
                Submitted = false;
                Selected = null;
                SetOptions();
            }
        }
    }
}
