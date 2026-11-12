using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Security;
using System.Text;
class DrawFlowchart
{
    static Graphics g;
    static StringBuilder svg=new StringBuilder();
    static Font heading=new Font("Microsoft JhengHei",28,FontStyle.Bold),title=new Font("Microsoft JhengHei",18,FontStyle.Bold),body=new Font("Microsoft JhengHei",12),methodFont=new Font("Consolas",11);
    static void Text(string text,int x,int y,Font font,string color)
    {
        using(var brush=new SolidBrush(ColorTranslator.FromHtml(color)))g.DrawString(text,font,brush,x,y);
        var lines=text.Split('\n');
        for(int i=0;i<lines.Length;i++)svg.AppendFormat("<text x='{0}' y='{1}' font-size='{2}' fill='{3}' font-family='Microsoft JhengHei, sans-serif'>{4}</text>",x,y+font.Size*1.3+i*font.GetHeight(g),font.Size*1.333,color,SecurityElement.Escape(lines[i]));
    }
    static void Box(int x,int y,int w,int h,string name,string detail,string color="#e8f3fa",string method="")
    {
        using(var fill=new SolidBrush(ColorTranslator.FromHtml(color)))g.FillRectangle(fill,x,y,w,h);
        using(var pen=new Pen(ColorTranslator.FromHtml("#517b9a"),2))g.DrawRectangle(pen,x,y,w,h);
        svg.AppendFormat("<rect x='{0}' y='{1}' width='{2}' height='{3}' rx='6' fill='{4}' stroke='#517b9a' stroke-width='2'/>",x,y,w,h,color);
        Text(name,x+16,y+6,title,"#143750");
        Text(method,x+16,y+40,methodFont,"#176b78");Text(detail,x+16,y+64,body,"#23475d");
    }
    static void Arrow(params Point[] points)
    {
        using(var pen=new Pen(ColorTranslator.FromHtml("#517b9a"),3))
        using(var cap=new AdjustableArrowCap(5,7)){pen.CustomEndCap=cap;g.DrawLines(pen,points);}
        svg.Append("<polyline points='");foreach(var p in points)svg.Append(p.X+","+p.Y+" ");svg.Append("' fill='none' stroke='#517b9a' stroke-width='3' marker-end='url(#arrow)'/>");
    }
    static int Main(string[] args)
    {
        string directory=args[0];Directory.CreateDirectory(directory);
        using(var image=new Bitmap(1600,1640))using(g=Graphics.FromImage(image)){
            g.Clear(Color.FromArgb(248,251,253));g.SmoothingMode=SmoothingMode.AntiAlias;
            svg.Append("<svg xmlns='http://www.w3.org/2000/svg' width='1600' height='1640' viewBox='0 0 1600 1640'><defs><marker id='arrow' markerWidth='10' markerHeight='10' refX='8' refY='5' orient='auto'><path d='M0,0 L10,5 L0,10 Z' fill='#517b9a'/></marker></defs><rect width='1600' height='1640' fill='#f8fbfd'/>");
            Text("FunWrite 2.0 程式流程",42,18,heading,"#143750");
            Text("主程式與操作",40,78,title,"#176b78");Text("WritingScorer 評分內部",880,78,title,"#176b78");
            Box(40,125,600,95,"Program / TemplateLibrary","讀取字單、範例中心線與部件資料",method:"Main() → TemplateLibrary.PracticeWords(), Load(), ReadImported()");
            Box(40,260,600,95,"PracticeForm","完整不透明畫面一次繪製／雙緩衝",method:"PracticeForm.OnPaint() / DrawPractice() / DrawGrid()");
            Box(40,395,600,95,"TabletDevice","背景連接寫字板；座標與壓力正規化",method:"TabletDevice.ConnectAsync() / Receive() / Normalize()");
            Box(40,530,600,95,"PracticeSession","選字格、保存原始落筆與提筆事件",method:"PracticeForm.Feed() → PracticeSession.Accept() / Select()");
            Box(40,665,600,95,"要求評分","螢幕按鈕、Enter 或實體評分區",method:"PracticeForm.Score() / PracticeSession.ScorePending()");
            Box(40,800,600,95,"WritingScorer","使用右欄同一套評分流程", "#dff3ed",method:"WritingScorer.Evaluate()");
            Box(40,935,600,95,"RecordStore","先保存原始筆跡，保留可重算的資料",method:"RecordStore.Save()");
            Box(40,1070,600,95,"判斷是否通過","筆順與整筆檢查通過，而且總分 ≥ 60",method:"WritingEvaluation.Passed / PracticeSession.ScorePending()");
            for(int y=220;y<=1030;y+=135)Arrow(new Point(340,y),new Point(340,y+35));
            Box(40,1235,270,90,"通過","增加一枚金幣","#dff3ed",method:"ScorePending(): Coins++");
            Box(370,1235,270,90,"未通過","不增加金幣","#fff0e8",method:"ScorePending(): !Passed");
            Arrow(new Point(240,1165),new Point(240,1200),new Point(175,1200),new Point(175,1235));
            Arrow(new Point(440,1165),new Point(440,1200),new Point(505,1200),new Point(505,1235));
            Box(40,1370,600,95,"PracticeForm 更新結果","顯示分數、錯誤筆畫、回饋與練習格",method:"RefreshSession() → Invalidate() → OnPaint()");
            Arrow(new Point(175,1325),new Point(175,1370));Arrow(new Point(505,1325),new Point(505,1370));
            Box(880,125,660,95,"複製原始筆跡","不裁尾、不自動接回提筆、不拆次筆畫",method:"TrajectoryChecker.Copy()");
            Box(880,260,660,95,"接觸段整理","一次落筆至提筆為一筆，保留完整端點",method:"TrajectoryChecker.ContactStroke()");
            Box(880,395,660,110,"ShapeScorer","整字等比例縮放與平移；完整筆畫一對一比對\n線條、長度、路徑、端點、彎曲、展開",method:"Evaluate() → Normalize(), BestAlignment(), Pair() / Overall()");
            Box(880,545,660,110,"StructureScorer","有部件資料時，比較大小比例、距離與對齊\n只量測原本關係，不分開放大部件",method:"StructureScorer.Check() → SizeRatio(), Difference()");
            Box(880,695,660,110,"TrajectoryChecker","檢查筆數、筆順、方向、整筆路徑與中途提筆\n筆數不符的 59 分上限在 ShapeScorer.Evaluate() 套用",method:"TrajectoryChecker.Check() → Assign(), Distance(), EndDistance()");
            Box(880,845,660,130,"總分與通過判定","字形分 = min（筆長加權平均，最差筆 + 25）\n總分 = 字形分 × 結構倍率\n速度與力量僅回饋，不加扣分", "#dff3ed",method:"ShapeScorer.Overall() / StructureResult.AdjustScore()");
            Box(880,1015,660,110,"WritingEvaluation","傳回精確分數、是否通過、筆畫配對與錯誤說明\n通過狀態與字形分數分開呈現",method:"WritingScorer.Evaluate() → WritingEvaluation.Passed");
            int[] ends={220,355,505,655,805,975};foreach(int y in ends)Arrow(new Point(1210,y),new Point(1210,y+35));
            Box(880,1210,660,100,"回到練習","可重播、動態比對、重寫此格或選另一字格",method:"PracticeForm.Animate() / PracticeSession.ClearSelected(), Select()");
            Arrow(new Point(1210,1125),new Point(1210,1210));
            Text("啟動時不等待裝置連線；沒有連線也能查看範例。",880,1365,body,"#23475d");
            Box(40,1510,1500,90,"歷史回顧","讀取既有筆跡 → 同一套 WritingScorer → 顯示結果（不增加金幣）","#edf0fa",method:"PracticeForm.OpenRecord() → PracticeSession.Review() → WritingScorer.Evaluate()");
            svg.Append("</svg>");image.Save(Path.Combine(directory,"program-flow.png"));
            File.WriteAllText(Path.Combine(directory,"program-flow.svg"),svg.ToString(),new UTF8Encoding(false));
        }
        heading.Dispose();title.Dispose();body.Dispose();methodFont.Dispose();return 0;
    }
}
