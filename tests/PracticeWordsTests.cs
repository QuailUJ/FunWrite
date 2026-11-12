using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FunWrite;

class PracticeWordsTests
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Reject(Action action, string message)
    { bool rejected = false; try { action(); } catch (Exception) { rejected = true; } Assert(rejected, message); }
    static System.Collections.Generic.IEnumerable<Control> Controls(Control root)
    { foreach (Control child in root.Controls) { yield return child; foreach (var descendant in Controls(child)) yield return descendant; } }
    [STAThread] static int Main()
    {
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string fixture = Path.Combine(AppPaths.Root, "tests", "output", "practice-words-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(fixture, "assets"));
            foreach (string folder in new[] { "hanzi", "templates" })
            {
                string destination = Path.Combine(fixture, "assets", folder); Directory.CreateDirectory(destination);
                foreach (string file in Directory.GetFiles(Path.Combine(AppPaths.Root, "assets", folder)))
                    File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }
            string[] original = TemplateLibrary.PracticeWords(AppPaths.Root), available = TemplateLibrary.AvailableWords(fixture);
            Assert(available.Length == 22 && original.All(available.Contains), "Available library changed unexpectedly");
            TemplateLibrary.SavePracticeWords(fixture, original);
            var session = new PracticeSession(fixture, null, false);
            foreach (var line in session.Templates[0].StrokeList)
            {
                foreach (var p in line) session.Accept(new Sample(0, 100, 1250 + (p.X - session.Templates[0].CenterX) * 2000 / Math.Max(session.Templates[0].Width, session.Templates[0].Height),
                    21250 + (p.Y - session.Templates[0].CenterY) * 2000 / Math.Max(session.Templates[0].Width, session.Templates[0].Height), 500));
                session.Accept(new Sample(0, 101, 1250, 21250, 0));
            }
            var result = session.ScorePending(); Assert(result.Evaluation.Passed && session.Coins == 1, "Baseline writing failed");
            var reversed = original.Reverse().ToArray(); session.ChangeWords(reversed);
            Assert(session.Result(44) == result && result.Slot == 44 && session.SelectedSlot == 44 && session.Coins == 1, "Reorder lost result, selection or coins");
            Assert(TemplateLibrary.PracticeWords(fixture).SequenceEqual(reversed), "Order not persisted");
            Assert(new PracticeSession(fixture, null, false).Words.SequenceEqual(reversed), "Restart lost order");
            reversed[0] = "錯"; Assert(session.Words[0] == original.Last(), "Live words alias caller array");
            string path = Path.Combine(fixture, "assets", "practice-words.txt"), before = File.ReadAllText(path);
            Reject(() => session.ChangeWords(new[] { "一", "一" }), "Duplicate accepted");
            Reject(() => session.ChangeWords(new[] { "龘" }), "Unavailable template accepted");
            Assert(File.ReadAllText(path) == before && session.Result(44) == result, "Invalid change modified lesson");
            session.Accept(new Sample(0, 200, 1250, 21250, 500));
            Reject(() => session.ChangeWords(original), "Pending writing silently discarded");
            Assert(session.PendingCount == 1 && File.ReadAllText(path) == before, "Pending change altered disk or samples");
            session.ClearSelected();
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Reject(() => session.ChangeWords(original), "Locked file unexpectedly saved");
            Assert(File.ReadAllText(path) == before && session.Words[0] == original.Last(), "Failed write changed disk or session");
            string[] applied = null;
            using (var dialog = new PracticeWordsForm(available, original, words => applied = words))
            {
                dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-20000, -20000);
                dialog.Show(); Application.DoEvents();
                var combos = Controls(dialog).OfType<ComboBox>().OrderBy(c => c.TabIndex).ToArray();
                var first = combos.Single(c => c.AccessibleName == "位置 1 的練習字");
                var second = combos.Single(c => c.AccessibleName == "位置 2 的練習字");
                first.SelectedItem = original[1];
                Assert((string)second.SelectedItem == original[0] && dialog.SelectedWords.Distinct().Count() == 12, "Duplicate selection did not swap positions");
                var number = Controls(dialog).OfType<NumericUpDown>().Single(); number.Value = 3;
                Assert(dialog.SelectedWords.Length == 3 && combos.Count(c => c.Enabled) == 3, "Count does not control enabled positions");
                Controls(dialog).OfType<Button>().Single(b => b.Text == "儲存並套用").PerformClick();
                Assert(applied != null && applied.Length == 3 && applied[0] == original[1], "Save did not apply selected order");
            }
            session.ChangeWords(new[] { "明", "林", "好" });
            Assert(session.Words.Length == 3 && new PracticeSession(fixture, null, false).Words.SequenceEqual(session.Words), "New word selection lost on restart");
            Reject(() => TemplateLibrary.ImportWords(fixture, "山"), "Missing offline source ignored");
            string source = Path.Combine(fixture, "external", "makemeahanzi"); Directory.CreateDirectory(source);
            foreach (string file in new[] { "graphics.txt", "dictionary.txt" })
                File.Copy(Path.Combine(AppPaths.Root, "external", "makemeahanzi", file), Path.Combine(source, file));
            before = File.ReadAllText(path);
            Reject(() => TemplateLibrary.ImportWords(fixture, "山龘"), "Missing or >16 stroke character accepted");
            Assert(!File.Exists(Path.Combine(fixture, "assets", "hanzi", "山.json")), "Failed batch left partial import");
            var expanded = TemplateLibrary.ImportWords(fixture, "山水火");
            Assert(expanded.Length == 25 && new[] { "山", "水", "火" }.All(expanded.Contains), "New characters not available");
            Assert(TemplateLibrary.ImportWords(fixture, "山水火").Length == 25, "Import duplicated existing characters");
            foreach (string word in new[] { "山", "水", "火" })
            {
                Assert(TemplateLibrary.Load(fixture, word).StrokeList.Count > 0, "Imported strokes missing");
                Assert(!TemplateLibrary.ReadImported(fixture, word).taiwanStrokeOrderVerified, "Import falsely claims Taiwan verification");
            }
            Assert(File.ReadAllText(path) == before, "Library import unexpectedly changed current lesson");
            using (var dialog = new PracticeWordsForm(available, original, words => applied = words, text => TemplateLibrary.ImportWords(fixture, text)))
            {
                dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-20000, -20000);
                dialog.Show(); Application.DoEvents();
                Controls(dialog).OfType<TextBox>().Single(c => c.AccessibleName == "要新增的中文字").Text = "山水火";
                var importButton = Controls(dialog).OfType<Button>().Single(b => b.Text == "匯入字庫"); importButton.PerformClick();
                var timeout = DateTime.UtcNow.AddSeconds(10);
                while (!importButton.Enabled && DateTime.UtcNow < timeout) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                Assert(importButton.Enabled && Controls(dialog).OfType<ComboBox>().All(c => c.Items.Contains("山")), "UI import failed to refresh choices");
                var first = Controls(dialog).OfType<ComboBox>().Single(c => c.AccessibleName == "位置 1 的練習字"); first.SelectedItem = "山";
                Assert(dialog.SelectedWords[0] == "山", "Cannot select newly imported word");
                var input = Controls(dialog).OfType<TextBox>().Single(c => c.AccessibleName == "要新增的中文字");
                var arrange = Controls(dialog).OfType<Button>().Single(b => b.Text == "依輸入排列");
                input.Text = "水 山 火"; arrange.PerformClick();
                Assert(dialog.SelectedWords.SequenceEqual(new[] { "水", "山", "火" }), "Pasted words did not retain order");
                foreach (string invalid in new[] { "水水", "龘", new string('水', 13) })
                {
                    input.Text = invalid; arrange.PerformClick();
                    Assert(dialog.SelectedWords.SequenceEqual(new[] { "水", "山", "火" }), "Invalid arrangement changed the draft");
                }
                Controls(dialog).OfType<Button>().Single(b => b.Text == "還原本次編輯").PerformClick();
                Assert(dialog.SelectedWords.SequenceEqual(original) && first.Items.Contains("山"), "Restore lost original order or newly imported word");
            }
            foreach (int width in new[] { 900, 780 })
            using (var dialog = new PracticeWordsForm(available, original, words => { }))
            {
                dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-20000, -20000);
                dialog.ClientSize = new Size(width, 540); dialog.Show(); Application.DoEvents();
                foreach (var combo in Controls(dialog).OfType<ComboBox>()) Assert(combo.Width > 50 && combo.Height > 20, "Choice clipped at small size");
                using (var image = new Bitmap(dialog.Width, dialog.Height))
                { dialog.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(AppPaths.Root, "tests", "output", "practice-words-dialog-" + width + ".png")); }
            }
            Console.WriteLine("PASS: selection, swap/count, save/restart, results/coins, write protection, offline import, pasted order, invalid draft and restore; isolated fixture " + fixture);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
