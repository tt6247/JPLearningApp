using System;
using System.Collections.Generic;
using System.Linq;

namespace KanaSmallSteps
{
    public enum PlanAction
    {
        None,
        ReviewQuiz,
        Warmup,
        LearnGroup,
        Quiz,
        AllQuiz,
        StartTimer
    }

    public sealed class PlanTask
    {
        public string Title;

        public string Detail;

        public bool Done;

        public string Button;

        public string DoneButton = "再来一次";

        public PlanAction Action;

        public string Scope;
    }

    public sealed class WeekDay
    {
        public DateTime Date;

        public string Label;

        public int Minutes;

        public bool Studied;

        public bool IsToday;
    }

    /// <summary>根据学习进度生成「今日学习建议」。纯逻辑，无界面依赖。</summary>
    public static class DailyPlan
    {
        /// <summary>错题达到这个数量时，建议当天不学新内容。</summary>
        public const int HeavyReview = 8;

        public static readonly string[] Tips = new string[]
        {
            "把假名大声读出来，比默看记得更牢。",
            "把容易混淆的假名放在一起对比：さ / ち、ぬ / め、る / ろ、わ / れ / ね。",
            "学新的一行之前，先花两分钟复习昨天的内容。",
            "记忆卡想不起来时，先停三秒再翻面——努力回想本身就在加深记忆。",
            "用手指在桌上描一描字形，动作记忆能帮你认字。",
            "在身边找假名：包装、歌词、动画片头，每认出一个都是练习。",
            "短而频繁胜过长而偶尔：每天 15 分钟，比周末一次两小时更有效。",
            "测验答错不要紧，错题会自动回来找你，这就是间隔复习。",
            "「こんにちは」最后的は读 wa——は作助词时就是这样读。",
            "带着一点「意犹未尽」结束，明天会更想回来。"
        };

        public static string TipOfDay(DateTime today)
        {
            return Tips[today.DayOfYear % Tips.Length];
        }

        public static string Greeting(DateTime now)
        {
            int h = now.Hour;
            if (h < 5) return "夜深了";
            if (h < 11) return "早上好";
            if (h < 14) return "中午好";
            if (h < 18) return "下午好";
            return "晚上好";
        }

        /// <summary>页面副标题里的一句个性化提示。</summary>
        public static string Coach(Progress p)
        {
            DayLog today = p.Today;
            int streak = p.Streak();
            int errors = p.Errors.Count;
            double acc = p.RecentAccuracy();
            if (p.Learned.Count == 0 && p.Answered == 0)
            {
                return "第一次来？从元音「あいうえお」开始，今天认识 5 个假名就很棒。";
            }
            KanaGroup upcoming = NextGroup(p);
            if (upcoming != null && upcoming.Id == "Ka" && !p.Learned.Contains("ア"))
            {
                return "平假名已经全部熟悉！接下来学片假名：读音完全一样，只是换一种写法。";
            }
            if (today.Seconds / 60 >= p.Goal)
            {
                return "今日目标已完成！想多学可以继续，也可以把精力留给明天。";
            }
            if (errors >= HeavyReview)
            {
                return "有 " + errors + " 个错题在等你，今天先专心复习，不急着学新内容。";
            }
            if (acc >= 0 && acc < 0.7)
            {
                return "最近正确率 " + Math.Round(acc * 100) + "%，建议放慢新内容，多翻卡、多听音。";
            }
            if (streak >= 3)
            {
                return "已经连续学习 " + streak + " 天，保持这个节奏！";
            }
            if (streak == 0 && p.HasEarlierDays())
            {
                return "欢迎回来！先做个热身复习，再继续往前走。";
            }
            return "按下面的建议逐项完成，大约需要 " + p.Goal + " 分钟。";
        }

        /// <summary>推荐顺序中第一个还没全部熟悉的行。</summary>
        public static KanaGroup NextGroup(Progress p)
        {
            return Data.Groups.FirstOrDefault(g => g.Items.Any(x => !p.Learned.Contains(x.K)));
        }

        /// <summary>
        /// 今天「学习新的一行」的目标行。第一次生成计划时取推荐顺序中下一行并记入当日记录，
        /// 之后当天固定不变，所以学完后显示为已完成，而不是直接跳到下一行。
        /// 错题过多且当天还没定目标时返回 null。
        /// </summary>
        public static KanaGroup TodayGroup(Progress p)
        {
            DayLog t = p.Today;
            if (t.Group != null)
            {
                KanaGroup g = Data.Groups.FirstOrDefault(x => x.Id == t.Group);
                if (g != null)
                {
                    return g;
                }
            }
            KanaGroup next = NextGroup(p);
            if (next == null || p.Errors.Count >= HeavyReview)
            {
                return null;
            }
            t.Group = next.Id;
            return next;
        }

