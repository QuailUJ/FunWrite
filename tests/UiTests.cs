using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using FunWrite;
class UiTests
{
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    [STAThread] static int Main()
    {
        try {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            foreach(int count in new[]{1,10,12}){
                var words=TemplateLibrary.PracticeWords(AppPaths.Root);
                if(count<words.Length)Array.Resize(ref words,count);
                using(var form=new PracticeForm(new PracticeSession(AppPaths.Root,words,false))){
                    form.WindowState=FormWindowState.Normal;form.Size=new Size(1920,1080);
                    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);
                    form.Show();Application.DoEvents();
                    Assert(form.Opacity==1&&form.Controls.Count==0,"Startup has transparent or separately painted child layers");
                    var getStyle=typeof(Control).GetMethod("GetStyle",BindingFlags.Instance|BindingFlags.NonPublic);
                    foreach(var style in new[]{ControlStyles.Opaque,ControlStyles.OptimizedDoubleBuffer,ControlStyles.AllPaintingInWmPaint})
                        Assert((bool)getStyle.Invoke(form,new object[]{style}),"Missing atomic paint style: "+style);
                    Assert(!form.IsConnecting,"Preview unexpectedly connects tablet");
                    using(var image=new Bitmap(form.Width,form.Height)){
                        form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));
                        Assert(image.GetPixel(2,2).A==255&&image.GetPixel(image.Width-3,image.Height-3).A==255,"Background is transparent");
                        image.Save(Path.Combine(AppPaths.Root,"docs","ui-preview-"+count+".png"));
                    }
                    if(count==12){
                        form.Size=new Size(1024,768);Application.DoEvents();
                        using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(AppPaths.Root,"docs","ui-preview-small.png"));}
                    }
                }
                Console.WriteLine("PASS: "+count+" words, opaque single buffered frame, full template layout, no hardware in preview.");
            }
            return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
