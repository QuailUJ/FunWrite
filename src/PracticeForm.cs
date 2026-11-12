using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace FunWrite
{
    public sealed class PracticeForm : Form
    {
        sealed class Hit { public RectangleF Area; public Action Action; }
        readonly PracticeSession session;
        readonly List<Hit> hits = new List<Hit>();
        readonly Font small = new Font("Microsoft JhengHei", 12), normal = new Font("Microsoft JhengHei", 17),
            medium = new Font("Microsoft JhengHei", 26), large = new Font("Microsoft JhengHei", 42, FontStyle.Bold);
        readonly Timer animation = new Timer { Interval = 30 };
        readonly SoundPlayer sound = new SoundPlayer();
        TabletDevice tablet;
        string deviceStatus, notice = "";
        bool connecting, editingWords, showOrder, forceReminder = true;
        int animationMode, forceSide;
        double progress = 1;
        long forceSince, lastForceSound;
        float scale = 1, offsetX, offsetY;

        public PracticeSession Session { get { return session; } }
        public bool IsConnecting { get { return connecting; } }
        public PracticeForm(PracticeSession session)
        {
            this.session = session;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
            DoubleBuffered = true; BackColor = Color.FromArgb(76, 184, 210); Opacity = 1;
            Text = "FunWrite 2.1 — 整筆軌跡與部件結構";
            FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(960, 540); KeyPreview = true;
            deviceStatus = AppPaths.Preview ? "預覽模式：未連接寫字板" : "準備連接寫字板…";
            session.Changed += RefreshSession;
            session.DemoRequested += Demonstrate;
            animation.Tick += delegate { progress = Math.Min(1, progress + .025); if (progress >= 1) animation.Stop(); Invalidate(); };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Everything is initialized before showing. Device calls begin after the first frame.
            if (!AppPaths.Preview) BeginInvoke(new Action(ConnectTablet));
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            using (var background = new LinearGradientBrush(ClientRectangle, Color.FromArgb(91, 204, 225), Color.FromArgb(19, 103, 149), 90))
                e.Graphics.FillRectangle(background, ClientRectangle);
            scale = Math.Min(ClientSize.Width / 1860f, ClientSize.Height / 960f);
            offsetX = (ClientSize.Width - 1860 * scale) / 2; offsetY = (ClientSize.Height - 960 * scale) / 2;
            var state = e.Graphics.Save();
            e.Graphics.TranslateTransform(offsetX, offsetY); e.Graphics.ScaleTransform(scale, scale);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; hits.Clear();
            DrawPractice(e.Graphics); e.Graphics.Restore(state);
        }

        void DrawPractice(Graphics g)
        {
            int word = session.SelectedSlot / 4;
            var result = session.Result(session.SelectedSlot);
            var evaluation = result == null ? null : result.Evaluation;
            var reference = session.Templates[word];
            TextAt(g, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), normal, Color.DarkGreen, 100, 28);
            TextAt(g, "FunWrite 2.1", large, Color.DarkGreen, 490, 6);
            TextAt(g, "範例字：「" + session.Words[word] + "」", normal, Color.Black, 30, 110);
            TextAt(g, "字形", normal, Color.Yellow, 220, 143);
            TextAt(g, evaluation == null ? "—" : Math.Floor(evaluation.Score) + " %", large, Color.White, 310, 118);
            if (evaluation != null)
            {
                Color verdict = evaluation.Passed ? Color.DarkGreen : Color.Red;
                TextAt(g, evaluation.Passed ? "書寫通過" : "書寫未通過", normal, verdict, 220, 245);
                using (var brush = new SolidBrush(verdict))
                    g.DrawString(evaluation.Feedback, small, brush, new RectangleF(215, 292, 245, 100));
                TextAt(g, "點此查看完整說明", small, Color.White, 215, 392);
                hits.Add(new Hit { Area = new RectangleF(210, 245, 250, 160), Action = ShowFeedback });
                TextAt(g, evaluation.Speed < 0 ? "速度資料不足" : "速度 " + evaluation.Speed.ToString("0") + " mm/s", medium, Color.White, 790, 140);
                string speed = evaluation.Speed < 0 ? "" : evaluation.Speed <= 5 ? "注意：速度很慢" : evaluation.Speed <= 10 ? "速度還可以" : "速度很快";
                TextAt(g, speed, normal, Color.White, 790, 240);
                string pressure = evaluation.MeanPressure < 0 ? "力量資料不足" : evaluation.MeanPressure < 550 ? "注意：力量太小" : evaluation.MeanPressure > 850 ? "注意：力量太大" : "力量適中";
                TextAt(g, pressure, normal, evaluation.MeanPressure >= 550 && evaluation.MeanPressure <= 850 ? Color.White : Color.OrangeRed, 790, 285);
            }
            var top = new RectangleF(480, 80, 280, 280);
            GlyphRenderer.Paper(g, top);
            GlyphRenderer.Draw(g, reference, top, ZColor.GetColor, animationMode == 1 ? progress : 1, showOrder);
            var handBox = new RectangleF(285, 430, 280, 280);
            var comparison = new RectangleF(680, 430, 280, 280);
            var hand = evaluation == null ? session.Pending : evaluation.Handwriting;
            TextAt(g, session.PendingCount > 0 ? "正在收集筆跡" : "手寫字", normal, Color.White, 285, 398);
            TextAt(g, "範例比對", normal, Color.White, 680, 398);
            GlyphRenderer.Paper(g, handBox, session.PendingCount > 0); GlyphRenderer.Paper(g, comparison);
            GlyphRenderer.Draw(g, hand, handBox, i => Color.DarkGray, animationMode == 2 ? progress : 1, showOrder);
            if (animationMode == 3)
                GlyphRenderer.Draw(g, hand, comparison, i => Color.LightGray, progress, showOrder);
            GlyphRenderer.Draw(g, reference, comparison, i => evaluation == null ? Color.DarkGray :
                evaluation.Shape.Mapping[2, i + 1] >= ShapeScorer.FeedbackThreshold ? Color.Green : Color.Red,
                animationMode == 3 ? progress : 1, showOrder);
            Button(g, new RectangleF(205, 685, 78, 38), "重播", () => Animate(2));
            Button(g, new RectangleF(600, 685, 78, 38), "比對", () => Animate(3));
            TextAt(g, "本次已獲得 " + session.Coins + " 枚金幣", normal, Color.White, 300, 740);
            TextAt(g, "紅色：字形或結構需調整", small, Color.Red, 680, 730);
            DrawGrid(g);
            Button(g, new RectangleF(1500, 65, 310, 45), "練習字與位置（F2）", EditWords);
            TextAt(g, "1. 點選範例可示範筆順\n\n2. 在對應字格書寫\n\n3. 完成後按評分\n\n字形分數與筆順判定分開；\n筆順錯或中途提筆不通過。", normal, Color.Yellow, 1500, 130);
            TextAt(g, deviceStatus, small, Color.White, 1500, 480);
            using (var brush = new SolidBrush(Color.White)) g.DrawString(notice, small, brush, new RectangleF(1500, 515, 320, 55));
            Button(g, new RectangleF(1500, 585, 310, 40), connecting ? "正在連接…" : "重新連接寫字板", ConnectTablet);
            Button(g, new RectangleF(1500, 635, 310, 40), forceReminder ? "力量聲音提醒：開" : "力量聲音提醒：關", () => { forceReminder = !forceReminder; Invalidate(); });
            Button(g, new RectangleF(1500, 685, 150, 40), showOrder ? "隱藏筆序" : "顯示筆序", () => { showOrder = !showOrder; Invalidate(); });
            Button(g, new RectangleF(1660, 685, 150, 40), "讀取筆跡 Ctrl+O", OpenRecord);
            Button(g, new RectangleF(1500, 750, 310, 45), "評分（Enter）", Score);
            Button(g, new RectangleF(1500, 805, 310, 40), "重寫此格／清除", () => { animation.Stop(); session.ClearSelected(); });
            Button(g, new RectangleF(1500, 860, 310, 40), "離開程式（Esc）", Close);
            using (var brush = new SolidBrush(Color.White)) g.DrawString(session.Status, normal, brush, new RectangleF(60, 865, 900, 65));
            TextAt(g, "筆順依目前範例；匯入字尚未逐字核對台灣標準。", small, Color.White, 1030, 915);
        }

        void DrawGrid(Graphics g)
        {
            TextAt(g, "請點選範例學習筆順，每字有四個練習格", normal, Color.Red, 1020, 28);
            for (int word = 0; word < session.Words.Length; word++)
            {
                int current = word;
                float x = 1030 + word % 6 * 72, y = word < 6 ? 110 : 520;
                Button(g, new RectangleF(x + 6, y - 35, 48, 25), "示範", () => session.Demonstrate(current));
                var box = new RectangleF(x, y, 60, 60);
                using (var brush = new SolidBrush(Color.White)) g.FillRectangle(brush, box);
                GlyphRenderer.Draw(g, session.Templates[word], box, i => Color.Black);
                hits.Add(new Hit { Area = box, Action = () => session.Demonstrate(current) });
                for (int row = 0; row < 4; row++)
                {
                    int slot = word * 4 + row;
                    var rect = new RectangleF(x, y + 78 + row * 78, 60, 60);
                    var item = session.Result(slot);
                    GlyphRenderer.Paper(g, rect);
                    if (item != null)
                    {
                        if (item.Evaluation.Passed) using (var brush = new SolidBrush(Color.LightGoldenrodYellow)) g.FillRectangle(brush, rect);
                        GlyphRenderer.Draw(g, item.Evaluation.Handwriting, rect, i => Color.DarkBlue);
                    }
                    if (slot == session.SelectedSlot) using (var pen = new Pen(Color.Yellow, 3)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    hits.Add(new Hit { Area = rect, Action = () => session.Select(slot) });
                }
            }
        }

        void Button(Graphics g, RectangleF rect, string text, Action action)
        {
            using (var fill = new SolidBrush(Color.FromArgb(222, 245, 246))) g.FillRectangle(fill, rect);
            using (var pen = new Pen(Color.White, 2)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(text, small, Brushes.DarkBlue, rect, format);
            hits.Add(new Hit { Area = rect, Action = action });
        }
        static void TextAt(Graphics g, string text, Font font, Color color, float x, float y)
        { using (var brush = new SolidBrush(color)) g.DrawString(text, font, brush, x, y); }
        PointF Logical(Point point) { return new PointF((point.X - offsetX) / scale, (point.Y - offsetY) / scale); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e); var point = Logical(e.Location);
            foreach (var hit in hits.ToArray()) if (hit.Area.Contains(point)) { try { hit.Action(); } catch (Exception ex) { notice = ex.Message; Invalidate(); } return; }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); var point = Logical(e.Location);
            Cursor = hits.Exists(h => h.Area.Contains(point)) ? Cursors.Hand : Cursors.Default;
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            try
            {
                if (e.KeyData == Keys.F2) EditWords();
                else if (e.KeyData == (Keys.Control | Keys.O)) OpenRecord();
                else if (e.KeyCode == Keys.Enter) Score();
                else if (e.KeyCode == Keys.Escape) Close();
                else if (e.KeyCode == Keys.Delete) session.ClearSelected();
            }
            catch (Exception ex) { notice = ex.Message; Invalidate(); }
        }

        void RefreshSession() { notice = ""; Invalidate(); }
        void Demonstrate(int word) { Animate(1); }
        void Animate(int mode) { animationMode = mode; progress = 0; animation.Start(); Invalidate(); }
        void Score()
        {
            var result = session.ScorePending();
            if (result != null) Play(result.Evaluation.Passed ? "coin.wav" : "scoring.wav");
        }
        void ShowFeedback()
        {
            var result = session.Result(session.SelectedSlot);
            if (result != null) MessageBox.Show(this, result.Evaluation.Feedback, "筆跡檢查詳細結果");
        }
        void OpenRecord()
        {
            if (session.PendingCount > 0) { notice = "請先評分或清除目前筆跡，再讀取舊筆跡。"; Invalidate(); return; }
            using (var dialog = new OpenFileDialog { InitialDirectory = Path.Combine(AppPaths.Root, "records"), Filter = "筆跡紀錄 (*.txt)|*.txt" })
                if (dialog.ShowDialog(this) == DialogResult.OK) session.Review(dialog.FileName);
        }

        void EditWords()
        {
            if (session.PendingCount > 0) { notice = "請先評分或清除目前筆跡，再更改練習字。"; Invalidate(); return; }
            animation.Stop(); animationMode = 0; progress = 1; editingWords = true;
            try
            {
                using (var dialog = new PracticeWordsForm(TemplateLibrary.AvailableWords(AppPaths.Root), session.Words, session.ChangeWords,
                    text => TemplateLibrary.ImportWords(AppPaths.Root, text)))
                    dialog.ShowDialog(this);
            }
            finally { editingWords = false; Invalidate(); }
        }

        async void ConnectTablet()
        {
            if (connecting || AppPaths.Preview || IsDisposed) return;
            connecting = true; deviceStatus = "正在連接寫字板…"; Invalidate();
            if (tablet == null) { tablet = new TabletDevice(); tablet.SampleReceived += Receive; }
            try { await tablet.ConnectAsync(); if (!IsDisposed) deviceStatus = "寫字板已連線"; }
            catch (Exception ex) { if (!IsDisposed) { deviceStatus = "寫字板未連線"; notice = ex.Message; } }
            finally { connecting = false; if (!IsDisposed) Invalidate(); }
        }
        void Receive(Sample sample)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(() => { if (!IsDisposed) Feed(sample); })); }
            catch (InvalidOperationException) { }
        }
        public void Feed(Sample sample)
        {
            if (editingWords) return;
            try
            {
                int before = session.Coins; session.Accept(sample);
                if (sample.Pressure > 0 && session.PendingCount > 0) { animation.Stop(); animationMode = 0; }
                if (session.Coins > before) Play("coin.wav");
                int side = sample.Pressure == 0 ? 0 : sample.Pressure < 550 ? -1 : sample.Pressure > 850 ? 1 : 0;
                if (side != forceSide) { forceSide = side; forceSince = sample.Time; }
                if (forceReminder && side != 0 && sample.Time - forceSince > 1500 && sample.Time - lastForceSound > 3000)
                { lastForceSound = sample.Time; Play(side < 0 ? "力量太小哦.wav" : "太用力哦.wav"); }
            }
            catch (Exception ex) { notice = ex.Message; Invalidate(); }
        }
        void Play(string name)
        {
            if (AppPaths.Preview) return;
            string path = Path.Combine(AppPaths.Root, "assets", "wave", name);
            if (!File.Exists(path)) return;
            try { sound.SoundLocation = path; sound.Play(); } catch (InvalidOperationException) { }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                session.Changed -= RefreshSession; session.DemoRequested -= Demonstrate;
                animation.Dispose(); if (tablet != null) tablet.Dispose();
                sound.Dispose(); small.Dispose(); normal.Dispose(); medium.Dispose(); large.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
