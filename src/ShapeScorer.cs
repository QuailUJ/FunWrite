using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;


namespace FunWrite
{
    public sealed class ShapeResult
    {
        public double[,] Mapping { get; internal set; }
        public readonly List<string> Messages = new List<string>();
        public string Feedback { get { return string.Join("\r\n", Messages); } }
    }

    // Experimental shape score: normalized geometry, independent of stroke segmentation/order.
    public static class ShapeScorer
    {
        public const double FeedbackThreshold = 0.60;
        private const double SampleStep = 0.01;
        private const double Tolerance = 0.12;

        public static double[,] Match(Stroke template, Stroke handwriting)
        {
            return Evaluate(template, handwriting).Mapping;
        }

        public static ShapeResult Evaluate(Stroke template, Stroke handwriting)
        {
            var expected = Normalize(template);
            var actual = Normalize(handwriting);
            var allExpected = expected.SelectMany(x => x).ToList();
            var allActual = actual.SelectMany(x => x).ToList();
            var mapping = new double[3, Math.Max(expected.Count, actual.Count) + 1];
            var result = new ShapeResult { Mapping = mapping };
            if (allExpected.Count == 0 || allActual.Count == 0 || expected.Any(line => line.Count == 0) || actual.Any(line => line.Count == 0)) return result;
            // Only translate after uniform whole-character normalization. Preserve proportions.
            var aligned = BestAlignment(actual, allExpected, allActual);
            var allAligned = aligned.SelectMany(x => x).ToList();
            // Reverse coverage penalizes additional marks as well as missing parts.
            double precision = 0.15 * Similarity(allActual, allExpected) +
                0.85 * Similarity(allAligned, allExpected);
            var pairs = new double[expected.Count, actual.Count];
            var reasons = new string[expected.Count, actual.Count];
            var expectedProgress = expected.Select(ProgressPoints).ToArray();
            var actualProgress = aligned.Select(ProgressPoints).ToArray();
            for (int i = 0; i < expected.Count; i++)
                for (int j = 0; j < actual.Count; j++)
                    pairs[i, j] = Pair(expected[i], aligned[j], expectedProgress[i], actualProgress[j], out reasons[i, j]);
            int[] assignment = null;
            if (expected.Count == actual.Count && expected.Count <= 16)
            {
                var costs = new double[expected.Count, actual.Count];
                for (int i = 0; i < expected.Count; i++)
                    for (int j = 0; j < actual.Count; j++) costs[i, j] = -Math.Log(Math.Max(1e-12, pairs[i, j]));
                assignment = TrajectoryChecker.Assign(costs);
            }
            for (int i = 0; i < expected.Count; i++)
            {
                double coverage = 0.15 * Similarity(expected[i], allActual) +
                    0.85 * Similarity(expected[i], allAligned);
                mapping[2, i + 1] = coverage + precision == 0 ? 0 :
                    2 * coverage * precision / (coverage + precision);
                double best = -1;
                for (int j = 0; j < actual.Count; j++)
                {
                    double pair = pairs[i, j];
                    if (pair > best)
                    {
                        best = pair;
                        mapping[0, i + 1] = j + 1;
                    }
                }
                if (assignment != null)
                {
                    mapping[0, i + 1] = assignment[i] + 1;
                    best = pairs[i, assignment[i]];
                }
                // Pen-count errors are checked independently. For equal counts, compare
                // whole strokes so component size/placement cannot hide in global overlap.
                if (expected.Count == actual.Count)
                    mapping[2, i + 1] = 0.25 * mapping[2, i + 1] +
                        0.75 * Math.Min(mapping[2, i + 1], Math.Max(0, best));
                int hand = (int)mapping[0, i + 1];
                if (hand > 0) mapping[1, hand] = i + 1;
                if (assignment != null && mapping[2, i + 1] < FeedbackThreshold)
                    result.Messages.Add("範例第 " + (i + 1) + " 筆（手寫第 " + hand + " 筆）：" + reasons[i, hand - 1]);
            }
            if (expected.Count != actual.Count)
            {
                for (int i = 1; i <= expected.Count; i++) mapping[2, i] = Math.Min(.59, mapping[2, i]);
                result.Messages.Add("筆數不符（範例 " + expected.Count + " 筆、手寫 " + actual.Count + " 筆），字形最高 59 分。");
            }
            return result;
        }

