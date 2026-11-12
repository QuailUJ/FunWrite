using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FunWrite
{
    public sealed class PracticeWordsForm : Form
    {
        readonly ComboBox[] choices = new ComboBox[12];
        readonly string[] previous = new string[12];
        readonly NumericUpDown count = new NumericUpDown { Minimum = 1, Maximum = 12, Width = 65 };
        readonly Label message = new Label { Dock = DockStyle.Fill, AutoSize = true, ForeColor = Color.DarkRed };
        readonly Action<string[]> apply;
        bool updating;
        bool importing;
        public string[] SelectedWords { get { return choices.Take((int)count.Value).Select(c => (string)c.SelectedItem).ToArray(); } }

        public PracticeWordsForm(string[] available, string[] current, Action<string[]> apply, Func<string, string[]> import = null)
        {
            TemplateLibrary.ValidateWords(current);
            if (available.Length < current.Length || current.Any(w => !available.Contains(w)))
                throw new ArgumentException("字庫缺少目前練習字，請先檢查範例檔案。");
            this.apply = apply;
            Text = "FunWrite 2.1 — 練習字與位置";
            Font = new Font("Microsoft JhengHei", 11);
            BackColor = Color.FromArgb(222, 245, 246);
            ForeColor = Color.FromArgb(15, 55, 74);
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            MinimizeBox = MaximizeBox = false; MinimumSize = new Size(780, 510); ClientSize = new Size(900, 540);
            AutoScaleMode = AutoScaleMode.Font;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), RowCount = 6, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(layout);
            layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(820, 0), Text =
                "選擇練習字並決定位置\r\n上排 1～6、下排 7～12，由左到右對應寫字板。選到其他位置已有的字時，兩個位置會交換。", Margin = new Padding(0, 0, 0, 16) });
            var settings = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            settings.Controls.Add(new Label { Text = "練習字數", AutoSize = true, Padding = new Padding(0, 4, 8, 0) });
            count.Maximum = Math.Min(12, available.Length); count.Value = current.Length; count.AccessibleName = "練習字數"; settings.Controls.Add(count);
            var libraryCount = new Label { AutoSize = true, Padding = new Padding(12, 4, 0, 0), Text = "使用前 N 個位置；目前字庫 " + available.Length + " 字" };
            settings.Controls.Add(libraryCount);
            layout.Controls.Add(settings);
            var add = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0, 10, 0, 0) };
            add.Controls.Add(new Label { Text = "輸入練習字", AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
            var characters = new TextBox { Width = 140, AccessibleName = "要新增的中文字" };
            var importButton = new Button { Text = "匯入字庫", AutoSize = true, MinimumSize = new Size(100, 32), Enabled = import != null };
            var arrange = new Button { Text = "依輸入排列", AutoSize = true, MinimumSize = new Size(110, 32) };
            add.Controls.Add(characters); add.Controls.Add(arrange); add.Controls.Add(importButton);
            add.Controls.Add(new Label { AutoSize = true, Padding = new Padding(6, 6, 0, 0), Text = "每次最多 12 字；筆順依離線字源" });
            layout.Controls.Add(add);
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 2, Margin = new Padding(0, 14, 0, 14) };
            for (int i = 0; i < 6; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            var initial = current.Concat(available.Except(current)).Take(12).ToArray();
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                var position = new GroupBox { Text = (i < 6 ? "上排 " : "下排 ") + (i % 6 + 1), Dock = DockStyle.Fill, Padding = new Padding(8) };
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top,
                    Font = new Font("Microsoft JhengHei", 22), AccessibleName = "位置 " + (i + 1) + " 的練習字" };
                combo.Items.AddRange(available.Cast<object>().ToArray());
                previous[i] = initial[Math.Min(i, initial.Length - 1)]; combo.SelectedItem = previous[i];
                choices[i] = combo; position.Controls.Add(combo); grid.Controls.Add(position, i % 6, i / 6);
                combo.SelectedIndexChanged += delegate
                {
                    if (updating) return;
                    updating = true;
                    string chosen = (string)combo.SelectedItem;
                    for (int j = 0; j < choices.Length; j++)
                        if (j != index && choices[j].SelectedItem as string == chosen)
                        { choices[j].SelectedItem = previous[index]; previous[j] = previous[index]; break; }
                    previous[index] = chosen; updating = false; message.Text = "";
                };
            }
            layout.Controls.Add(grid); layout.Controls.Add(message);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true };
            var save = new Button { Text = "儲存並套用", AutoSize = true, MinimumSize = new Size(130, 40) };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 40) };
            var restore = new Button { Text = "還原本次編輯", AutoSize = true, MinimumSize = new Size(130, 40) };
            actions.Controls.Add(save); actions.Controls.Add(cancel); actions.Controls.Add(restore); layout.Controls.Add(actions);
            var original = (string[])current.Clone();
            Action<string[]> setDraft = words =>
            {
                var filled = words.Concat(available.Except(words)).Take(choices.Length).ToArray();
                updating = true;
                try
                {
                    for (int i = 0; i < choices.Length; i++)
                    {
                        previous[i] = filled[Math.Min(i, filled.Length - 1)];
                        choices[i].SelectedItem = previous[i];
                    }
                    count.Value = words.Length;
                    UpdateCount();
                }
                finally { updating = false; }
            };
            arrange.Click += delegate
            {
                try
                {
                    var words = characters.Text.Where(c => !char.IsWhiteSpace(c)).Select(c => c.ToString()).ToArray();
                    TemplateLibrary.ValidateWords(words);
                    var missing = words.Except(available).ToArray();
                    if (missing.Length > 0) throw new InvalidOperationException("字庫缺少「" + string.Concat(missing) + "」，請先按「匯入字庫」。");
                    setDraft(words);
                    message.ForeColor = Color.DarkGreen;
                    message.Text = "已按輸入順序排列，確認後按「儲存並套用」。";
                }
                catch (Exception ex) { message.ForeColor = Color.DarkRed; message.Text = "無法排列：" + ex.Message; }
            };
            restore.Click += delegate
            {
                setDraft(original);
                message.ForeColor = Color.DarkGreen;
                message.Text = "已還原開啟設定時的排列。";
            };
            save.Click += delegate
            {
                try { apply(SelectedWords); DialogResult = DialogResult.OK; Close(); }
                catch (Exception ex) { message.ForeColor = Color.DarkRed; message.Text = "無法套用：" + ex.Message; }
            };
            AcceptButton = save; CancelButton = cancel;
            importButton.Click += async delegate
            {
                string requested = characters.Text;
                importing = true; save.Enabled = cancel.Enabled = importButton.Enabled = arrange.Enabled = restore.Enabled = false;
                message.ForeColor = Color.DarkRed; message.Text = "正在匯入，請稍候…";
                try
                {
                    available = await Task.Run(() => import(requested));
                    count.Maximum = Math.Min(12, available.Length);
                    libraryCount.Text = "使用前 N 個位置；目前字庫 " + available.Length + " 字";
                    updating = true;
                    for (int i = 0; i < choices.Length; i++)
                    { choices[i].Items.Clear(); choices[i].Items.AddRange(available.Cast<object>().ToArray()); choices[i].SelectedItem = previous[i]; }
                    updating = false;
                    message.ForeColor = Color.DarkGreen; message.Text = "字庫已更新，可按「依輸入排列」或用下拉選單選字。";
                }
                catch (Exception ex) { message.ForeColor = Color.DarkRed; message.Text = "匯入失敗：" + ex.Message; }
                finally { updating = importing = false; save.Enabled = cancel.Enabled = importButton.Enabled = arrange.Enabled = restore.Enabled = true; }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (importing) e.Cancel = true; };
            count.ValueChanged += delegate { UpdateCount(); }; UpdateCount();
        }

        void UpdateCount()
        {
            for (int i = 0; i < choices.Length; i++) choices[i].Enabled = i < count.Value;
            message.ForeColor = Color.DarkRed;
            message.Text = "換字／減少位置後，移出的畫面結果不再顯示；已存筆跡與本次金幣保留。";
        }
    }
}
