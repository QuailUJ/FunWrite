using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;


namespace FunWrite
{
    public static class RecordStore
    {
        public static string Save(string root, string word, int slot, IEnumerable<Sample> samples)
        {
            string directory = Path.Combine(root, "records");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + word + "_" + slot + ".txt");
            var text = samples.Select(p => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2:R} {3:R} {4}",
                p.StrokeNumber, p.Time, p.X, p.Y, p.Pressure));
            // CreateNew refuses to overwrite an existing handwriting recording.
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                foreach (string line in text) writer.WriteLine(line);
            return path;
        }
    }
}