        private static double Pair(List<PointF> expected, List<PointF> actual,
            PointF[] expectedProgress, PointF[] actualProgress, out string reason)
        {
            double forward = Similarity(expected, actual), backward = Similarity(actual, expected);
            double coverage = forward + backward == 0 ? 0 : 2 * forward * backward / (forward + backward);
            double expectedLength = Length(expected), actualLength = Length(actual);
            double factor = RatioPenalty(expectedLength, actualLength);
            reason = actualLength < expectedLength ? "筆畫過短" : "筆畫過長";
            double direct = ProgressError(expectedProgress, actualProgress, false);
            double reverse = ProgressError(expectedProgress, actualProgress, true);
            bool reversed = reverse < direct;
            double progressPenalty = ErrorPenalty(Math.Min(direct, reverse), .08, .28);
            if (progressPenalty < factor) { factor = progressPenalty; reason = "筆畫路徑或轉折差異較大"; }
            int first = reversed ? actualProgress.Length - 1 : 0, last = reversed ? 0 : actualProgress.Length - 1;
            double endpoint = Math.Sqrt(Math.Max(SquaredDistance(expectedProgress[0], actualProgress[first]),
                SquaredDistance(expectedProgress[expectedProgress.Length - 1], actualProgress[last])));
            double endpointPenalty = ErrorPenalty(endpoint, .10, .23);
            if (endpointPenalty < factor) { factor = endpointPenalty; reason = "起筆或收筆位置偏差較大"; }
            double expectedStraightness = Math.Sqrt(SquaredDistance(expected[0], expected[expected.Count - 1])) / Math.Max(.001, expectedLength);
            double actualStraightness = Math.Sqrt(SquaredDistance(actual[0], actual[actual.Count - 1])) / Math.Max(.001, actualLength);
            double bendPenalty = ErrorPenalty(Math.Abs(actualStraightness - expectedStraightness), .15, .25);
            if (bendPenalty < factor)
            {
                factor = bendPenalty;
                reason = actualStraightness > expectedStraightness ? "轉折或彎曲不足，請對照整筆路徑" : "轉折或彎曲過多";
            }
            double expectedWidth = expected.Max(p => p.X) - expected.Min(p => p.X);
            double actualWidth = actual.Max(p => p.X) - actual.Min(p => p.X);
            double expectedHeight = expected.Max(p => p.Y) - expected.Min(p => p.Y);
            double actualHeight = actual.Max(p => p.Y) - actual.Min(p => p.Y);
            if (expectedWidth > .08)
            {
                // Absolute character-relative tolerance avoids amplifying small slope noise.
                double widthPenalty = ErrorPenalty(Math.Abs(expectedWidth - actualWidth), .08, .18);
                if (expectedWidth > .15 && actualWidth < expectedWidth * .4)
                    widthPenalty = Math.Min(widthPenalty, CollapsePenalty(expectedWidth, actualWidth));
                if (widthPenalty < factor) { factor = widthPenalty; reason = actualWidth < expectedWidth ? "橫向展開不足，請檢查轉折與收筆" : "橫向展開過大"; }
            }
            if (expectedHeight > .08)
            {
                double heightPenalty = ErrorPenalty(Math.Abs(expectedHeight - actualHeight), .08, .18);
                if (expectedHeight > .15 && actualHeight < expectedHeight * .4)
                    heightPenalty = Math.Min(heightPenalty, CollapsePenalty(expectedHeight, actualHeight));
                if (heightPenalty < factor) { factor = heightPenalty; reason = actualHeight < expectedHeight ? "縱向展開不足，筆畫縮在局部" : "縱向展開過大"; }
            }
            if (coverage < factor) reason = "筆畫位置或形狀需調整";
            return coverage * factor;
        }

        private static double CollapsePenalty(double expected, double actual)
        {
            // Ramp into the severe penalty; crossing 40% must not abruptly change the score.
            double blend = Math.Max(0, Math.Min(1, (.4 - actual / expected) / .2));
            return 1 - blend * (1 - RatioPenalty(expected, actual));
        }

