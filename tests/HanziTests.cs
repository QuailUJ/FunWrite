using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using FunWrite;
class HanziTests
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            string root = AppPaths.Root;
            foreach (string file in Directory.GetFiles(Path.Combine(root, "assets", "hanzi"), "*.json"))
            {
                string word = Path.GetFileNameWithoutExtension(file);
                var data = TemplateLibrary.ReadImported(root, word);
                Stroke stroke;
                stroke = TemplateLibrary.Load(root, word);
                Assert(stroke.StrokeList.Count == data.medians.Length, word + " stroke count changed");
                for (int i = 0; i < data.medians.Length; i++)
                {
                    var median = data.medians[i]; var line = stroke.StrokeList[i];
                    Assert(line.First().X == median.First()[0] && line.First().Y == median.First()[1] + 124 &&
                        line.Last().X == median.Last()[0] && line.Last().Y == median.Last()[1] + 124, word + " endpoint changed");
                }
                Assert(ShapeScorer.Overall(ShapeScorer.Match(stroke, stroke), stroke) > .9999, word + " identity shape failed");
                var raw = stroke.StrokeList.SelectMany((line, i) => line.Concat(new[] {
                    new Sample(i * 2 + 1, 0, line.Last().X, line.Last().Y, 0) })).ToArray();
                Assert(TrajectoryChecker.Check(stroke, raw).Passed, word + " identity trajectory failed");
                Console.WriteLine("PASS " + word + ": " + stroke.StrokeList.Count + " strokes; endpoints, shape and trajectory");
            }
            var thought = TemplateLibrary.ReadImported(root, "思");
            Assert(thought.decomposition == "⿱田心" && thought.matches.Take(5).All(x => x.SequenceEqual(new[]{0})) &&
                thought.matches.Skip(5).All(x => x.SequenceEqual(new[]{1})), "Thought component mapping lost");
            var words = TemplateLibrary.PracticeWords(root);
            Assert(words.Length == 12 && words.Contains("土") && words.Contains("士"), "Default slots invalid");
            Console.WriteLine("PASS: preserved component data and valid 12-word practice list.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
