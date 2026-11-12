using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;


namespace FunWrite
{
    public sealed class TrajectoryResult
    {
        public readonly List<string> Errors = new List<string>();
        public bool Passed { get { return Errors.Count == 0; } }
        public string Feedback { get { return string.Join("\r\n", Errors); } }
    }

    // Uses untouched contact samples: never join a recorded pen-up automatically.
    public static class TrajectoryChecker
    {
        public static Sample[] Copy(IEnumerable<Sample> samples)
        {
            return samples.Select(p => new Sample(p.StrokeNumber, p.Time, p.X, p.Y, p.Pressure)).ToArray();
        }

        public static Sample[] Parse(string text)
        {
            return text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line)).Select(line =>
                {
                    var p = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length != 5) throw new FormatException("筆跡資料每行須包含筆序、時間、X、Y、壓力。");
                    return new Sample(int.Parse(p[0], CultureInfo.InvariantCulture), long.Parse(p[1], CultureInfo.InvariantCulture),
                        float.Parse(p[2], CultureInfo.InvariantCulture), float.Parse(p[3], CultureInfo.InvariantCulture), ushort.Parse(p[4], CultureInfo.InvariantCulture));
                }).ToArray();
        }

        public static TrajectoryResult Check(Stroke template, IEnumerable<Sample> samples)
        {
            var result = new TrajectoryResult();
            var hand = ContactStroke(samples);
            var expected = ShapeScorer.Normalize(template);
            var actual = ShapeScorer.Normalize(hand);
            if (actual.Count == 0 || expected.Count == 0)
            {
                result.Errors.Add("沒有完整筆跡，請重新書寫。");
                return result;
            }
            actual = ShapeScorer.BestAlignment(actual, expected.SelectMany(x => x).ToList(), actual.SelectMany(x => x).ToList());
            int n = expected.Count, m = actual.Count;
            if (n != m)
            {
                result.Errors.Add("應寫 " + n + " 筆，實際落筆 " + m + " 次。");
                if (m > n)
                {
                    for (int j = 0; j + 1 < m; j++)
                    {
                        var joined = actual[j].Concat(actual[j + 1]).ToList();
                        int best = Enumerable.Range(0, n).OrderBy(i => Distance(expected[i], joined)).First();
                        if (Distance(expected[best], joined) < 0.12 &&
                            Distance(expected[best], joined) + 0.03 < Math.Min(Distance(expected[best], actual[j]), Distance(expected[best], actual[j + 1])))
                            result.Errors.Add("範例第 " + (best + 1) + " 筆疑似中途提筆\r\n（手寫第 " + (j + 1) + "、" + (j + 2) + " 筆交界）。");
                    }
                    if (result.Errors.Count == 1) result.Errors.Add("有多筆或斷筆，請對照示範重寫。");
                }
                else result.Errors.Add("可能漏筆或連筆，請逐筆對照示範。");
                return result;
            }
            // One-to-one assignment prevents a repeated stroke covering a missing stroke.
            var costs = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) costs[i, j] = Distance(expected[i], actual[j]);
            if (n > 16) { result.Errors.Add("此字超出目前筆順檢查範圍。"); return result; }
            var assignment = Assign(costs);
            for (int i = 0; i < n; i++)
            {
                int j = assignment[i];
                if (j != i)
                    result.Errors.Add("筆順：範例第 " + (i + 1) + " 筆寫在第 " + (j + 1) + " 筆。");
                var reversed = actual[i].AsEnumerable().Reverse().ToList();
                double directionMargin = Math.Max(.00001, Math.Min(.04, ShapeScorer.Length(expected[i]) * .15));
                if (Distance(expected[i], reversed) + directionMargin < costs[i, i])
                    result.Errors.Add("第 " + (i + 1) + " 筆方向不符，請依示範起筆。");
                else if (costs[i, i] > 0.16 || EndDistance(expected[i], actual[i]) > 0.30)
                    result.Errors.Add("第 " + (i + 1) + " 筆軌跡或起收筆需修正。");
            }
            return result;
        }

        public static Stroke ContactStroke(IEnumerable<Sample> samples)
        {
            var hand = new Stroke();
            List<Sample> current = null;
            foreach (var p in samples)
            {
                if (p.Pressure == 0) { current = null; continue; }
                if (current == null) { current = new List<Sample>(); hand.StrokeList.Add(current); }
                if (float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsInfinity(p.X) || float.IsInfinity(p.Y))
                    throw new ArgumentException("筆跡包含無效座標。");
                // Canonical contact IDs keep legacy display helpers from splitting the
                // first point of a stroke, whose original ID was advanced one sample late.
                current.Add(new Sample((hand.StrokeList.Count - 1) * 2, p.Time, p.X, p.Y, p.Pressure));
            }
            var points = hand.StrokeList.SelectMany(line => line).ToArray();
            if (points.Length == 0) return hand;
            hand.Left = points.Min(p => p.X); hand.Right = points.Max(p => p.X);
            hand.Top = points.Min(p => p.Y); hand.Bottom = points.Max(p => p.Y);
            hand.CenterX = (hand.Left + hand.Right) / 2; hand.CenterY = (hand.Top + hand.Bottom) / 2;
            hand.Width = Math.Max(1, (int)Math.Ceiling(hand.Right - hand.Left) + 1);
            hand.Height = Math.Max(1, (int)Math.Ceiling(hand.Bottom - hand.Top) + 1);
            return hand;
        }

        internal static int[] Assign(double[,] costs)
        {
            int n = costs.GetLength(0);
            int count = 1 << n;
            var dp = Enumerable.Repeat(double.PositiveInfinity, count).ToArray();
            var choice = new int[count];
            dp[0] = 0;
            for (int mask = 0; mask < count; mask++)
            {
                int row = 0;
                for (int bits = mask; bits != 0; bits &= bits - 1) row++;
                for (int j = 0; j < n && row < n; j++)
                    if ((mask & (1 << j)) == 0)
                    {
                        int next = mask | (1 << j);
                        if (dp[mask] + costs[row, j] < dp[next]) { dp[next] = dp[mask] + costs[row, j]; choice[next] = j; }
                    }
            }
            var assignment = new int[n];
            int remaining = count - 1;
            for (int i = n - 1; i >= 0; i--) { assignment[i] = choice[remaining]; remaining ^= 1 << assignment[i]; }
            return assignment;
        }

        static double PointDistance(PointF a, PointF b)
        {
            double x = a.X - b.X, y = a.Y - b.Y;
            return Math.Sqrt(x * x + y * y);
        }
        static double EndDistance(List<PointF> a, List<PointF> b)
        {
            return Math.Max(PointDistance(a[0], b[0]), PointDistance(a[a.Count - 1], b[b.Count - 1]));
        }
        // Monotonic trajectory matching allows speed/sampling variation, not reversal.
        internal static double Distance(List<PointF> a, List<PointF> b)
        {
            var dp = new double[a.Count + 1, b.Count + 1];
            for (int i = 0; i <= a.Count; i++) dp[i, 0] = double.PositiveInfinity;
            for (int j = 0; j <= b.Count; j++) dp[0, j] = double.PositiveInfinity;
            dp[0, 0] = 0;
            for (int i = 1; i <= a.Count; i++)
                for (int j = 1; j <= b.Count; j++)
                    dp[i, j] = PointDistance(a[i - 1], b[j - 1]) + Math.Min(dp[i - 1, j - 1], Math.Min(dp[i - 1, j], dp[i, j - 1]));
            return dp[a.Count, b.Count] / Math.Max(a.Count, b.Count);
        }
    }
}
