using System;
using System.IO;
using System.Linq;

namespace FunWrite
{
    public static class AppPaths
    {
        public static readonly bool Preview = Environment.GetCommandLineArgs().Contains("--preview");
        public static readonly string Root = FindRoot();
        static string FindRoot()
        {
            for (var folder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); folder != null; folder = folder.Parent)
                if (File.Exists(Path.Combine(folder.FullName, "assets", "practice-words.txt"))) return folder.FullName;
            throw new DirectoryNotFoundException("找不到 assets/practice-words.txt，請保留完整 FunWrite2.0 資料夾。");
        }
    }
}
