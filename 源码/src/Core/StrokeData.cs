using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace KanaSmallSteps
{
    /// <summary>一笔：已展开成折线的点（KanjiVG 的 109×109 坐标系，拗音的小写字已缩放平移）。</summary>
    public sealed class Stroke
    {
        public PointF[] Points;

        public float Length;

        /// <summary>沿笔画走到 t（0–1）处为止的折线，用于逐笔书写动画。</summary>
        public PointF[] Partial(float t)
        {
            if (t >= 1 || Points.Length < 2)
            {
                return Points;
            }
            float target = Math.Max(0, t) * Length;
            List<PointF> list = new List<PointF> { Points[0] };
            float walked = 0;
            for (int i = 1; i < Points.Length; i++)
            {
                PointF a = Points[i - 1], b = Points[i];
                float seg = Distance(a, b);
                if (walked + seg >= target)
                {
                    float f = seg > 0 ? (target - walked) / seg : 0;
                    list.Add(new PointF(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f));
                    break;
                }
                list.Add(b);
                walked += seg;
            }
            if (list.Count == 1)
            {
                list.Add(list[0]);
            }
            return list.ToArray();
        }

        internal static float Distance(PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>
    /// 平假名笔顺数据。来自 KanjiVG（https://kanjivg.tagaini.net，CC BY-SA 3.0），
    /// 以内嵌资源 Strokes.kana-strokes.json 打包。笔画路径只用到 M（绝对移动）和 c（相对三次贝塞尔）两种命令。
    /// </summary>
    public static class StrokeData
    {
        public const float Size = 109f;

        /// <summary>拗音画布宽度：小写ゃゅょ缩到 72%，贴在第一个字右侧、底部对齐（与安卓版相同）。</summary>
        public const float ComboWidth = 150f;

        private const float SmallScale = 0.72f, SmallX = 81f, SmallY = 27f;

        private static Dictionary<string, List<string>> raw;

        private static readonly Dictionary<string, List<Stroke>> Cache = new Dictionary<string, List<Stroke>>();

        private static Dictionary<string, List<string>> Raw
        {
            get
            {
                if (raw == null)
                {
                    raw = Load();
                }
                return raw;
            }
        }

        private static Dictionary<string, List<string>> Load()
        {
            Dictionary<string, List<string>> map = new Dictionary<string, List<string>>();
            try
            {
                string json;
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Strokes.kana-strokes.json"))
                {
                    if (s == null)
                    {
                        return map;
                    }
                    using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                    {
                        json = r.ReadToEnd();
                    }
                }
                int start = json.IndexOf("\"strokes\"", StringComparison.Ordinal);
                if (start < 0)
                {
                    return map;
                }
                foreach (Match m in Regex.Matches(json.Substring(start), "\"(.)\":\\[([^\\]]*)\\]"))
                {
                    List<string> paths = new List<string>();
                    foreach (Match p in Regex.Matches(m.Groups[2].Value, "\"([^\"]+)\""))
                    {
                        paths.Add(p.Groups[1].Value);
                    }
                    map[m.Groups[1].Value] = paths;
                }
            }
            catch
            {
                // 数据缺失时界面隐藏笔顺面板
            }
            return map;
        }

        public static bool Has(string kana)
        {
            foreach (char c in kana)
            {
                if (!Raw.ContainsKey(c.ToString()))
                {
                    return false;
                }
            }
            return kana.Length > 0;
        }

        public static float Width(string kana)
        {
            return kana.Length > 1 ? ComboWidth : Size;
        }

        /// <summary>按书写顺序排列的笔画（拗音先写大字，再写小写字）。</summary>
        public static List<Stroke> Get(string kana)
        {
            List<Stroke> list;
            if (Cache.TryGetValue(kana, out list))
            {
                return list;
            }
            list = new List<Stroke>();
            for (int i = 0; i < kana.Length; i++)
            {
                List<string> paths;
                if (!Raw.TryGetValue(kana[i].ToString(), out paths))
                {
                    continue;
                }
                float k = i == 0 ? 1f : SmallScale, dx = i == 0 ? 0f : SmallX, dy = i == 0 ? 0f : SmallY;
                foreach (string d in paths)
                {
                    PointF[] pts = Flatten(d);
                    for (int j = 0; j < pts.Length; j++)
                    {
                        pts[j] = new PointF(pts[j].X * k + dx, pts[j].Y * k + dy);
                    }
                    Stroke s = new Stroke { Points = pts };
                    for (int j = 1; j < pts.Length; j++)
                    {
                        s.Length += Stroke.Distance(pts[j - 1], pts[j]);
                    }
                    list.Add(s);
                }
            }
            Cache[kana] = list;
            return list;
        }

        /// <summary>把 SVG 路径（M / c）展开成折线，每段贝塞尔取 12 个点。</summary>
        public static PointF[] Flatten(string d)
        {
            List<PointF> pts = new List<PointF>();
            MatchCollection tokens = Regex.Matches(d, @"[Mc]|-?\d*\.?\d+(?:[eE][-+]?\d+)?");
            char cmd = ' ';
            float cx = 0, cy = 0;
            List<float> nums = new List<float>();
            Action flush = null;
            flush = delegate
            {
                if (cmd == 'M')
                {
                    for (int i = 0; i + 1 < nums.Count; i += 2)
                    {
                        cx = nums[i];
                        cy = nums[i + 1];
                        pts.Add(new PointF(cx, cy));
                    }
                }
                else if (cmd == 'c')
                {
                    for (int i = 0; i + 5 < nums.Count; i += 6)
                    {
                        PointF p0 = new PointF(cx, cy);
                        PointF p1 = new PointF(cx + nums[i], cy + nums[i + 1]);
                        PointF p2 = new PointF(cx + nums[i + 2], cy + nums[i + 3]);
                        PointF p3 = new PointF(cx + nums[i + 4], cy + nums[i + 5]);
                        for (int n = 1; n <= 12; n++)
                        {
                            float t = n / 12f, u = 1 - t;
                            pts.Add(new PointF(
                                u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X,
                                u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y));
                        }
                        cx = p3.X;
                        cy = p3.Y;
                    }
                }
                nums.Clear();
            };
            foreach (Match t in tokens)
            {
                string v = t.Value;
                if (v == "M" || v == "c")
                {
                    flush();
                    cmd = v[0];
                }
                else
                {
                    nums.Add(float.Parse(v, CultureInfo.InvariantCulture));
                }
            }
            flush();
            return pts.ToArray();
        }
    }
}