        public static List<PlanTask> Build(Progress p)
        {
            DayLog t = p.Today;
            List<Kana> learned = Data.Pool("learned", p);
            int errors = p.Errors.Count;
            KanaGroup target = TodayGroup(p);
            KanaGroup next = NextGroup(p);
            List<PlanTask> tasks = new List<PlanTask>();

            // 1. 先复习：有错题做错题，没有错题且以前学过就翻卡热身
            if (errors > 0)
            {
                tasks.Add(new PlanTask
                {
                    Title = "复习错题 · " + errors + " 个",
                    Detail = "先清旧账：错题在测验中连续答对两次即可移出。",
                    Button = "开始复习",
                    Action = PlanAction.ReviewQuiz
                });
            }
            else if (learned.Count >= 5 && p.HasEarlierDays())
            {
                tasks.Add(new PlanTask
                {
                    Title = "热身：回顾已熟悉的假名",
                    Detail = "今天已翻 " + Math.Min(t.Cards, 10) + " / 10 张卡。",
                    Done = t.Cards >= 10,
                    Button = "翻卡热身",
                    Action = PlanAction.Warmup
                });
            }

            // 2. 学新内容
            if (target != null)
            {
                int known = target.Items.Count(x => p.Learned.Contains(x.K));
                string chars = string.Join(" ", target.Items.Select(x => x.K));
                if (known >= target.Items.Count)
                {
                    tasks.Add(new PlanTask
                    {
                        Title = "学完新的一行：" + Data.GroupLabel(target),
                        Detail = chars + "  ·  已熟悉 " + known + " / " + known + (next != null ? "。还有余力可以继续学「" + Data.GroupLabel(next) + "」。" : "。"),
                        Done = true,
                        DoneButton = next != null ? "学下一行" : "综合测验",
                        Action = next != null ? PlanAction.LearnGroup : PlanAction.AllQuiz,
                        Scope = next != null ? next.Id : null
                    });
                }
                else
                {
                    tasks.Add(new PlanTask
                    {
                        Title = "学习新的一行：" + Data.GroupLabel(target),
                        Detail = chars + "  ·  已熟悉 " + known + " / " + target.Items.Count + "（翻卡记住或测验答对都算）",
                        Button = known > 0 ? "继续学习" : "开始学习",
                        Action = PlanAction.LearnGroup,
                        Scope = target.Id
                    });
                }
            }
            else if (next == null)
            {
                tasks.Add(new PlanTask
                {
                    Title = "平假名和片假名都已熟悉",
                    Detail = "用「平片混合」测验保持手感，下一步可以开始认读简单的词语。",
                    Button = "综合测验",
                    Action = PlanAction.AllQuiz
                });
            }
            else if (errors >= HeavyReview)
            {
                tasks.Add(new PlanTask
                {
                    Title = "今天先不学新的一行",
                    Detail = "错题有 " + errors + " 个，先练熟它们，明天再学「" + Data.GroupLabel(next) + "」效果更好。",
                    Action = PlanAction.None
                });
            }

            // 3. 测验
            string quizScope;
            if (target != null && target.Items.Any(x => p.Learned.Contains(x.K)))
            {
                quizScope = target.Id;
            }
            else if (learned.Count >= 4)
            {
                quizScope = "learned";
            }
            else
            {
                quizScope = target != null ? target.Id : (next != null ? next.Id : "mix");
            }
            tasks.Add(new PlanTask
            {
                Title = "完成一轮 10 题测验",
                Detail = t.Answered > 0
                    ? "今天已答 " + t.Answered + " 题，正确率 " + Math.Round(100.0 * t.Correct / t.Answered) + "%。"
                    : "检查记忆最好的办法，就是试着回答。",
                Done = t.Answered >= 10,
                Button = "去测验",
                Action = PlanAction.Quiz,
                Scope = quizScope
            });

            // 4. 专注时长
            int minutes = (int)(t.Seconds / 60);
            tasks.Add(new PlanTask
            {
                Title = "专注学习 " + p.Goal + " 分钟",
                Detail = "今天已计时 " + minutes + " 分钟" + (minutes < p.Goal ? "，还差 " + (p.Goal - minutes) + " 分钟。" : "。"),
                Done = minutes >= p.Goal,
                Button = "开始计时",
                DoneButton = "再学一段",
                Action = PlanAction.StartTimer
            });
            return tasks;
        }

        /// <summary>按时长目标推荐的下一段计时分钟数（5–60）。</summary>
        public static int SuggestedTimerMinutes(Progress p)
        {
            int left = (int)Math.Ceiling((p.Goal * 60 - p.Today.Seconds) / 60);
            if (left <= 0)
            {
                return p.TimerMinutes;
            }
            return Math.Max(5, Math.Min(60, left));
        }

        public static List<WeekDay> Week(Progress p)
        {
            string[] names = new string[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            DateTime today = p.Clock().Date;
            List<WeekDay> list = new List<WeekDay>();
            for (int i = 6; i >= 0; i--)
            {
                DateTime d = today.AddDays(-i);
                DayLog log;
                p.Days.TryGetValue(Progress.DayKey(d), out log);
                list.Add(new WeekDay
                {
                    Date = d,
                    Label = i == 0 ? "今天" : names[(int)d.DayOfWeek],
                    Minutes = log == null ? 0 : (int)(log.Seconds / 60),
                    Studied = log != null && log.Active,
                    IsToday = i == 0
                });
            }
            return list;
        }
    }
}
