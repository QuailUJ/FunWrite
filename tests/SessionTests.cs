using System;
using System.IO;
using System.Linq;

using FunWrite;
class SessionTests
{
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    public static Sample[] Writing(Stroke t,int slot)
    {
        var samples=new System.Collections.Generic.List<Sample>();
        int word=slot/4,row=slot%4;float scale=2000f/Math.Max(t.Width,t.Height);
        foreach(var line in t.StrokeList) {
            foreach(var p in line)samples.Add(new Sample(0,samples.Count*10,word%6*2500+1250+(p.X-t.CenterX)*scale,
                (word<6?22500:10000)-row*2500-1250+(p.Y-t.CenterY)*scale,500));
            var last=samples.Last();samples.Add(new Sample(0,samples.Count*10,last.X,last.Y,0));
        }
        return samples.ToArray();
    }
    static int Main()
    {
        try {
            var session=new PracticeSession(AppPaths.Root,null,false);
            for(int slot=0;slot<48;slot++){
                int word=slot/4,row=slot%4;
                Assert(PracticeSession.SlotAt(word%6*2500+1250,(word<6?22500:10000)-row*2500-1250)==slot,"Paper slot changed: "+slot);
            }
            Assert(PracticeSession.SlotAt(-1,20000)==-1&&PracticeSession.SlotAt(2000,11000)==-1,"Non-writing regions accepted");
            Assert(PracticeSession.SlotAt(1000,22500)==0&&PracticeSession.SlotAt(1000,10000)==24&&PracticeSession.SlotAt(1000,25000)==-1,"Physical grid boundary changed");
            int demos=0,lastDemo=-1;session.DemoRequested+=i=>{demos++;lastDemo=i;};
            session.Accept(new Sample(0,0,1000,24000,500));session.Accept(new Sample(0,1,1000,24000,500));
            Assert(demos==1&&lastDemo==0,"Demo was repeatedly triggered while holding pen");
            session.Accept(new Sample(0,2,1000,24000,0));session.Accept(new Sample(0,3,1000,11000,500));
            Assert(demos==2&&lastDemo==6,"Lower demo row mapping changed");
            session.Accept(new Sample(0,4,1000,11000,0));
            var raw=Writing(session.Templates[0],0);
            foreach(var sample in raw)session.Accept(sample);
            var result=session.ScorePending();
            Assert(result.Evaluation.Passed&&result.Evaluation.Score>99.99&&session.Coins==1,"Exact handwriting not accepted");
            Assert(session.ScorePending()==null&&session.Coins==1,"Repeated score awarded duplicate coin");
            Assert(result.Samples.Length==raw.Length,"Original samples were dropped");
            session.Accept(new Sample(0,10000,1300,21000,500));Assert(session.PendingCount==0,"Filled slot was overwritten");
            session.ClearSelected();session.Accept(new Sample(0,10001,1000,21000,0));
            session.Accept(new Sample(0,10002,1000,21000,500));
            session.Accept(new Sample(0,10002,-100,21000,500));
            Assert(session.Pending.StrokeList[0].Last().X==-100,"Contact path outside the grid was trimmed");
            session.Accept(new Sample(0,10003,20000,21000,0));
            session.Accept(new Sample(0,10004,1050,21000,500));
            Assert(session.Pending.StrokeList.Count==2,"Release outside grid was lost");
            session.Select(4);Assert(session.SelectedSlot==0,"Pending writing was silently switched to another word");
            session.Accept(new Sample(0,10005,16000,500,500));
            Assert(session.Result(0)!=null&&!session.Result(0).Evaluation.Passed,"Split/malformed 一 passed");
            int coins=session.Coins;session.Accept(new Sample(0,10006,16000,500,500));Assert(session.Coins==coins,"Held score button repeated scoring");
            var normalized=TabletDevice.Normalize(4250,6100,1000,2048,100);
            Assert(normalized.X==1250&&normalized.Y==21250&&normalized.Pressure==500,"Tablet calibration changed");
            Assert(TabletDevice.Normalize(4250,6100,1000,0,100).Pressure==1024,"Pressure range overflowed");
            string store=Path.Combine(AppPaths.Root,"tests","output","record-store");
            string file=RecordStore.Save(store,"一",0,raw);
            Assert(TrajectoryChecker.Parse(File.ReadAllText(file)).Length==raw.Length,"Stored samples changed");
            var review=new PracticeSession(AppPaths.Root,null,false);var replay=review.Review(file);
            Assert(replay.Evaluation.Passed&&review.Coins==0,"Review awarded coins or used another score rule");
            Console.WriteLine("PASS: 48 paper slots, demo debounce, scoring/coins, raw data, outside release, rewriting, pressure calibration, storage and replay.");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
