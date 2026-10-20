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

        public Quiz(string scope, Progress p)
        {
            Scope = scope;
            State = p;
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
            IEnumerable<Kana> source = Randomize.Shuffle(Data.Pool(Scope, State)).Concat(Randomize.Shuffle(Data.Pool(Current.Type, State))).Concat(Randomize.Shuffle(Data.All));
            IEnumerable<string> first = (from x in source
                select x.R into r
                where r != Current.R
                select r).Distinct().Take(3);
            Options = Randomize.Shuffle(first.Concat(new string[1] { Current.R }));
        }

        public bool Answer(string r)
        {
            if (Done || Submitted || !Options.Contains(r))
            {
                return false;
            }
            Submitted = true;
            Selected = r;
            bool flag = r == Current.R;
            if (flag)
            {
                Score++;
            }
            State.Record(Current, flag);
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
