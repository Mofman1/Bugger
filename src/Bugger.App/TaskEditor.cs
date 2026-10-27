using Bugger.Core;

namespace Bugger.App;

internal sealed class TaskEditor : Form
{
    private readonly TaskItem source;
    private readonly TextBox name = new() { Dock = DockStyle.Fill, MaxLength = 200 };
    private readonly ComboBox schedule = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly DateTimePicker date = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill };
    private readonly DateTimePicker start = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Dock = DockStyle.Fill };
    private readonly NumericUpDown interval = Number(1, 1439, 30);
    private readonly NumericUpDown count = Number(1, 1440, 5);
    private readonly NumericUpDown days = Number(1, 36500, 3);
    private readonly CheckBox important = new() { Text = "Important", AutoSize = true };
    private readonly CheckBox rollover = new() { Text = "Roll over daily", AutoSize = true };
    private readonly CheckBox unlimited = new() { Text = "Unlimited", AutoSize = true };
    private readonly CheckBox[] weekdays = Enumerable.Range(0, 7).Select(i => new CheckBox { Text = TaskItem.DayNames[(i + 1) % 7], AutoSize = true, Margin = new Padding(0, 2, 3, 2) }).ToArray();
    private readonly Label last = new() { AutoSize = false, Dock = DockStyle.Fill, Height = 34, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(5) };
    private readonly GroupBox weekdayBox = new() { Text = "Days", Dock = DockStyle.Fill, Height = 52 };
    public TaskItem Result { get; private set; }

    internal static NumericUpDown Number(int min, int max, int value) => new() { Minimum = min, Maximum = max, Value = value, Dock = DockStyle.Fill };
    internal static Label LabelFor(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 4, 3, 2) };
    internal static Button ButtonFor(string text, EventHandler action)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(76, 26), Margin = new Padding(3) };
        button.Click += action;
        return button;
    }

    public TaskEditor(TaskItem item, string appName, bool isNew)
    {
        source = item;
        Result = item;
        Text = appName + (isNew ? " - New task" : " - Edit task");
        Icon = ApplicationIcons.Normal;
        Font = new Font("Segoe UI", 9);
        BackColor = SystemColors.Control;
        ForeColor = Color.Black;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(424, 458);
        AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 2, RowCount = 10 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Controls.Add(layout);
        void Row(Control c, int row, int span = 2) { layout.Controls.Add(c, 0, row); layout.SetColumnSpan(c, span); }
        Row(LabelFor("Task name"), 0); Row(name, 1);
        var timing = new TableLayoutPanel { Dock = DockStyle.Fill, Height = 47, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 5, 0, 0) };
        timing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); timing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        timing.Controls.Add(LabelFor("Schedule"), 0, 0); timing.Controls.Add(LabelFor("Date (one time)"), 1, 0);
        timing.Controls.Add(schedule, 0, 1); timing.Controls.Add(date, 1, 1); Row(timing, 2);
        var dayFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(5, 0, 0, 0), WrapContents = false };
        dayFlow.Controls.AddRange(weekdays); weekdayBox.Controls.Add(dayFlow); Row(weekdayBox, 3);
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, Height = 98, ColumnCount = 2, RowCount = 4, Margin = new Padding(0) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.Controls.Add(LabelFor("First reminder"), 0, 0); fields.Controls.Add(LabelFor("Every (minutes)"), 1, 0);
        fields.Controls.Add(start, 0, 1); fields.Controls.Add(interval, 1, 1);
        fields.Controls.Add(LabelFor("Total reminders"), 0, 2); fields.Controls.Add(count, 0, 3); fields.Controls.Add(important, 1, 3); Row(fields, 4);
        var rollBox = new GroupBox { Text = "Unfinished tasks", Dock = DockStyle.Fill, Height = 76 };
        var rollFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(5, 0, 0, 0) };
        days.Dock = DockStyle.None; days.Width = 65;
        rollFlow.Controls.Add(rollover); rollFlow.SetFlowBreak(rollover, true);
        rollFlow.Controls.Add(LabelFor("Try for")); rollFlow.Controls.Add(days); rollFlow.Controls.Add(LabelFor("extra days")); rollFlow.Controls.Add(unlimited);
        rollBox.Controls.Add(rollFlow); Row(rollBox, 5); Row(last, 6);
        var note = LabelFor("Schedule edits apply at the next daily start. Current progress is kept."); note.MaximumSize = new Size(400, 0); Row(note, 7);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Height = 34 };
        var save = ButtonFor("Save task", SaveTask); var cancel = ButtonFor("Cancel", (_, _) => DialogResult = DialogResult.Cancel);
        buttons.Controls.Add(save); buttons.Controls.Add(cancel); Row(buttons, 8);
        AcceptButton = save; CancelButton = cancel;
        name.Text = item.Name; schedule.Items.AddRange(["Weekly", "One time"]); schedule.SelectedIndex = item.OneTime ? 1 : 0;
        date.Value = item.OneTimeDate; start.Value = DateTime.Today + item.StartTime;
        interval.Value = item.IntervalMinutes; count.Value = item.TotalReminders; important.Checked = item.Important;
        rollover.Checked = item.RolloverEnabled; unlimited.Checked = !item.RolloverDays.HasValue; days.Value = item.RolloverDays ?? 3;
        foreach (var check in weekdays) check.Checked = item.Weekdays.Split(' ').Contains(check.Text);
        schedule.SelectedIndexChanged += (_, _) => UpdatePreview();
        start.ValueChanged += (_, _) => UpdatePreview(); interval.ValueChanged += (_, _) => UpdatePreview(); count.ValueChanged += (_, _) => UpdatePreview();
        rollover.CheckedChanged += (_, _) => UpdatePreview(); unlimited.CheckedChanged += (_, _) => UpdatePreview();
        UpdatePreview();
    }
    private void UpdatePreview()
    {
        date.Enabled = schedule.SelectedIndex == 1; weekdayBox.Enabled = !date.Enabled;
        days.Enabled = rollover.Checked && !unlimited.Checked; unlimited.Enabled = rollover.Checked;
        var offset = ((int)count.Value - 1) * (int)interval.Value;
        var end = DateTime.Today + start.Value.TimeOfDay + TimeSpan.FromMinutes(offset);
        last.Text = offset >= 1440 ? "Overlaps next daily start. Reduce count or interval." : "Last reminder: " + end.ToString("HH:mm") + (end.Date > DateTime.Today ? " next day" : ", same day");
        last.ForeColor = offset >= 1440 ? Color.DarkRed : Color.Black;
    }
    private void SaveTask(object? sender, EventArgs e)
    {
        var copy = source.Copy();
        copy.Name = name.Text.Trim(); copy.OneTime = schedule.SelectedIndex == 1; copy.OneTimeDate = date.Value.Date;
        copy.Weekdays = string.Join(" ", weekdays.Where(c => c.Checked).Select(c => c.Text));
        copy.StartTime = new TimeSpan(start.Value.Hour, start.Value.Minute, 0); copy.IntervalMinutes = (int)interval.Value;
        copy.TotalReminders = (int)count.Value; copy.Important = important.Checked; copy.RolloverEnabled = rollover.Checked;
        copy.RolloverDays = unlimited.Checked ? null : (int)days.Value;
        try { copy.Validate(); }
        catch (ArgumentException error) { MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        Result = copy; DialogResult = DialogResult.OK;
    }
}
