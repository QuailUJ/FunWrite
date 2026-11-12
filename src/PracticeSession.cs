using System;
using System.Collections.Generic;
using System.IO;


namespace FunWrite
{
    public sealed class PracticeResult
    {
        public int Slot;
        public Sample[] Samples;
        public WritingEvaluation Evaluation;
        public string Path;
    }

    public sealed class PracticeSession
    {
        readonly string root;
        readonly bool saveRecords;
        readonly PracticeResult[] results = new PracticeResult[48];
        readonly List<Sample> pending = new List<Sample>();
        int pendingSlot = -1, strokeNumber;
        bool down, scorePressed;
        public string[] Words { get; private set; }
        public Stroke[] Templates { get; private set; }
        public HanziTemplate[] Metadata { get; private set; }
        public int SelectedSlot { get; private set; }
        public int Coins { get; private set; }
        public string Status { get; private set; } = "選擇範例後，在對應的寫字格練習。";
        public Stroke Pending { get { return TrajectoryChecker.ContactStroke(pending); } }
        public int PendingCount { get { return pending.Count; } }
        public event Action Changed;
        public event Action<int> DemoRequested;

        public PracticeSession(string root, string[] words = null, bool saveRecords = true)
        {
            this.root = root; this.saveRecords = saveRecords;
            Words = (string[])(words ?? TemplateLibrary.PracticeWords(root)).Clone();
            TemplateLibrary.ValidateWords(Words);
            Templates = new Stroke[Words.Length]; Metadata = new HanziTemplate[Words.Length];
            for (int i = 0; i < Words.Length; i++)
            { Templates[i] = TemplateLibrary.Load(root, Words[i]); Metadata[i] = TemplateLibrary.ReadImported(root, Words[i]); }
        }

        public PracticeResult Result(int slot) { return slot >= 0 && slot < results.Length ? results[slot] : null; }
        public void ChangeWords(string[] words)
        {
            if (pending.Count > 0) throw new InvalidOperationException("請先評分或清除目前筆跡，再更改練習字。");
            TemplateLibrary.ValidateWords(words);
            var nextWords = (string[])words.Clone();
            var templates = new Stroke[words.Length]; var metadata = new HanziTemplate[words.Length];
            for (int i = 0; i < words.Length; i++)
            { templates[i] = TemplateLibrary.Load(root, words[i]); metadata[i] = TemplateLibrary.ReadImported(root, words[i]); }
            // Persist before changing live state. A failed write leaves the current lesson intact.
            TemplateLibrary.SavePracticeWords(root, nextWords);
            var moved = new PracticeResult[48];
            string selected = Words[SelectedSlot / 4]; int selectedRow = SelectedSlot % 4;
            for (int i = 0; i < nextWords.Length; i++)
            {
                int old = Array.IndexOf(Words, nextWords[i]);
                if (old < 0) continue;
                for (int row = 0; row < 4; row++)
                { moved[i * 4 + row] = results[old * 4 + row]; if (moved[i * 4 + row] != null) moved[i * 4 + row].Slot = i * 4 + row; }
            }
            Words = nextWords; Templates = templates; Metadata = metadata;
            Array.Copy(moved, results, results.Length);
            int selectedWord = Array.IndexOf(Words, selected);
            SelectedSlot = selectedWord < 0 ? 0 : selectedWord * 4 + selectedRow;
            down = scorePressed = false; strokeNumber = 0;
            Notify("練習字與位置已儲存；每個位置仍對應原寫字板的四格。");
        }
        public static int SlotAt(float x, float y)
        {
            if (x < 0 || x >= 15000 || y < 0 || y > 22500 || (y > 10000 && y < 12500)) return -1;
            int word = (int)(x / 2500) + (y >= 12500 ? 0 : 6);
            int row = Math.Min(3, (int)(((y >= 12500 ? 22500 : 10000) - y) / 2500));
            return word * 4 + row;
        }

        public void Select(int slot)
        {
            if (slot < 0 || slot / 4 >= Words.Length) return;
            if (pending.Count > 0 && slot != pendingSlot) { Notify("請先評分或清除目前筆跡，再切換字格。"); return; }
            SelectedSlot = slot; Notify(results[slot] == null ? "在此格書寫，完成後按評分。" : "顯示這一格的評分；可以重播或重寫。");
        }

        public void Demonstrate(int word)
        {
            if (word < 0 || word >= Words.Length) return;
            Select(word * 4);
            if (SelectedSlot / 4 == word && DemoRequested != null) DemoRequested(word);
        }

