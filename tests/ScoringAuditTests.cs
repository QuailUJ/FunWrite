using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FunWrite;
class ScoringAuditTests
{
    static int checks, failures;
    static void Check(bool condition, string message) { checks++; if(!condition) {failures++; Console.WriteLine("FAIL: "+message);} }
    static Sample[] Raw(IEnumerable<List<Sample>> lines)
    {
        var samples=new List<Sample>(); int i=0;
        foreach(var line in lines) {
            foreach(var p in line) samples.Add(new Sample(i*2,samples.Count*10,p.X,p.Y,500));
            var last=line.Last(); samples.Add(new Sample(i*2+1,samples.Count*10,last.X,last.Y,0)); i++;
        }
        return samples.ToArray();
    }
    static Stroke Clone(Stroke t) {
        var h=new Stroke(); foreach(var line in t.StrokeList) h.StrokeList.Add(TrajectoryChecker.Copy(line).ToList());return h;
    }
    static int Main()
    {
        try {
            var root=AppPaths.Root;
            var words=Directory.GetFiles(Path.Combine(root,"assets","templates"),"*.txt").Select(Path.GetFileNameWithoutExtension)
                .Concat(Directory.GetFiles(Path.Combine(root,"assets","hanzi"),"*.json").Select(Path.GetFileNameWithoutExtension)).Distinct().ToArray();
            foreach(var word in words) {
                Stroke t;t = TemplateLibrary.Load(root, word);
                Check(TrajectoryChecker.Check(t,Raw(t.StrokeList)).Passed,word+" exact rejected");
                for(int i=0;i<t.StrokeList.Count;i++) {
                    var h=Clone(t);h.StrokeList[i].Reverse();
                    Check(!TrajectoryChecker.Check(t,Raw(h.StrokeList)).Passed,word+" reverse stroke "+(i+1)+" accepted");
                }
                for(int i=0;i<t.StrokeList.Count-1;i++) {
                    var h=Clone(t);var line=h.StrokeList[i];h.StrokeList[i]=h.StrokeList[i+1];h.StrokeList[i+1]=line;
                    Check(!TrajectoryChecker.Check(t,Raw(h.StrokeList)).Passed,word+" adjacent order "+(i+1)+"/"+(i+2)+" accepted");
                }
                // A geometrically identical split must not be displayed as a passing score.
                var split=Clone(t);int longest=Enumerable.Range(0,split.StrokeList.Count).OrderByDescending(i=>split.StrokeList[i].Count).First();
                var whole=split.StrokeList[longest]; int at=whole.Count/2;
                split.StrokeList[longest]=whole.Take(at+1).ToList();split.StrokeList.Insert(longest+1,whole.Skip(at).ToList());
                var splitResult=WritingScorer.Evaluate(t,Raw(split.StrokeList),TemplateLibrary.ReadImported(root,word));
                Check(splitResult.Score<60 && !splitResult.Passed,word+" split score still >=60 ("+splitResult.Score.ToString("F1")+")");
            }
            var nearby=new Stroke();
            nearby.StrokeList.Add(new List<Sample>{new Sample(0,0,0,0,500),new Sample(0,10,1000,0,500)});
            nearby.StrokeList.Add(new List<Sample>{new Sample(2,20,0,10,500),new Sample(2,30,1000,10,500)});
            Check(!TrajectoryChecker.Check(nearby,Raw(nearby.StrokeList.AsEnumerable().Reverse())).Passed,"Nearby parallel strokes wrong order accepted");
            var tiny=new Stroke();
            tiny.StrokeList.Add(new List<Sample>{new Sample(0,0,0,0,500),new Sample(0,10,1000,0,500)});
            tiny.StrokeList.Add(new List<Sample>{new Sample(2,20,500,300,500),new Sample(2,30,510,290,500)});
            var reverseTiny=Clone(tiny);reverseTiny.StrokeList[1].Reverse();
            Check(!TrajectoryChecker.Check(tiny,Raw(reverseTiny.StrokeList)).Passed,"Short dot reverse direction accepted");
            var data=TemplateLibrary.ReadImported(root,"思"); Stroke thought;thought = TemplateLibrary.Load(root, "思");
            var baseMapping=ShapeScorer.Match(thought,thought);
            for(int i=1;i<=thought.StrokeList.Count;i++) baseMapping[2,i]=i==1?.1:1;
            double before=ShapeScorer.Overall(baseMapping,thought);
            // Set a real structure result to a known multiplier, without internal setters.
            var factor=typeof(StructureResult).GetProperty("Multiplier");var supported=typeof(StructureResult).GetProperty("Supported");var evaluated=typeof(StructureResult).GetProperty("Evaluated");
            var penalty=new StructureResult();factor.SetValue(penalty,.5,null);supported.SetValue(penalty,true,null);evaluated.SetValue(penalty,true,null);
            Check(Math.Abs(penalty.AdjustScore(before)-before*.5)<1e-8,"Structure multiplier is not applied to actual final score");
            var distorted=Clone(thought);
            foreach(var line in distorted.StrokeList.Take(5))foreach(var p in line)p.Y+=300;
            var run=WritingScorer.Evaluate(thought,Raw(distorted.StrokeList),data);
            double shapeOnly=ShapeScorer.Overall(ShapeScorer.Match(thought,distorted),thought);
            Check(Math.Abs(run.Score/100-shapeOnly*run.Structure.Multiplier)<1e-8,"Integrated structure score formula differs from documented formula");
            var noTime=Raw(thought.StrokeList);foreach(var p in noTime)p.Time=0;
            var zeroTime=WritingScorer.Evaluate(thought,noTime,data);
            Check(!double.IsNaN(zeroTime.Speed)&&!double.IsInfinity(zeroTime.Speed)&&zeroTime.Speed<0,"Zero-duration speed is invalid");
            foreach(var p in noTime)p.Pressure=p.Pressure==0?(ushort)0:(ushort)9999;
            Check(WritingScorer.Evaluate(thought,noTime,data).MeanPressure==-1,"Legacy pressure markers treated as physical pressure");
            var repeatedEnd=Clone(thought);
            foreach(var line in repeatedEnd.StrokeList)line.Add(TrajectoryChecker.Copy(new[]{line.Last()})[0]);
            Check(100*ShapeScorer.Overall(ShapeScorer.Match(thought,repeatedEnd),thought)>99.999,"Repeated endpoint altered complete curve geometry");
            Stroke seven;seven = TemplateLibrary.Load(root, "七");
            var justBelow=Clone(seven);var justAbove=Clone(seven);
            float anchor=seven.StrokeList[1][0].X;
            foreach(var p in justBelow.StrokeList[1])p.X=anchor+(p.X-anchor)*.3999f;
            foreach(var p in justAbove.StrokeList[1])p.X=anchor+(p.X-anchor)*.4001f;
            double below=ShapeScorer.Overall(ShapeScorer.Match(seven,justBelow),seven);
            double above=ShapeScorer.Overall(ShapeScorer.Match(seven,justAbove),seven);
            Check(Math.Abs(below-above)*100<.1,"Severe-span penalty jumps at 40% boundary");
            Console.WriteLine("AUDIT: "+checks+" checks, "+failures+" failures across "+words.Length+" characters.");
            return failures==0?0:1;
        } catch(Exception e) {Console.Error.WriteLine(e);return 2;}
    }
}
