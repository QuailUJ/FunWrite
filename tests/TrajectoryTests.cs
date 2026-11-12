using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FunWrite;
class TrajectoryTests
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    static List<Sample> Raw(IEnumerable<List<Sample>> lines)
    {
        var samples = new List<Sample>();
        int number = 0;
        foreach (var line in lines)
        {
            foreach (var p in line) samples.Add(new Sample(number, samples.Count, p.X, p.Y, 500));
            var end = line.Last();
            samples.Add(new Sample(number++, samples.Count, end.X, end.Y, 0));
        }
        return samples;
    }
    static int Main(string[] args)
    {
        try
        {
            foreach (var file in Directory.GetFiles(args[0], "*.txt"))
            {
                var template = new Stroke(File.ReadAllText(file));
                var lines = template.StrokeList;
                string name = Path.GetFileNameWithoutExtension(file);
                var raw = Raw(lines);
                var exact = TrajectoryChecker.Check(template, raw);
                Assert(exact.Passed, name + " exact: " + exact.Feedback);
                var mild = raw.Select(p => new Sample(p.StrokeNumber, p.Time, p.X * 2 + 300 + (float)(2 * Math.Sin(p.Y)), p.Y * 2 - 500, p.Pressure));
                Assert(TrajectoryChecker.Check(template, mild).Passed, name + " mild variation");
                var reverse = lines.Select(line => line.AsEnumerable().Reverse().ToList()).ToList();
                Assert(!TrajectoryChecker.Check(template, Raw(reverse)).Passed, name + " reverse direction passed");
                var split = lines.Select(line => line.ToList()).ToList();
                int at = split[0].Count / 2;
                var tail = split[0].Skip(at).ToList();
                split[0] = split[0].Take(at + 1).ToList();
                split.Insert(1, tail);
                Assert(!TrajectoryChecker.Check(template, Raw(split)).Passed, name + " penlift passed");
                Assert(!TrajectoryChecker.Check(template, new Sample[0]).Passed, name + " empty passed");
                if (lines.Count > 1)
                {
                    var swap = lines.Select(line => line.ToList()).ToList();
                    var first = swap[0]; swap[0] = swap[1]; swap[1] = first;
                    Assert(!TrajectoryChecker.Check(template, Raw(swap)).Passed, name + " swapped order passed");
                    var repeated = lines.Select(line => line.ToList()).ToList(); repeated[1] = repeated[0];
                    Assert(!TrajectoryChecker.Check(template, Raw(repeated)).Passed, name + " repeated/missing passed");
                    Assert(!TrajectoryChecker.Check(template, Raw(lines.Skip(1))).Passed, name + " missing passed");
                    split.RemoveAt(split.Count - 1);
                    Assert(!TrajectoryChecker.Check(template, Raw(split)).Passed, name + " split plus missing passed");
                }
                var copy = TrajectoryChecker.Copy(raw);
                new Stroke(copy);
                Assert(raw.Count(p => p.Pressure == 0) == lines.Count, "Original contact data mutated");
                Console.WriteLine("PASS " + name + ": trajectory, order, direction, penlift, missing/repeated, mild variation");
            }
            return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
