using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;


namespace FunWrite
{
    public sealed class HanziTemplate
    {
        public string character { get; set; }
        public string[] strokes { get; set; }
        public float[][][] medians { get; set; }
        public string decomposition { get; set; }
        public int[][] matches { get; set; }
        public string source { get; set; }
        public string revision { get; set; }
        public string strokeOrderStandard { get; set; }
        public bool taiwanStrokeOrderVerified { get; set; }
    }

    public static class TemplateLibrary
    {
        public static HanziTemplate ReadImported(string root, string word)
        {
            string path = Path.Combine(root, "assets", "hanzi", word + ".json");
            if (!File.Exists(path)) return null;
            var data = new JavaScriptSerializer().Deserialize<HanziTemplate>(File.ReadAllText(path));
            ValidateImported(data, word);
            return data;
        }

        static void ValidateImported(HanziTemplate data, string word)
        {
            if (data == null || data.character != word || data.medians == null || data.medians.Length == 0 ||
                data.strokes == null || data.strokes.Length != data.medians.Length ||
                data.matches == null || data.matches.Length != data.medians.Length ||
                data.medians.Any(line => line == null || line.Length < 2 ||
                    line.Any(p => p == null || p.Length != 2 || p.Any(v => float.IsNaN(v) || float.IsInfinity(v)))))
                throw new InvalidDataException("範例資料不完整：" + word);
        }

        public static Stroke Load(string root, string word)
        {
            var data = ReadImported(root, word);
            if (data == null) return new Stroke(File.ReadAllText(Path.Combine(root, "assets", "templates", word + ".txt")));
            var samples = new System.Collections.Generic.List<Sample>();
            for (int i = 0; i < data.medians.Length; i++)
            {
                var points = data.medians[i];
                for (int j = 0; j < points.Length - 1; j++)
                {
                    double dx = points[j + 1][0] - points[j][0], dy = points[j + 1][1] - points[j][1];
                    int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / 4));
                    for (int k = 0; k < steps; k++)
                        samples.Add(new Sample(i * 2, samples.Count * 10L, (float)(points[j][0] + dx * k / steps),
                            (float)(points[j][1] + dy * k / steps + 124), 500));
                }
                var last = points[points.Length - 1];
                samples.Add(new Sample(i * 2, samples.Count * 10L, last[0], last[1] + 124, 500));
                samples.Add(new Sample(i * 2 + 1, samples.Count * 10L, last[0], last[1] + 124, 0));
            }
            return TrajectoryChecker.ContactStroke(samples);
        }

        public static string[] PracticeWords(string root)
        {
            var words = File.ReadAllLines(Path.Combine(root, "assets", "practice-words.txt")).Select(w => w.Trim())
                .Where(w => w.Length > 0 && !w.StartsWith("#")).ToArray();
            ValidateWords(words);
            return words;
        }

        public static void ValidateWords(string[] words)
        {
            if (words == null || words.Length == 0 || words.Length > 12 || words.Distinct().Count() != words.Length ||
                words.Any(w => string.IsNullOrEmpty(w) || w.Length != 1 || w[0] < '\u4e00' || w[0] > '\u9fff'))
                throw new InvalidDataException("練習字單須為 1～12 個不重複中文字，每行一字。");
        }

        public static string[] AvailableWords(string root)
        {
            return new[] { new { Folder = "hanzi", Pattern = "*.json" }, new { Folder = "templates", Pattern = "*.txt" } }
                .SelectMany(p => Directory.GetFiles(Path.Combine(root, "assets", p.Folder), p.Pattern))
                .Select(Path.GetFileNameWithoutExtension).Where(w => w.Length == 1 && w[0] >= '\u4e00' && w[0] <= '\u9fff')
                .Distinct().OrderBy(w => w, StringComparer.CurrentCulture).ToArray();
        }

        public static void SavePracticeWords(string root, string[] words)
        {
            ValidateWords(words);
            foreach (string word in words) Load(root, word);
            string path = Path.Combine(root, "assets", "practice-words.txt"), temporary = path + ".tmp";
            try
            {
                File.WriteAllLines(temporary, words, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, path + ".previous");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static string[] ImportWords(string root, string characters)
        {
            var wanted = (characters ?? "").Where(c => !char.IsWhiteSpace(c)).Select(c => c.ToString()).Distinct().ToArray();
            ValidateWords(wanted);
            var available = AvailableWords(root);
            wanted = wanted.Except(available).ToArray();
            if (wanted.Length == 0) return available;
            string source = Path.Combine(root, "external", "makemeahanzi");
            if (!File.Exists(Path.Combine(source, "dictionary.txt")) || !File.Exists(Path.Combine(source, "graphics.txt")))
                throw new FileNotFoundException("缺少離線字源，請保留 external/makemeahanzi 的 dictionary.txt 與 graphics.txt。");
            // Same fields and coordinate system as tools/Import-Hanzi.ps1; no shell or Git needed at runtime.
            var serializer = new JavaScriptSerializer();
            var entries = new Dictionary<string, HanziTemplate>();
            foreach (string word in wanted) entries[word] = null;
            foreach (string kind in new[] { "graphics", "dictionary" })
                foreach (string line in File.ReadLines(Path.Combine(source, kind + ".txt")))
                    foreach (string word in wanted)
                        if (line.StartsWith("{\"character\":\"" + word + "\"", StringComparison.Ordinal))
                        {
                            var data = serializer.Deserialize<HanziTemplate>(line);
                            if (kind == "graphics") entries[word] = data;
                            else if (entries[word] != null)
                            { entries[word].decomposition = data.decomposition; entries[word].matches = data.matches; }
                            break;
                        }
            foreach (string word in wanted)
            {
                if (entries[word] == null) throw new InvalidDataException("離線字源沒有「" + word + "」，未新增任何字。");
                ValidateImported(entries[word], word);
                if (entries[word].medians.Length > 16)
                    throw new InvalidDataException("「" + word + "」超過目前可完整判定的 16 筆，未新增任何字。");
                entries[word].source = "https://github.com/skishore/makemeahanzi";
                entries[word].revision = "bddc96d41bef78427ed0e034e9f7e31d71fd1b92";
                entries[word].strokeOrderStandard = "upstream-PRC";
                entries[word].taiwanStrokeOrderVerified = false;
            }
            var written = new List<string>();
            try
            {
                foreach (string word in wanted)
                {
                    string path = Path.Combine(root, "assets", "hanzi", word + ".json");
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                    {
                        written.Add(path);
                        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(serializer.Serialize(entries[word]));
                    }
                }
            }
            catch { foreach (string path in written) File.Delete(path); throw; }
            return AvailableWords(root);
        }
    }
}
