using System;
using System.Globalization;
using System.IO;
using System.Linq;

using FunWrite;
class StrokeAccuracyTests
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    static Sample[] Read(string path)
    {
        return File.ReadAllLines(path).Where(s=>!string.IsNullOrWhiteSpace(s)).Select(s=>{
            var p=s.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
            return new Sample(int.Parse(p[0]),long.Parse(p[1]),float.Parse(p[2],CultureInfo.InvariantCulture),float.Parse(p[3],CultureInfo.InvariantCulture),ushort.Parse(p[4]));
        }).ToArray();
    }
    static int Main()
    {
        try
        {
            string root=AppPaths.Root;
            foreach(string name in new[]{"20261021_154124_336_乃_30.txt","20261021_154308_706_四_9.txt","20261021_154116_589_乃_29.txt"})
            {
                string word=name.Split('_')[3]; Stroke template;
                template = TemplateLibrary.Load(root, word);
                var raw=Read(Path.Combine(root,"records",name));
                var hand=TrajectoryChecker.ContactStroke(raw);
                var shape=ShapeScorer.Evaluate(template,hand);
                double score=100*ShapeScorer.Overall(shape.Mapping,template);
                var matches=Enumerable.Range(1,template.StrokeList.Count).Select(i=>(int)shape.Mapping[0,i]).ToArray();
                Assert(matches.Distinct().Count()==matches.Length,"A handwriting stroke was reused: "+name);
                if(name.Contains("154124"))
                {
                    Assert(score<50 && shape.Mapping[2,2]<.6,"Compressed 乃 was over-scored: "+score);
                    Assert(!TrajectoryChecker.Check(template,raw).Passed,"Wrong-order 乃 passed");
                }
                else if(word=="四")
                {
                    Assert(score<60 && shape.Mapping[2,4]<.6,"Missing turn 四 was over-scored: "+score);
                    Assert(shape.Mapping[0,5]==5,"Bottom stroke matched an inner vertical");
                    Assert(shape.Feedback.Contains("範例第 4 筆"),"Missing turn not explained");
                }
                else Assert(score>80,"Normal 乃 collapsed: "+score);
                Console.WriteLine(name+": "+score.ToString("F1")+" | "+shape.Feedback);
            }
            Stroke seven;  seven = TemplateLibrary.Load(root, "七");
            var noHook=new Stroke();
            for(int i=0;i<seven.StrokeList.Count;i++)
                noHook.StrokeList.Add(seven.StrokeList[i].Select(p=>new Sample(p.StrokeNumber,p.Time,i==1?seven.StrokeList[i][0].X:p.X,p.Y,p.Pressure)).ToList());
            var bad=ShapeScorer.Evaluate(seven,noHook);
            Assert(bad.Mapping[2,2]<.6 && 100*ShapeScorer.Overall(bad.Mapping,seven)<60,"Missing hook not penalized");
            var sparse=new Stroke();
            foreach(var line in seven.StrokeList)
                sparse.StrokeList.Add(line.Where((p,i)=>i%3==0||i==line.Count-1).Select(p=>new Sample(p.StrokeNumber,p.Time,p.X,p.Y,p.Pressure)).ToList());
            Assert(ShapeScorer.Overall(ShapeScorer.Match(seven,sparse),seven)>.95,"Sampling density affected geometry");
            Assert(ShapeScorer.Overall(ShapeScorer.Match(seven,new Stroke()),seven)==0,"Empty input passed");
            Console.WriteLine("PASS: real bad/good samples, unique whole-stroke pairing, missing turn/hook, sampling density, empty input.");
            return 0;
        }
        catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
