using System;
using System.Globalization;
using System.IO;
using FunWrite;
class GoldenScoreTests
{
    static int Main()
    {
        try {
            int count=0;double maximum=0;
            foreach(string line in File.ReadAllLines(Path.Combine(AppPaths.Root,"tests","fixtures","scores-before-refactor.csv"))){
                if(line.StartsWith("file,"))continue;var p=line.Split(',');
                var expected=double.Parse(p[2],CultureInfo.InvariantCulture);bool pass=bool.Parse(p[3]);
                string word=p[1];var samples=TrajectoryChecker.Parse(File.ReadAllText(Path.Combine(AppPaths.Root,"records",p[0])));
                var result=WritingScorer.Evaluate(TemplateLibrary.Load(AppPaths.Root,word),samples,TemplateLibrary.ReadImported(AppPaths.Root,word));
                double difference=Math.Abs(result.Score-expected);maximum=Math.Max(maximum,difference);
                if(difference>.0001||result.Passed!=pass)throw new Exception("Score/pass changed: "+p[0]+" old="+expected+" new="+result.Score+" oldPass="+pass+" newPass="+result.Passed);
                count++;
            }
            Console.WriteLine("PASS: "+count+" saved recordings retain score and verdict; maximum rounding difference="+maximum.ToString("F8",CultureInfo.InvariantCulture));return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
