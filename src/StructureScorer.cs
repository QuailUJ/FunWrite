using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;


namespace FunWrite
{
    public sealed class StructureResult
    {
        public bool Supported { get; internal set; }
        public bool Evaluated { get; internal set; }
        public double Multiplier { get; internal set; } = 1;
        public readonly List<string> Messages = new List<string>();
        public string Feedback { get { return string.Join("\r\n", Messages); } }

        public double AdjustScore(double shapeScore)
        {
            if (!Supported) return shapeScore;
            return Evaluated ? shapeScore * Multiplier : Math.Min(.59, shapeScore);
        }

        public void Apply(double[,] mapping, int strokeCount)
        {
            if (!Supported) return;
            for (int i = 1; i <= strokeCount; i++)
                mapping[2, i] = Evaluated ? mapping[2, i] * Multiplier : Math.Min(.59, mapping[2, i]);
        }
    }

    public static class StructureScorer
    {
        // Structure is measured in a shared character frame. Never fit components separately.
        public static StructureResult Check(Stroke template, Stroke handwriting, HanziTemplate data)
        {
            var result = new StructureResult();
            if (data == null || string.IsNullOrEmpty(data.decomposition) || data.matches == null) return result;
            string decomposition = data.decomposition;
            char layout = decomposition[0];
            if (layout != '⿱' && layout != '⿰') return result;
            int offset = 1;
            string first = Node(decomposition, ref offset), second = Node(decomposition, ref offset);
            if (first == null || second == null || offset != decomposition.Length ||
                first.Contains("？") || second.Contains("？")) return result;
            int n = template.StrokeList.Count;
            if (data.matches.Length != n || data.matches.Any(m => m == null || m.Length == 0 || m[0] < 0 || m[0] > 1) ||
                !data.matches.Any(m => m[0] == 0) || !data.matches.Any(m => m[0] == 1)) return result;
            result.Supported = true;
            if (handwriting.StrokeList.Count != n || n > 16)
            {
                result.Messages.Add("筆畫未完整配對，結構無法確認；字形最高 59 分。");
                return result;
            }
            var expected = ShapeScorer.Normalize(template);
            var actual = ShapeScorer.Normalize(handwriting);
            if (expected.Any(line => line.Count == 0) || actual.Any(line => line.Count == 0))
            {
                result.Messages.Add("有空筆畫，結構無法確認；字形最高 59 分。");
                return result;
            }
            var costs = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    costs[i, j] = Math.Min(TrajectoryChecker.Distance(expected[i], actual[j]),
                        TrajectoryChecker.Distance(expected[i], actual[j].AsEnumerable().Reverse().ToList()));
            var assignment = TrajectoryChecker.Assign(costs);
            var e = new RectangleF[2]; var a = new RectangleF[2];
            for (int component = 0; component < 2; component++)
            {
                var indices = Enumerable.Range(0, n).Where(i => data.matches[i][0] == component).ToArray();
                e[component] = Bounds(indices.SelectMany(i => expected[i]));
                a[component] = Bounds(indices.SelectMany(i => actual[assignment[i]]));
            }
            result.Evaluated = true;
            SizeRatio(result, e[0].Width, e[1].Width, a[0].Width, a[1].Width, first, second, "寬度");
            SizeRatio(result, e[0].Height, e[1].Height, a[0].Height, a[1].Height, first, second, "高度");
            bool vertical = layout == '⿱';
            // Y increases upwards in both the tablet and imported median data.
            double expectedGap = vertical ? e[0].Top - e[1].Bottom : e[1].Left - e[0].Right;
            double actualGap = vertical ? a[0].Top - a[1].Bottom : a[1].Left - a[0].Right;
            Difference(result, actualGap - expectedGap, .05, .10,
                first + "與" + second + (actualGap > expectedGap ? "距離過遠" : "距離過近或重疊"));
            double expectedOffset = vertical ? CenterX(e[0]) - CenterX(e[1]) : CenterY(e[0]) - CenterY(e[1]);
            double actualOffset = vertical ? CenterX(a[0]) - CenterX(a[1]) : CenterY(a[0]) - CenterY(a[1]);
            Difference(result, actualOffset - expectedOffset, .06, .10,
                first + "與" + second + (vertical ? "左右對齊需調整" : "上下對齊需調整"));
            return result;
        }

        static string Node(string text, ref int offset)
        {
            if (offset >= text.Length) return null;
            int begin = offset;
            char c = text[offset++];
            if (c >= '\u2ff0' && c <= '\u2ffb')
            {
                int children = c == '⿲' || c == '⿳' ? 3 : 2;
                for (int i = 0; i < children; i++) if (Node(text, ref offset) == null) return null;
            }
            return text.Substring(begin, offset - begin);
        }
        static RectangleF Bounds(IEnumerable<PointF> points)
        {
            var p = points.ToArray();
            return RectangleF.FromLTRB(p.Min(x => x.X), p.Min(x => x.Y), p.Max(x => x.X), p.Max(x => x.Y));
        }
        static double CenterX(RectangleF r) { return (r.Left + r.Right) / 2; }
        static double CenterY(RectangleF r) { return (r.Top + r.Bottom) / 2; }
        static void SizeRatio(StructureResult result, double e0, double e1, double a0, double a1,
            string first, string second, string dimension)
        {
            // A flat component has no meaningful height/width ratio on that axis.
            if (e0 < .03 || e1 < .03) return;
            double ratio = (Math.Max(.001, a0) / Math.Max(.001, a1)) / (e0 / e1);
            Difference(result, Math.Log(ratio), Math.Log(1.25), .50,
                first + "相對" + second + "的" + dimension + (ratio < 1 ? "偏小" : "偏大"));
        }
        static void Difference(StructureResult result, double error, double tolerance, double spread, string message)
        {
            double excess = Math.Max(0, Math.Abs(error) - tolerance);
            double factor = Math.Exp(-excess * excess / (2 * spread * spread));
            // Use the strongest penalty: related size/gap measurements must not double-deduct.
            result.Multiplier = Math.Min(result.Multiplier, factor);
            if (factor < .85) result.Messages.Add(message);
        }
    }
}
