using Bugger.Core;

namespace Bugger.App;

internal sealed class MainForm : Form
{
    private readonly AppSettings settings;
    private readonly TaskStore store = new(Path.Combine(AppContext.BaseDirectory, "tasks.csv"));
    private readonly List<TaskItem> tasks;
    private readonly TreeView list = new() { Name = "TaskGroups", Dock = DockStyle.Fill, BackColor = Color.White, ForeColor = Color.Black, HideSelection = false, FullRowSelect = true, ShowLines = false, ShowRootLines = true, ShowPlusMinus = true, Indent = 16, ItemHeight = 24 };
    private readonly ImageList groupImages = new() { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Label details = new() { AutoSize = true, ForeColor = Color.Black, Padding = new Padding(0, 2, 0, 0), MaximumSize = new Size(202, 0) };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 24, BorderStyle = BorderStyle.Fixed3D, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button done;
    private readonly NotifyIcon tray;
    private readonly ToolStripMenuItem pause;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly Dictionary<string, ReminderForm> popups = [];
    private bool quitting, ticking, storageFailed, refreshingTree;
    private string viewSignature = "";
    private CommandPipe? commandPipe;
    private TaskItem? Selected => list.SelectedNode?.Tag as TaskItem;
    internal bool HasAttention { get; private set; }

    public MainForm()
    {
        settings = AppSettings.Load(); tasks = store.Load();
        Text = settings.Name + " - All tasks"; Icon = ApplicationIcons.Normal; Font = new Font("Segoe UI", 9); ForeColor = Color.Black;
        ClientSize = new Size(480, 300); MinimumSize = new Size(450, 310); AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        var menu = new MenuStrip { Renderer = new ToolStripSystemRenderer() };
        var taskMenu = new ToolStripMenuItem("Tasks");
        taskMenu.DropDownItems.Add("New task...", null, (_, _) => Edit(null));
        taskMenu.DropDownItems.Add("Edit task...", null, (_, _) => Edit(Selected));
        pause = new ToolStripMenuItem("Pause task", null, (_, _) => Pause()); taskMenu.DropDownItems.Add(pause);
        taskMenu.DropDownItems.Add("Duplicate task...", null, (_, _) => Duplicate());
        taskMenu.DropDownItems.Add("Delete task...", null, (_, _) => Delete());
        taskMenu.DropDownItems.Add(new ToolStripSeparator());
        taskMenu.DropDownItems.Add("Expand all groups", null, (_, _) => list.ExpandAll());
        taskMenu.DropDownItems.Add("Collapse all groups", null, (_, _) => list.CollapseAll());
        taskMenu.DropDownItems.Add(new ToolStripSeparator()); taskMenu.DropDownItems.Add("Exit " + settings.Name, null, (_, _) => Quit());
        taskMenu.DropDownOpening += (_, _) => { var selected = Selected != null; for (var i = 1; i <= 4; i++) taskMenu.DropDownItems[i].Enabled = selected; pause.Text = Selected?.Enabled == false ? "Resume task" : "Pause task"; };
        var settingsMenu = new ToolStripMenuItem("Settings"); settingsMenu.DropDownItems.Add("Reminder settings...", null, (_, _) => Settings());
        var help = new ToolStripMenuItem("Help"); help.DropDownItems.Add("About", null, (_, _) => MessageBox.Show(this, settings.Name + "\nPortable task reminders\n\nTasks and progress: tasks.csv\nSettings: settings.txt\n\nClosing this window keeps reminders running in the tray. Use Exit to quit.", settings.Name));
        menu.Items.AddRange([taskMenu, settingsMenu, help]); MainMenuStrip = menu;
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 2, RowCount = 2 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill };
        toolbar.Controls.Add(TaskEditor.ButtonFor("New task...", (_, _) => Edit(null))); toolbar.Controls.Add(TaskEditor.ButtonFor("Edit...", (_, _) => Edit(Selected)));
        content.Controls.Add(toolbar, 0, 0); content.SetColumnSpan(toolbar, 2);
        content.Controls.Add(list, 0, 1);
        groupImages.Images.Add("folder", ApplicationIcons.Folder());
        // Transparent task image preserves folder indentation without adding decorative task icons.
        groupImages.Images.Add("task", new Bitmap(16, 16));
        list.ImageList = groupImages;
        var detailPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
        done = TaskEditor.ButtonFor("Mark done", (_, _) => Complete()); done.Dock = DockStyle.Bottom;
        var detailScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        detailScroll.Controls.Add(details);
        detailScroll.Resize += (_, _) => details.MaximumSize = new Size(Math.Max(80, detailScroll.ClientSize.Width - 18), 0);
        detailPanel.Controls.Add(detailScroll); detailPanel.Controls.Add(done); content.Controls.Add(detailPanel, 1, 1);
        Controls.Add(content); Controls.Add(status); Controls.Add(menu);
        list.AfterSelect += (_, _) => ShowDetails(); list.NodeMouseDoubleClick += (_, e) => { if (e.Node.Tag is TaskItem) Edit(Selected); };
        list.AfterExpand += (_, e) => RememberGroup(e.Node, true);
        list.AfterCollapse += (_, e) => RememberGroup(e.Node, false);
        var trayMenu = new ContextMenuStrip { Renderer = new ToolStripSystemRenderer() };
        trayMenu.Items.Add("Open " + settings.Name, null, (_, _) => Open()); trayMenu.Items.Add("New task...", null, (_, _) => { Open(); Edit(null); });
        trayMenu.Items.Add(new ToolStripSeparator()); trayMenu.Items.Add("Exit " + settings.Name, null, (_, _) => Quit());
        tray = new NotifyIcon { Text = settings.Name.Length > 63 ? settings.Name[..63] : settings.Name, Icon = ApplicationIcons.Normal, Visible = true, ContextMenuStrip = trayMenu };
        tray.DoubleClick += (_, _) => Open();
        FormClosing += (_, e) => { if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        FormClosed += (_, _) => { commandPipe?.Dispose(); timer.Dispose(); foreach (var p in popups.Values.ToArray()) p.CloseSilently(); tray.Visible = false; tray.Dispose(); groupImages.Dispose(); };
        Shown += (_, _) => { commandPipe = new CommandPipe(this); if (!Persist()) return; timer.Start(); TickScheduler(); };
        timer.Tick += (_, _) => TickScheduler(); RefreshList(true);
    }

    private void Open() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    internal CommandResult ExecuteCommand(string[] args)
    {
        if (storageFailed) return new(false, "Storage is unavailable. Fix storage access and retry in Settings.");
        var result = TaskCommands.Execute(args, tasks, updated => store.Save(updated), DateTime.Now);
        if (result.Success)
        {
            foreach (var popup in popups.Values.ToArray())
            {
                var task = tasks.Find(t => t.Id == popup.TaskId);
                if (task == null || !task.Enabled || task.Status != "Pending" || (args[0].Equals("edit", StringComparison.OrdinalIgnoreCase) && result.Output == task.Id)) popup.CloseSilently();
            }
            RefreshList(true); TickScheduler(); UpdateTray();
        }
        return result;
    }
    private bool Persist()
    {
        try { store.Save(tasks); settings.Save(); storageFailed = false; return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            timer.Stop(); storageFailed = true; status.Text = "Storage error. Reminders paused.";
            MessageBox.Show(this, "Could not save files beside the application. Reminders are paused to protect your progress.\n\n" + e.Message + "\n\nMove the application to a writable folder, or fix access and use Settings to retry.", settings.Name, MessageBoxButtons.OK, MessageBoxIcon.Error); return false;
        }
    }
    private void TickScheduler()
    {
        if (ticking || storageFailed) return;
        ticking = true;
        try
        {
            var now = DateTime.Now; var changed = false;
            foreach (var task in tasks) changed |= Scheduler.Advance(task, now);
            foreach (var pair in popups.ToArray())
            {
                var task = tasks.Find(t => t.Id == pair.Key);
                if (task == null || !task.Enabled || task.Status != "Pending" || task.OccurrenceDate != pair.Value.Occurrence || task.WindowEnd <= now) pair.Value.CloseSilently();
            }
            if (changed && !Persist()) return;
            foreach (var task in tasks.Where(t => Scheduler.Due(t, now)).OrderBy(t => t.NextReminderAt).ThenBy(t => t.Id).ToArray())
            {
                if (popups.Count >= 3) break;
                if (popups.ContainsKey(task.Id)) continue;
                var before = task.Copy();
                Scheduler.Shown(task, now);
                if (!Persist()) { tasks[tasks.IndexOf(task)] = before; return; }
                var popup = new ReminderForm(task, settings, Respond);
                popups.Add(task.Id, popup);
                popup.FormClosed += (_, _) => { popups.Remove(task.Id); StackPopups(); UpdateTray(); };
                StackPopups(); popup.Show(); StackPopups();
            }
            RefreshList(changed);
            UpdateTray();
        }
        finally { ticking = false; }
    }
    private void StackPopups()
    {
        var area = Screen.PrimaryScreen!.WorkingArea; var bottom = area.Bottom - 8;
        foreach (var popup in popups.Values.Reverse()) { popup.Location = new Point(area.Right - popup.Width - 8, bottom - popup.Height); bottom -= popup.Height + 8; }
    }
    private void Respond(string id, int? minutes)
    {
        var task = tasks.Find(t => t.Id == id); if (task == null) return;
        Scheduler.Advance(task, DateTime.Now);
        if (minutes.HasValue) Scheduler.Snooze(task, DateTime.Now, minutes.Value); else Scheduler.Done(task, DateTime.Now);
        Persist(); RefreshList(true); UpdateTray();
    }
    private void Complete()
    {
        if (Selected is not { Status: "Pending" } task) return;
        Scheduler.Done(task, DateTime.Now); if (popups.TryGetValue(task.Id, out var popup)) popup.CloseSilently(); Persist(); RefreshList(true); UpdateTray();
    }
    private void Edit(TaskItem? existing)
    {
        using var editor = new TaskEditor(existing ?? new TaskItem(), settings.Name, existing == null);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        if (existing != null) { if (popups.TryGetValue(existing.Id, out var popup)) popup.CloseSilently(); tasks[tasks.IndexOf(existing)] = editor.Result; }
        else tasks.Add(editor.Result);
        Scheduler.Advance(editor.Result, DateTime.Now);
        ExpandTaskGroup(editor.Result);
        Persist(); RefreshList(true, editor.Result.Id);
    }
    private void Duplicate()
    {
        if (Selected is not { } selected) return;
        var copy = new TaskItem { Name = selected.Name + " (copy)", OneTime = selected.OneTime, OneTimeDate = selected.OneTimeDate, Weekdays = selected.Weekdays, StartTime = selected.StartTime, IntervalMinutes = selected.IntervalMinutes, TotalReminders = selected.TotalReminders, Important = selected.Important, RolloverEnabled = selected.RolloverEnabled, RolloverDays = selected.RolloverDays };
        using var editor = new TaskEditor(copy, settings.Name, true);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            tasks.Add(editor.Result); Scheduler.Advance(editor.Result, DateTime.Now);
            ExpandTaskGroup(editor.Result);
            Persist(); RefreshList(true, editor.Result.Id);
        }
    }
    private void Pause()
    {
        if (Selected is not { } task) return;
        task.Enabled = !task.Enabled; if (popups.TryGetValue(task.Id, out var popup)) popup.CloseSilently(); ExpandTaskGroup(task); Persist(); RefreshList(true, task.Id); UpdateTray();
    }
    private void Delete()
    {
        if (Selected is not { } task || MessageBox.Show(this, "Delete '" + task.Name + "'?", settings.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (popups.TryGetValue(task.Id, out var popup)) popup.CloseSilently(); tasks.Remove(task); Persist(); RefreshList(true); UpdateTray();
    }
    private void RefreshList(bool force = false, string? selectId = null)
    {
        var now = DateTime.Now;
        var today = tasks.Where(t => TaskView.VisibleToday(t, now)).ToArray();
        var other = tasks.Except(today).ToArray();
        var signature = now.Date + string.Join("|", tasks.Select(t => $"{t.Id}:{t.Name}:{t.Status}:{t.Enabled}:{t.RemindersShown}:{t.NextReminderAt}:{TaskView.VisibleToday(t, now)}"));
        if (!force && signature == viewSignature) return;
        viewSignature = signature; var id = selectId ?? Selected?.Id;
        refreshingTree = true; list.BeginUpdate(); list.Nodes.Clear();
        TreeNode? selection = null;
        void Group(string key, string label, TaskItem[] entries, bool expanded)
        {
            var parent = new TreeNode($"{label} ({entries.Length})") { Name = key, ImageKey = "folder", SelectedImageKey = "folder", BackColor = SystemColors.Control };
            foreach (var task in entries)
            {
                var node = new TreeNode((!task.Enabled ? "Paused: " : task.Status == "Done" ? "Done: " : task.Important ? "! " : "") + task.Name) { Name = task.Id, Tag = task, ImageKey = "task", SelectedImageKey = "task" };
                parent.Nodes.Add(node);
                if (task.Id == id) selection = node;
            }
            if (entries.Length == 0) parent.Nodes.Add(new TreeNode(key == "today" ? "Nothing scheduled today" : "No other tasks") { ImageKey = "task", SelectedImageKey = "task", ForeColor = Color.DimGray });
            list.Nodes.Add(parent); if (expanded) parent.Expand();
        }
        Group("today", "Today", today, settings.TodayExpanded);
        Group("other", "Other days", other, settings.OtherDaysExpanded);
        selection ??= list.Nodes.Cast<TreeNode>().SelectMany(n => n.Nodes.Cast<TreeNode>()).FirstOrDefault(n => n.Tag is TaskItem && n.Parent!.IsExpanded);
        // Do not expand a collapsed folder just to preserve its previously selected task.
        list.SelectedNode = selection?.Parent?.IsExpanded == true ? selection : null;
        list.EndUpdate(); refreshingTree = false; ShowDetails();
        if (!storageFailed) status.Text = $"{tasks.Count} tasks saved · {today.Length} scheduled today · {now:ddd d MMM}";
    }
    private void ShowDetails()
    {
        var t = Selected; done.Enabled = t?.Enabled == true && t.Status == "Pending";
        if (t == null) { details.Text = tasks.Count == 0 ? "No tasks yet.\n\nChoose New task to begin." : $"{tasks.Count} tasks saved.\n\nSelect a task to see its schedule. Expand a group with its plus button."; return; }
        var next = TaskView.NextScheduledStart(t, DateTime.Now);
        var state = !t.Enabled ? "Paused" : t.Status == "Done" ? "Done at " + t.CompletedAt?.ToString("HH:mm") : t.Status == "Pending" ? $"{t.RemindersShown} of {t.ActiveTotal} reminders shown" : next.HasValue ? "Next: " + next.Value.ToString("ddd d MMM, HH:mm") : "No future occurrence";
        details.Text = $"{t.Name}\n\n{t.ScheduleSummary} · {t.StartTime.ToString(@"hh\:mm")}\n{t.TotalReminders} reminders, every {t.IntervalMinutes} min\n\nRollover: {(t.RolloverEnabled ? t.RolloverDays.HasValue ? $"up to {t.RolloverDays} extra days" : "unlimited" : "off")}\n{state}";
    }
    private void Settings()
    {
        using var dialog = new Form { Text = settings.Name + " - Settings", Icon = ApplicationIcons.Normal, ClientSize = new Size(330, 170), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, Font = Font };
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var seconds = TaskEditor.Number(3, 300, settings.PopupSeconds); seconds.Dock = DockStyle.None; seconds.Width = 100;
        layout.Controls.Add(TaskEditor.LabelFor("Normal popup duration (seconds)")); layout.Controls.Add(seconds);
        layout.Controls.Add(TaskEditor.LabelFor("Name can be changed in settings.txt after exiting."));
        layout.Controls.Add(TaskEditor.ButtonFor(storageFailed ? "Save and retry" : "Save", (_, _) =>
        {
            try { settings.PopupSeconds = (int)seconds.Value; settings.Save(); if (Persist()) { timer.Start(); dialog.DialogResult = DialogResult.OK; } }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { MessageBox.Show(dialog, e.Message, settings.Name); }
        }));
        dialog.Controls.Add(layout); dialog.ShowDialog(this);
    }
    private void Quit() { if (!Persist() && MessageBox.Show(this, "Exit without saving the latest changes?", settings.Name, MessageBoxButtons.YesNo) != DialogResult.Yes) return; quitting = true; Close(); }

    private void RememberGroup(TreeNode? node, bool expanded)
    {
        if (refreshingTree || node == null || node.Parent != null) return;
        if (node.Name == "today") settings.TodayExpanded = expanded; else settings.OtherDaysExpanded = expanded;
        Persist();
    }
    private void ExpandTaskGroup(TaskItem task)
    {
        if (TaskView.VisibleToday(task, DateTime.Now)) settings.TodayExpanded = true; else settings.OtherDaysExpanded = true;
    }
    private void UpdateTray()
    {
        HasAttention = tasks.Any(t => Scheduler.NeedsAttention(t, DateTime.Now));
        tray.Icon = HasAttention ? ApplicationIcons.Alert : ApplicationIcons.Normal;
        var text = settings.Name + (HasAttention ? " - Reminder needs attention" : "");
        tray.Text = text.Length > 63 ? text[..63] : text;
    }
}
