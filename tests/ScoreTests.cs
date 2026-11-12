using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FunWrite;

class ScoreTests
{
    static double Score(Stroke a, Stroke b) { return 100 * ShapeScorer.Overall(ShapeScorer.Match(a, b), a); }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Stroke Copy(Stroke source, Func<Sample, Sample> transform)
    {
        var copy = new Stroke();
        foreach (var line in source.StrokeList) copy.StrokeList.Add(line.Select(transform).ToList());
        return copy;
    }
    static Sample Clone(Sample p) { return new Sample(p.StrokeNumber, p.Time, p.X, p.Y, p.Pressure); }
    static int Main(string[] args)
    {
        try
        {
            foreach (var file in Directory.GetFiles(args[0], "*.txt"))
            {
                var template = new Stroke(File.ReadAllText(file));
                Assert(template.StrokeList.Count > 0, "Empty template: " + file);
                double exact = Score(template, template);
                var scaled = Copy(template, p => new Sample(p.StrokeNumber, p.Time, p.X * 2 + 300, p.Y * 2 - 500, p.Pressure));
                Assert(exact > 99.99 && Score(template, scaled) > 99.99, "Scale/translation changed score");
                var reversed = Copy(template, Clone);
                reversed.StrokeList.Reverse();
                foreach (var line in reversed.StrokeList) line.Reverse();
                Assert(Score(template, reversed) > 99, "Order/direction affected shape");
                var split = Copy(template, Clone);
                var longest = split.StrokeList.OrderByDescending(x => x.Count).First();
                int at = longest.Count / 2;
                split.StrokeList.Remove(longest);
                split.StrokeList.Add(longest.Take(at + 1).ToList());
                split.StrokeList.Add(longest.Skip(at).ToList());
                Assert(Score(template, split) < 60, "Pen-count mismatch must not display a passing shape score");
                var mild = Copy(template, p => new Sample(p.StrokeNumber, p.Time, p.X + (float)(4 * Math.Sin(p.Y / 40.0)), p.Y, p.Pressure));
                Assert(Score(template, mild) > 90, "Small variation collapsed score");
                var extra = Copy(template, Clone);
                float minX = template.StrokeList.SelectMany(x => x).Min(p => p.X);
                float maxX = template.StrokeList.SelectMany(x => x).Max(p => p.X);
                float maxY = template.StrokeList.SelectMany(x => x).Max(p => p.Y);
                extra.StrokeList.Add(new List<Sample> { new Sample(99, 0, minX, maxY + 150, 1), new Sample(99, 1, maxX, maxY + 150, 1) });
                Assert(Score(template, extra) < 95, "Extra stroke not penalized");
                double missing = 0;
                if (template.StrokeList.Count > 1)
                {
                    var omitted = Copy(template, Clone);
                    omitted.StrokeList.Remove(omitted.StrokeList.OrderByDescending(x => x.Count).First());
                    missing = Score(template, omitted);
                    Assert(missing < 95, "Missing stroke not penalized");
                }
                Assert(Score(template, new Stroke()) == 0, "Empty input must score zero");
                Console.WriteLine(Path.GetFileNameWithoutExtension(file) + ": exact=" + exact.ToString("F1") + ", mild=" + Score(template, mild).ToString("F1") + ", missing=" + missing.ToString("F1") + ", extra=" + Score(template, extra).ToString("F1"));
            }
            string records = Path.GetFullPath(Path.Combine(args[0], "..", "..", "records"));
            string[] examples = { "20261021_144100_255_二_5.txt", "20261021_144142_055_七_22.txt", "20261021_144228_852_乃_29.txt" };
            foreach (string name in examples)
            {
                string word = name.Split('_')[3];
                var template = new Stroke(File.ReadAllText(Path.Combine(args[0], word + ".txt")));
                var handwriting = new Stroke(File.ReadAllText(Path.Combine(records, name)));
                double score = Score(template, handwriting);
                if (word == "二")
                    // This sample is vertically compressed relative to the template. With
                    // independent-axis stretching removed, it must no longer be boosted >80.
                    Assert(score >= 30 && score < 80, "Compressed 二 was corrected away or collapsed: " + score);
                else
                    Assert(score >= (word == "七" ? 75 : 80) && score < 100, "Recorded plausible handwriting should not collapse or receive a perfect score: " + name);
                Console.WriteLine("Recorded " + word + ": " + score.ToString("F1"));
            }
            var straightTemplate = new Stroke(File.ReadAllText(Path.Combine(args[0], "一.txt")));
            foreach (string name in new[] { "20261021_145641_984_六_19.txt", "20261021_145744_883_思_37.txt" })
            {
                string word = name.Split('_')[3];
                var template = new Stroke(File.ReadAllText(Path.Combine(args[0], word + ".txt")));
                double score = Score(template, new Stroke(File.ReadAllText(Path.Combine(records, name))));
                Assert(score < 60, "Severe stroke/component proportions must not pass shape: " + name);
                Console.WriteLine("Distorted " + word + ": " + score.ToString("F1"));
            }
            double straight = Score(straightTemplate, new Stroke(File.ReadAllText(Path.Combine(records, "20261021_144020_883_一_0.txt"))));
            double curved = Score(straightTemplate, new Stroke(File.ReadAllText(Path.Combine(records, "20261021_144032_521_一_2.txt"))));
            Assert(straight - curved > 15, "Curved incorrect 一 must remain worse than the straight sample");
            Console.WriteLine("PASS: synthetic geometry and recorded handwriting regressions.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
