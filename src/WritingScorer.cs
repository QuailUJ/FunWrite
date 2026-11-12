using System;
using System.Collections.Generic;
using System.Linq;


namespace FunWrite
{
    public sealed class WritingEvaluation
    {
        public Stroke Handwriting { get; internal set; }
        public ShapeResult Shape { get; internal set; }
        public TrajectoryResult Trajectory { get; internal set; }
        public StructureResult Structure { get; internal set; }
        public double Score { get; internal set; }
        public bool Passed { get { return Trajectory.Passed && Score >= 60; } }
        public double Speed { get; internal set; }
        public int MeanPressure { get; internal set; }
        public string Feedback { get; internal set; }
    }

    public static class WritingScorer
    {
        // Live input and replay must use the same untouched contact runs and pass decision.
        public static WritingEvaluation Evaluate(Stroke template, IEnumerable<Sample> samples, HanziTemplate data)
        {
            var raw = TrajectoryChecker.Copy(samples);
            var hand = TrajectoryChecker.ContactStroke(raw);
            var shape = ShapeScorer.Evaluate(template, hand);
            double shapeScore = ShapeScorer.Overall(shape.Mapping, template);
            var structure = StructureScorer.Check(template, hand, data);
            var trajectory = TrajectoryChecker.Check(template, raw);
            var evaluation = new WritingEvaluation {
                Handwriting = hand, Shape = shape, Structure = structure, Trajectory = trajectory,
                Score = 100 * structure.AdjustScore(shapeScore), MeanPressure = -1, Speed = -1
            };
            // Per-stroke colors reflect structure too. Do not recompute the final score
            // from this display mapping: the weakest-stroke cap contains a fixed allowance.
            structure.Apply(shape.Mapping, template.StrokeList.Count);
            var pressures = raw.Where(p => p.Pressure > 0 && p.Pressure <= 1024).Select(p => (double)p.Pressure).ToArray();
            if (pressures.Length > 0) evaluation.MeanPressure = (int)pressures.Average();
            double distance = 0, duration = 0;
            foreach (var line in hand.StrokeList)
                for (int i = 1; i < line.Count; i++)
                {
                    long elapsed = line[i].Time - line[i - 1].Time;
                    if (elapsed <= 0) continue;
                    double dx = line[i].X - line[i - 1].X, dy = line[i].Y - line[i - 1].Y;
                    distance += Math.Sqrt(dx * dx + dy * dy); duration += elapsed;
                }
            if (duration > 0) evaluation.Speed = 10 * distance / duration;
            var messages = shape.Messages.Concat(structure.Messages).Concat(trajectory.Errors).Distinct().ToList();
            if (messages.Count == 0)
                messages.Add(evaluation.Passed ? "筆順、整筆軌跡與字形符合範例。" : "字形整體差異較大，請對照範例。");
            evaluation.Feedback = string.Join("\r\n", messages);
            return evaluation;
        }
    }
}