        public void Accept(Sample sample)
        {
            bool pressed = sample.Pressure > 0, rising = pressed && !down;
            if (pressed != down) strokeNumber++;
            down = pressed;
            var recorded = new Sample(strokeNumber, sample.Time, sample.X, sample.Y, sample.Pressure);
            // A release belongs to the pending writing even outside the paper/grid.
            if (!pressed && pending.Count > 0 && pending[pending.Count - 1].Pressure > 0) pending.Add(recorded);
            bool scoreArea = sample.X > 15500 && sample.Y < 1000;
            if (scoreArea)
            {
                if (pressed && !scorePressed) ScorePending();
                scorePressed = pressed; return;
            }
            scorePressed = false;
            int slot = SlotAt(sample.X, sample.Y);
            if (pressed && pendingSlot >= 0 && pending.Count > 0 && pending[pending.Count - 1].Pressure > 0 && slot != pendingSlot)
            {
                pending.Add(recorded); Notify("這一筆超出字格，仍保留完整軌跡供評分。"); return;
            }
            int demo = sample.X >= 0 && sample.X < 15000 ? (int)(sample.X / 2500) : -1;
            if (demo >= 0 && ((sample.Y > 22500 && sample.Y < 25000) || (sample.Y > 10000 && sample.Y < 12500)))
            { if (rising) Demonstrate(demo + (sample.Y > 22500 ? 0 : 6)); return; }
            if (slot < 0 || slot / 4 >= Words.Length) { if (!pressed) Notify(Status); return; }
            if (!pressed)
            { if (pending.Count == 0) Select(slot); else Notify(Status); return; }
            if (results[slot] != null) { Notify("這格已有結果，請先按「重寫此格」。"); return; }
            if (pendingSlot >= 0 && pendingSlot != slot) { Notify("請在同一格完成書寫，再評分或清除。"); return; }
            pendingSlot = SelectedSlot = slot;
            pending.Add(recorded); Notify("正在收集筆跡，完成後按右下角評分。");
        }

        public PracticeResult ScorePending()
        {
            if (pending.Count == 0) { Notify("請先在字格內寫字。"); return null; }
            int slot = pendingSlot, word = slot / 4;
            var samples = TrajectoryChecker.Copy(pending);
            var evaluation = WritingScorer.Evaluate(Templates[word], samples, Metadata[word]);
            string path = saveRecords ? RecordStore.Save(root, Words[word], slot, samples) : null;
            var result = new PracticeResult { Slot = slot, Samples = samples, Evaluation = evaluation, Path = path };
            results[slot] = result; SelectedSlot = slot;
            if (evaluation.Passed) Coins++;
            pending.Clear(); pendingSlot = -1;
            Notify(evaluation.Passed ? "書寫通過，獲得一枚金幣。" : "書寫未通過；點錯誤說明可查看詳情。");
            return result;
        }

        public void ClearSelected()
        {
            if (pendingSlot >= 0 && pendingSlot != SelectedSlot) return;
            pending.Clear(); pendingSlot = -1; results[SelectedSlot] = null;
            Notify("已清除此格，可以重新書寫；已存的筆跡檔案仍保留。");
        }

        public PracticeResult Review(string path)
        {
            if (pending.Count > 0) throw new InvalidOperationException("請先評分或清除目前筆跡。");
            var parts = System.IO.Path.GetFileNameWithoutExtension(path).Split('_');
            int oldSlot;
            if (parts.Length != 5 || !int.TryParse(parts[4], out oldSlot) || oldSlot < 0 || oldSlot >= 48)
                throw new InvalidDataException("筆跡檔名須為 日期_時間_毫秒_字_格號.txt。");
            int word = Array.IndexOf(Words, parts[3]);
            if (word < 0) throw new InvalidDataException("目前字單沒有「" + parts[3] + "」，請先更換字單。");
            int slot = word * 4 + oldSlot % 4;
            var samples = TrajectoryChecker.Parse(File.ReadAllText(path));
            var result = new PracticeResult { Slot = slot, Samples = samples, Path = path,
                Evaluation = WritingScorer.Evaluate(Templates[word], samples, Metadata[word]) };
            results[slot] = result; SelectedSlot = slot; Notify("已依目前規則重算筆跡；回顧不會增加金幣。");
            return result;
        }

        void Notify(string text) { Status = text; if (Changed != null) Changed(); }
    }
}
