using Bugger.Core;
using System.Media;

namespace Bugger.App;

internal sealed class ReminderForm : Form
{
    public string TaskId { get; }
    public DateTime? Occurrence { get; }
    private readonly bool important;
    private readonly Action<string, int?> respond;
    private bool silentClose;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 250 };
    private int remaining;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000; return cp; } }

    public ReminderForm(TaskItem task, AppSettings settings, Action<string, int?> callback)
    {
        TaskId = task.Id; Occurrence = task.OccurrenceDate; important = task.Important; respond = callback;
        Text = settings.Name + (important ? " - Important" : "");
        Icon = ApplicationIcons.Alert;
        Font = new Font("Segoe UI", 9); ForeColor = Color.Black; BackColor = SystemColors.Control;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(300, 144);
        // Expand for a long task name instead of hiding the response buttons.
        var titleHeight = TextRenderer.MeasureText(task.Name, new Font(Font, FontStyle.Bold), new Size(280, int.MaxValue), TextFormatFlags.WordBreak).Height;
        ClientSize = new Size(300, 144 + Math.Max(0, titleHeight - 18));
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), RowCount = 4, ColumnCount = 1 };
        var title = new Label { Text = task.Name, AutoSize = true, MaximumSize = new Size(280, 0), Font = new Font(Font, FontStyle.Bold), ForeColor = Color.Black };
        content.Controls.Add(title);
        var extra = Math.Max(0, (task.OccurrenceDate!.Value - task.OriginalDueDate!.Value).Days);
        content.Controls.Add(TaskEditor.LabelFor($"Reminder {task.RemindersShown} of {task.ActiveTotal}" + (extra > 0 ? $" · rolled over {extra} day(s)" : "")));
        content.Controls.Add(TaskEditor.LabelFor(important ? "Stays open until Done or Snooze" : $"Closes after {settings.PopupSeconds} seconds"));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 34, WrapContents = false };
        var durations = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 72, Margin = new Padding(3, 5, 0, 0) };
        durations.Items.AddRange(["15 min", "30 min", "1 hour"]); durations.SelectedIndex = 0;
        buttons.Controls.Add(TaskEditor.ButtonFor("Done", (_, _) => Finish(null)));
        buttons.Controls.Add(TaskEditor.ButtonFor("Snooze", (_, _) => Finish(new[] { 15, 30, 60 }[durations.SelectedIndex])));
        buttons.Controls.Add(durations); content.Controls.Add(buttons); Controls.Add(content);
        remaining = settings.PopupSeconds * 4;
        timer.Tick += (_, _) =>
        {
            if (important || RectangleToScreen(ClientRectangle).Contains(Cursor.Position)) return;
            if (--remaining <= 0) CloseSilently();
        };
        Shown += (_, _) => { timer.Start(); if (important) SystemSounds.Exclamation.Play(); };
        FormClosing += (_, _) => { if (!silentClose && important) respond(TaskId, 15); };
        FormClosed += (_, _) => timer.Dispose();
    }
    private void Finish(int? minutes) { respond(TaskId, minutes); CloseSilently(); }
    public void CloseSilently() { silentClose = true; Close(); }
}
