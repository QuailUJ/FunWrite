using System;
using System.Drawing;
using System.IO;
using System.Linq;

using FunWrite;
class StructureTests
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    static Stroke Transform(Stroke source, Func<int, Sample, PointF> transform)
    {
        var copy = new Stroke();
        for (int i = 0; i < source.StrokeList.Count; i++)
            copy.StrokeList.Add(source.StrokeList[i].Select(p => { var q = transform(i,p); return new Sample(p.StrokeNumber,p.Time,q.X,q.Y,p.Pressure); }).ToList());
        return copy;
    }
    static double Score(Stroke template, Stroke hand, HanziTemplate data, out StructureResult structure)
    {
        var mapping = ShapeScorer.Match(template, hand);
        double shapeScore = ShapeScorer.Overall(mapping, template);
        structure = StructureScorer.Check(template, hand, data);
        structure.Apply(mapping, template.StrokeList.Count);
        return 100 * structure.AdjustScore(shapeScore);
    }
    static int Main()
    {
        try
        {
            string root = AppPaths.Root;
            Stroke t;
            t = TemplateLibrary.Load(root, "思");
            var data = TemplateLibrary.ReadImported(root, "思");
            StructureResult result;
            Assert(Score(t, t, data, out result) > 99.99 && result.Evaluated && result.Feedback == "", "Exact structure failed");
            var scaled = Transform(t,(i,p) => new PointF(p.X*2+500,p.Y*2-300));
            Assert(Score(t,scaled,data,out result) > 99.99, "Uniform scale/translation affected structure");
            var top = t.StrokeList.Take(5).SelectMany(x=>x).ToArray();
            float cx = (top.Min(p=>p.X)+top.Max(p=>p.X))/2, cy = (top.Min(p=>p.Y)+top.Max(p=>p.Y))/2;
            var mild = Transform(t,(i,p) => i < 5 ? new PointF(cx+(p.X-cx)*.9f,cy+(p.Y-cy)*.9f+10) : new PointF(p.X,p.Y));
            double mildScore = Score(t,mild,data,out result);
            Assert(mildScore > 85 && result.Multiplier > .95, "Mild variation over-penalized: " + mildScore);
            var tiny = Transform(t,(i,p) => i < 5 ? new PointF(cx+(p.X-cx)*.3f,cy+(p.Y-cy)*.3f) : new PointF(p.X,p.Y));
            double tinyScore = Score(t,tiny,data,out result);
            Assert(tinyScore < 60 && result.Feedback.Contains("偏小"), "Tiny 田 not caught: " + tinyScore + result.Feedback);
            Console.WriteLine("Tiny 田: " + tinyScore.ToString("F1") + " | " + result.Feedback);
            var far = Transform(t,(i,p) => new PointF(p.X,p.Y+(i<5?600:0)));
            double farScore = Score(t,far,data,out result);
            Assert(farScore < 60 && result.Feedback.Contains("距離過遠"), "Large gap not caught: " + farScore + result.Feedback);
            Console.WriteLine("Large gap: " + farScore.ToString("F1") + " | " + result.Feedback);
            var offset = Transform(t,(i,p) => new PointF(p.X+(i<5?500:0),p.Y));
            Assert(Score(t,offset,data,out result)<60 && result.Feedback.Contains("對齊"), "Misalignment not caught");
            var missing = Transform(t,(i,p)=>new PointF(p.X,p.Y)); missing.StrokeList.RemoveAt(2);
            Assert(Score(t,missing,data,out result) <=59.001 && !result.Evaluated && result.Feedback.Contains("無法確認"), "Missing stroke got structure credit");
            var swapped = Transform(t,(i,p)=>new PointF(p.X,p.Y)); swapped.StrokeList.Reverse();
            Assert(Score(t,swapped,data,out result)>99.99, "Component grouping follows stroke order instead of geometric correspondence");
            var squash = Transform(t,(i,p)=>new PointF(p.X,p.Y*.35f));
            double squashScore = Score(t,squash,data,out result);
            Assert(squashScore <85, "Nonuniform stretch was corrected away");
            foreach (string word in new[]{"林","明","好","家","土"})
            {
                Stroke sample; sample = TemplateLibrary.Load(root, word);
                Assert(Score(sample,sample,TemplateLibrary.ReadImported(root,word),out result)>99.99 && result.Evaluated, word+" root layout not supported");
            }
            var noMapping = TemplateLibrary.ReadImported(root,"思"); noMapping.matches[0]=null;
            Assert(!StructureScorer.Check(t,t,noMapping).Supported, "Incomplete matches were guessed");
            foreach(var file in Directory.GetFiles(Path.Combine(root,"records"),"*_思_*.txt"))
            {
                var hand=new Stroke(File.ReadAllText(file));
                double score=Score(t,hand,data,out result);
                Console.WriteLine(Path.GetFileName(file)+": "+score.ToString("F1")+" | "+result.Feedback);
            }
            Console.WriteLine("PASS: exact/mild, whole scale, component size/gap/alignment, missing strokes, shared geometry and other layouts. Mild="+mildScore.ToString("F1")+", squash="+squashScore.ToString("F1"));
            return 0;
        }
        catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
