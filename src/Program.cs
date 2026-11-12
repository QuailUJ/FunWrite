using System;
using System.Windows.Forms;
using System.Reflection;
using System.IO;

[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]

namespace FunWrite
{
    static class Program
    {
        [STAThread] static void Main()
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new PracticeForm(new PracticeSession(AppPaths.Root))); }
            catch (Exception ex) { MessageBox.Show("無法開啟練習：" + ex.Message, "FunWrite 2.1"); }
        }
    }
}
