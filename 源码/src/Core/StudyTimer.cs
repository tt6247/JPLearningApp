using System;

namespace KanaSmallSteps
{
    /// <summary>
    /// 专注倒计时。按墙上时钟计算剩余时间，不依赖 Tick 的准确间隔；
    /// 每次 Tick 返回本次应计入学习时长的秒数（单次最多 90 秒，避免电脑睡眠时虚增）。
    /// 与界面无关，可在自检中直接测试。
    /// </summary>
    public sealed class StudyTimer
    {
        public const int MaxSeconds = 7200;

        public const double MaxCreditPerTick = 90;

        public int Total { get; private set; }

        public double Remaining { get; private set; }

        public bool Running { get; private set; }

        private DateTime EndAt;

        private DateTime LastTick;

        public StudyTimer(int minutes)
        {
            Total = Clamp(minutes * 60);
            Remaining = Total;
        }

        private static int Clamp(int seconds)
        {
            return Math.Max(60, Math.Min(MaxSeconds, seconds));
        }

        /// <summary>开始后或暂停中（未走完、未重置）。</summary>
        public bool Started
        {
            get { return Running || (Remaining > 0 && Remaining < Total); }
        }

        public bool Finished
        {
            get { return !Running && Remaining <= 0; }
        }

        /// <summary>已走过的比例 0–1。</summary>
        public double Elapsed
        {
            get { return Total > 0 ? 1 - Remaining / Total : 0; }
        }

        /// <summary>设定时长（运行中无效）。</summary>
        public bool Set(int minutes)
        {
            if (Running)
            {
                return false;
            }
            Total = Clamp(minutes * 60);
            Remaining = Total;
            return true;
        }

        public void Start(DateTime nowUtc)
        {
            if (Running)
            {
                return;
            }
            if (Remaining <= 0)
            {
                Remaining = Total;
            }
            Running = true;
            EndAt = nowUtc.AddSeconds(Remaining);
            LastTick = nowUtc;
        }

        /// <summary>暂停；返回暂停前最后一段应计入的秒数。</summary>
        public double Pause(DateTime nowUtc)
        {
            bool finished;
            double credit = Tick(nowUtc, out finished);
            Running = false;
            return credit;
        }

        public void Reset()
        {
            Running = false;
            Remaining = Total;
        }

        public void AddSeconds(int seconds)
        {
            int newTotal = Math.Min(MaxSeconds, Total + seconds);
            int added = newTotal - Total;
            Total = newTotal;
            Remaining = Math.Min(Total, Remaining + added);
            if (Running)
            {
                EndAt = EndAt.AddSeconds(added);
            }
        }

        public double Tick(DateTime nowUtc, out bool finished)
        {
            finished = false;
            if (!Running)
            {
                LastTick = nowUtc;
                return 0;
            }
            double delta = Math.Max(0, Math.Min((nowUtc - LastTick).TotalSeconds, MaxCreditPerTick));
            LastTick = nowUtc;
            double before = Remaining;
            Remaining = Math.Max(0, (EndAt - nowUtc).TotalSeconds);
            double credit = Math.Min(delta, before);
            if (Remaining <= 0)
            {
                Remaining = 0;
                Running = false;
                finished = true;
            }
            return credit;
        }

        public static string Format(double seconds)
        {
            int t = (int)Math.Ceiling(Math.Max(0, seconds));
            return (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
        }
    }
}
