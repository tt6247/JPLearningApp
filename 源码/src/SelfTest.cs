using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace KanaSmallSteps
{
    public static class SelfTest
    {
        private static void Check(bool pass, string name)
        {
            if (!pass)
            {
                throw new Exception(name);
            }
        }

        private static string DailyTests(string folder)
        {
            DateTime now = new DateTime(2026, 10, 19, 9, 0, 0);
            string path = Path.Combine(folder, "daily-progress.xml");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            Progress p = new Progress(path);
            p.Clock = delegate { return now; };
            Check(p.Streak() == 0 && !p.HasEarlierDays(), "empty streak");
            List<PlanTask> fresh = DailyPlan.Build(p);
            Check(fresh.Any(t => t.Action == PlanAction.LearnGroup && t.Scope == "a"), "first plan starts with vowels");
            Check(!fresh.Any(t => t.Action == PlanAction.ReviewQuiz || t.Action == PlanAction.Warmup), "no review on first day");
            Check(DailyPlan.Coach(p).Contains("第一次"), "first-time coach");

            // 前三天学习，今天尚未学习 → 连续 3 天
            for (int i = 1; i <= 3; i++)
            {
                p.Day(now.AddDays(-i)).Seconds = 600;
            }
            Check(p.Streak() == 3 && p.HasEarlierDays(), "streak counts from yesterday");
            p.AddStudySeconds(45);
            Check(p.Streak() == 4, "streak includes today");
            p.Day(now.AddDays(-2)).Seconds = 0;
            Check(p.Streak() == 2, "gap breaks streak");

            foreach (Kana k in Data.Groups[0].Items)
            {
                p.CardKnown(k);
            }
            Check(p.Today.Learned == 5 && p.Today.Cards == 5, "card counters");
            p.CardKnown(Data.Groups[0].Items[0]);
            Check(p.Today.Learned == 5 && p.Today.Cards == 6, "relearn not double counted");
            Check(DailyPlan.NextGroup(p).Id == "k", "next group after vowels");
            p.Record(Data.ByKana["か"], false);
            p.Record(Data.ByKana["あ"], true);
            Check(p.Today.Answered == 2 && p.Today.Correct == 1, "daily quiz counters");
            Check(DailyPlan.Build(p)[0].Action == PlanAction.ReviewQuiz, "errors reviewed first");

            foreach (Kana k in Data.All.Take(DailyPlan.HeavyReview))
            {
                p.MarkAgain(k);
            }
            Check(DailyPlan.Build(p).Any(t => t.Action == PlanAction.LearnGroup), "today's row kept after errors pile up");
            p.Today.Group = null; // 当天尚未确定目标行时，错题过多会暂停学新行
            Check(DailyPlan.Build(p).Any(t => t.Action == PlanAction.None) && p.Today.Group == null, "heavy review pauses new rows");

            p.Goal = 20;
            p.TimerMinutes = 25;
            Check(p.Save(), "daily save");
            Progress q = Progress.Load(path);
            q.Clock = delegate { return now; };
            Check(q.Goal == 20 && q.TimerMinutes == 25, "goal reload");
            Check(q.Theme == "system", "theme default");
            q.Theme = "dark";
            Check(q.Save() && Progress.Load(path).Theme == "dark", "theme reload");
            Check(Math.Abs(q.Today.Seconds - 45) < 1 && q.Today.Learned == 5 && q.Today.Answered == 2 && q.Today.Correct == 1, "day log reload");
            Check(q.Streak() == 2, "streak reload");
            Check(DailyPlan.Week(q).Count == 7 && DailyPlan.Week(q)[6].IsToday && DailyPlan.Week(q)[5].Minutes == 10, "week chart data");
            Check(DailyPlan.SuggestedTimerMinutes(q) == 20, "suggested timer");

            // 旧版（v2）进度文件仍可读取
            string old = Path.Combine(folder, "v2-progress.xml");
            File.WriteAllText(old, "<kanaProgress version=\"2\" answered=\"3\" correct=\"2\"><kana value=\"あ\" learned=\"true\" review=\"0\" /></kanaProgress>");
            Progress v2 = Progress.Load(old);
            Check(v2.Warning.Length == 0 && v2.Learned.Contains("あ") && v2.Answered == 3 && v2.Goal == 15 && v2.Days.Count == 0, "v2 file compatible");
            File.WriteAllText(old, "<kanaProgress version=\"3\" theme=\"neon\" />");
            Check(Progress.Load(old).Theme == "system", "theme sanitize");
            return "PASS: daily log; streak; plan ordering; heavy-review pause; goal/timer/theme persistence; week data; v2 file compatibility.";
        }

        /// <summary>2.1.1：只靠测验学完一行时，「学习新的一行」应显示为完成，且当天目标行不跳动。</summary>
        private static string PlanCompletionTests(string folder)
        {
            DateTime now = new DateTime(2026, 10, 19, 14, 0, 0);
            string path = Path.Combine(folder, "plan-progress.xml");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            Progress p = new Progress(path);
            p.Clock = delegate { return now; };
            Func<PlanTask> learnTask = delegate { return DailyPlan.Build(p).First(t => t.Title.Contains("一行")); };
            PlanTask before = learnTask();
            Check(!before.Done && before.Scope == "a" && p.Today.Group == "a", "today group assigned");
            for (int round = 0; round < 3; round++)
            {
                foreach (Kana k in Data.Groups[0].Items)
                {
                    p.Record(k, true);
                }
            }
            Check(p.Today.Answered == 15 && p.Today.Learned == 5, "quiz-only learning counted");
            PlanTask after = learnTask();
            Check(after.Done && after.Title.Contains("元音") && after.Scope == "k", "row done via quiz, stays on today's row");
            Check(p.Save(), "group save");
            Progress q = Progress.Load(path);
            q.Clock = delegate { return now; };
            Check(q.Today.Group == "a" && DailyPlan.Build(q).First(t => t.Title.Contains("一行")).Done, "group reload");

            // 答错会取消熟悉；答对一次不够（还有 1 次待复习），连对两次才重新算熟悉
            Kana a = Data.ByKana["あ"];
            p.Record(a, false);
            p.Record(a, true);
            Check(!p.Learned.Contains("あ") && !learnTask().Done, "pending review not learned");
            p.Record(a, true);
            Check(p.Learned.Contains("あ") && learnTask().Done, "review cleared relearns");

            // 详情窗口切换熟悉标记
            Kana ka = Data.ByKana["か"];
            int learned = p.Today.Learned;
            p.ToggleLearned(ka);
            Check(p.Learned.Contains("か") && p.Today.Learned == learned + 1, "toggle on");
            p.ToggleLearned(ka);
            Check(!p.Learned.Contains("か") && p.Today.Learned == learned, "toggle off");

            // 第二天换到下一行
            DateTime tomorrow = now.AddDays(1);
            p.Clock = delegate { return tomorrow; };
            PlanTask next = learnTask();
            Check(!next.Done && next.Scope == "k" && p.Today.Group == "k", "next day moves on");
            return "PASS: row completion via quiz; fixed daily row; review gating; detail toggle; next-day advance.";
        }

                private static string TimerTests()
        {
            DateTime t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            bool finished;
            StudyTimer timer = new StudyTimer(1);
            Check(timer.Total == 60 && !timer.Started && StudyTimer.Format(timer.Remaining) == "01:00", "timer initial");
            timer.Start(t0);
            double credit = timer.Tick(t0.AddSeconds(10), out finished);
            Check(Math.Abs(credit - 10) < 0.01 && !finished && StudyTimer.Format(timer.Remaining) == "00:50", "timer tick");
            Check(!timer.Set(5), "no preset while running");
            credit = timer.Pause(t0.AddSeconds(20));
            Check(Math.Abs(credit - 10) < 0.01 && !timer.Running && timer.Started, "timer pause");
            credit = timer.Tick(t0.AddSeconds(500), out finished);
            Check(credit == 0 && Math.Abs(timer.Remaining - 40) < 0.01, "paused timer frozen");
            timer.Start(t0.AddSeconds(500));
            timer.AddSeconds(300);
            Check(timer.Total == 360 && Math.Abs(timer.Remaining - 340) < 0.01, "add five minutes");
            credit = timer.Tick(t0.AddSeconds(500 + 3600), out finished);
            Check(finished && credit <= StudyTimer.MaxCreditPerTick && timer.Finished && timer.Remaining == 0, "sleep clamp and finish");
            timer.Start(t0.AddSeconds(5000));
            Check(timer.Running && Math.Abs(timer.Remaining - 360) < 0.01, "restart after finish");
            timer.Reset();
            Check(!timer.Running && !timer.Started, "timer reset");
            Check(timer.Set(120) && timer.Total == StudyTimer.MaxSeconds, "timer clamp");
            return "PASS: timer tick; pause; add time; sleep clamp; finish; restart; reset; limits.";
        }

        public static int Run(string folder)
        {
            Directory.CreateDirectory(folder);
            List<string> list = new List<string>();
            try
            {
                Progress progress = new Progress(Path.Combine(folder, "test-progress.xml"));
                Check(Data.All.Count == 104 && Data.ByKana.Count == 104, "unique kana");
                Check(Data.Pool("basic", progress).Count == 46 && Data.Pool("voiced", progress).Count == 25 && Data.Pool("combo", progress).Count == 33, "groups");
                Check(Data.ByKana["を"].Column == 4 && Data.ByKana["や"].Column == 0 && Data.ByKana["ゆ"].Column == 2 && Data.ByKana["よ"].Column == 4, "chart spacing");
                list.Add("PASS: 104 kana; 46/25/33 group counts; sparse chart positions.");
                for (int i = 0; i < 300; i++)
                {
                    Quiz quiz = new Quiz((i % 2 == 0) ? "all" : "nn", progress);
                    Check(quiz.Items.Select((Kana x) => x.K).Distinct().Count() == quiz.Items.Count, "duplicate questions");
                    while (!quiz.Done)
                    {
                        Check(quiz.Options.Count == 4 && quiz.Options.Distinct().Count() == 4 && quiz.Options.Contains(quiz.Current.R), "choices");
                        Check(quiz.Answer(quiz.Current.R), "answer");
                        int answered = progress.Answered;
                        Check(!quiz.Answer(quiz.Current.R) && progress.Answered == answered, "duplicate submit");
                        quiz.Next();
                    }
                    Check(quiz.Score == quiz.Items.Count, "score");
                }
                list.Add("PASS: 300 randomized quizzes; distinct answers; score; repeat-submit protection.");
                progress = new Progress(progress.Path);
                Kana kana = Data.ByKana["ん"];
                progress.Learned.Add(kana.K);
                progress.Record(kana, false);
                Check(progress.Errors[kana.K] == 2 && !progress.Learned.Contains(kana.K), "wrong adds review");
                progress.Record(kana, true);
                Check(progress.Errors[kana.K] == 1, "first review");
                progress.Record(kana, false);
                Check(progress.Errors[kana.K] == 2, "review resets");
                progress.Record(kana, true);
                progress.Record(kana, true);
                Check(!progress.Errors.ContainsKey(kana.K), "review clears");
                progress.Learned.Add("あ");
                progress.MarkAgain(Data.ByKana["きゃ"]);
                Check(progress.Save(), "save");
                Progress progress2 = Progress.Load(progress.Path);
                Check(progress2.Learned.Contains("あ") && progress2.Errors["きゃ"] == 2 && progress2.Answered == progress.Answered && progress2.Correct == progress.Correct, "reload");
                Check(progress2.Save(), "atomic replacement");
                Check(File.Exists(progress.Path + ".bak"), "backup");
                File.WriteAllText(progress.Path, "corrupted");
                Progress progress3 = Progress.Load(progress.Path);
                Check(progress3.Learned.Contains("あ") && progress3.Warning.Length > 0, "recovery");
                string text = Path.Combine(folder, "not-a-directory");
                File.WriteAllText(text, "test");
                Progress progress4 = new Progress(Path.Combine(text, "progress.xml"));
                Check(!progress4.Save() && progress4.Warning.Length > 0, "write failure");
                Check(new Quiz("errors", new Progress(progress.Path)).Items.Count == 0, "empty review");
                list.Add("PASS: wrong-answer review; two correct answers; reset on mistake; XML persistence; atomic save; backup recovery; write failure; empty review.");
                list.Add(DailyTests(folder));
                list.Add(TimerTests());
                list.Add(PlanCompletionTests(folder));
                Check(Data.All.Select(OfflineAudio.Key).Distinct().Count() == 104, "audio mapping");
                bool windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                using (OfflineAudio offlineAudio = new OfflineAudio(Path.Combine(folder, "audio")))
                {
                    foreach (Kana item in Data.All)
                    {
                        Check(OfflineAudio.Bytes(item).Length > 1000, "embedded audio " + item.K);
                        if (windows)
                        {
                            int num = offlineAudio.Open(item);
                            Check(num >= 200 && num < 4000, "audio decode " + item.K);
                        }
                    }
                }
                list.Add(windows
                    ? "PASS: all 104 embedded recordings map correctly and decode through the Windows audio API."
                    : "PASS: all 104 embedded recordings present (decode check skipped: not Windows).");
                using (MainForm mainForm = new MainForm(new Progress(progress.Path)))
                {
                    string[] array = new string[7] { "today", "table", "groups", "cards", "quiz", "errors", "help" };
                    string[] modes = new string[] { "light", "dark" };
                    foreach (string mode in modes)
                    {
                        mainForm.UseTheme(mode);
                        Check(Theme.IsDark == (mode == "dark") && mainForm.BackColor == Theme.Paper, "theme switch " + mode);
                        foreach (string text2 in array)
                        {
                            mainForm.Navigate(text2);
                            Check(mainForm.Controls.Count > 0, "native UI " + text2 + " (" + mode + ")");
                        }
                    }
                    mainForm.UseTheme("light");
                }
                list.Add("PASS: all seven native Windows pages construct in light and dark eye-care themes.");
                File.WriteAllLines(Path.Combine(folder, "result.txt"), list);
                return 0;
            }
            catch (Exception ex)
            {
                list.Add("FAIL: " + ex);
                File.WriteAllLines(Path.Combine(folder, "result.txt"), list);
                return 1;
            }
        }
    }
}