        private static double RatioPenalty(double expected, double actual)
        {
            return ErrorPenalty(Math.Abs(Math.Log(Math.Max(.001, actual) / Math.Max(.001, expected))), Math.Log(1.25), .45);
        }
        private static double ErrorPenalty(double error, double tolerance, double spread)
        {
            double excess = Math.Max(0, error - tolerance);
            return Math.Exp(-excess * excess / (2 * spread * spread));
        }
        private static double SquaredDistance(PointF a, PointF b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
        private static double ProgressError(PointF[] expected, PointF[] actual, bool reverse)
        {
            double total = 0;
            for (int i = 0; i < expected.Length; i++)
                total += SquaredDistance(expected[i], actual[reverse ? actual.Length - 1 - i : i]);
            return Math.Sqrt(total / expected.Length);
        }
        private static PointF[] ProgressPoints(List<PointF> line)
        {
            var result = new PointF[32];
            double length = Length(line), covered = 0;
            int segment = 1;
            for (int i = 0; i < result.Length; i++)
            {
                double target = length * i / (result.Length - 1);
                while (segment < line.Count - 1 && covered + Math.Sqrt(SquaredDistance(line[segment - 1], line[segment])) < target)
                {
                    covered += Math.Sqrt(SquaredDistance(line[segment - 1], line[segment])); segment++;
                }
                if (line.Count == 1) { result[i] = line[0]; continue; }
                double distance = Math.Sqrt(SquaredDistance(line[segment - 1], line[segment]));
                double ratio = distance == 0 ? 0 : Math.Max(0, Math.Min(1, (target - covered) / distance));
                result[i] = new PointF((float)(line[segment - 1].X + (line[segment].X - line[segment - 1].X) * ratio),
                    (float)(line[segment - 1].Y + (line[segment].Y - line[segment - 1].Y) * ratio));
            }
            return result;
        }

        internal static double Length(List<PointF> points)
        {
            double length = 0;
            for (int i = 1; i < points.Count; i++)
            {
                double dx = points[i].X - points[i - 1].X, dy = points[i].Y - points[i - 1].Y;
                length += Math.Sqrt(dx * dx + dy * dy);
            }
            return length;
        }

        internal static List<List<PointF>> BestAlignment(List<List<PointF>> strokes,
            List<PointF> expected, List<PointF> actual)
        {
            var best = strokes;
            double bestScore = (Similarity(actual, expected) + Similarity(expected, actual)) / 2;
            // Bounding-box centers are sensitive to hooks/endpoints. Fit one translation for
            // the whole character (at most 12%); never move individual strokes independently.
            for (int x = -3; x <= 3; x++)
            for (int y = -3; y <= 3; y++)
            {
                var shifted = strokes.Select(line => line.Select(p =>
                    new PointF(p.X + x * 0.04f, p.Y + y * 0.04f)).ToList()).ToList();
                var points = shifted.SelectMany(line => line).ToList();
                double score = (Similarity(points, expected) + Similarity(expected, points)) / 2;
                if (score > bestScore) { bestScore = score; best = shifted; }
            }
            return best;
        }

        public static double Overall(double[,] mapping, Stroke template)
        {
            var strokes = Normalize(template);
            double total = 0, weight = 0;
            for (int i = 0; i < strokes.Count; i++)
            {
                // Arc-length sampling makes long strokes contribute proportionally.
                double w = strokes[i].Count;
                total += mapping[2, i + 1] * w;
                weight += w;
            }
            if (weight == 0) return 0;
            // A badly formed short stroke must not be hidden by long correct outlines.
            double weakest = Enumerable.Range(1, strokes.Count).Min(i => mapping[2, i]);
            return Math.Min(total / weight, weakest + .25);
        }

        private static double Similarity(List<PointF> from, List<PointF> to)
        {
            if (from.Count == 0 || to.Count == 0) return 0;
            double total = 0;
            foreach (var p in from)
            {
                double nearest = double.MaxValue;
                foreach (var q in to)
                {
                    double dx = p.X - q.X, dy = p.Y - q.Y;
                    nearest = Math.Min(nearest, dx * dx + dy * dy);
                }
                total += Math.Exp(-nearest / (2 * Tolerance * Tolerance));
            }
            return total / from.Count;
        }

        internal static List<List<PointF>> Normalize(Stroke stroke)
        {
            var points = stroke.StrokeList.SelectMany(x => x).ToList();
            var result = new List<List<PointF>>();
            if (points.Count == 0) return result;
            double left = points.Min(p => p.X), right = points.Max(p => p.X);
            double top = points.Min(p => p.Y), bottom = points.Max(p => p.Y);
            double size = Math.Max(1, Math.Max(right - left, bottom - top));
            foreach (var line in stroke.StrokeList)
            {
                var sampled = new List<PointF>();
                double carry = 0;
                for (int i = 0; i < line.Count; i++)
                {
                    var p = new PointF((float)((line[i].X - (left + right) / 2) / size),
                        (float)((line[i].Y - (top + bottom) / 2) / size));
                    if (i == 0) { sampled.Add(p); continue; }
                    var prev = new PointF((float)((line[i - 1].X - (left + right) / 2) / size),
                        (float)((line[i - 1].Y - (top + bottom) / 2) / size));
                    double dx = p.X - prev.X, dy = p.Y - prev.Y;
                    double length = Math.Sqrt(dx * dx + dy * dy);
                    if (length == 0) continue;
                    double next = SampleStep - carry;
                    for (; next <= length; next += SampleStep)
                        sampled.Add(new PointF((float)(prev.X + dx * next / length),
                            (float)(prev.Y + dy * next / length)));
                    carry = length - (next - SampleStep);
                }
                if (line.Count > 0)
                {
                    var end = line[line.Count - 1];
                    var endpoint = new PointF((float)((end.X - (left + right) / 2) / size),
                        (float)((end.Y - (top + bottom) / 2) / size));
                    if (sampled[sampled.Count - 1] != endpoint) sampled.Add(endpoint);
                }
                result.Add(sampled);
            }
            return result;
        }
    }
}
