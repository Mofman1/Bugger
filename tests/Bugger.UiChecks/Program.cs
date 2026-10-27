using Bugger.App;
using Bugger.Core;
using System.Drawing.Imaging;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var output = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(output);
        var now = DateTime.Now;
        var tasks = new List<TaskItem>();
        foreach (var name in new[] { "Unload dishwasher", "Put bins out", "Water plants", "Clean desk" })
        {
            var task = new TaskItem { Id = tasks.Count.ToString("D2"), Name = name, CreatedOn = now.Date, Weekdays = string.Join(" ", TaskItem.DayNames), StartTime = new(now.Hour, now.Minute, 0), Important = name == "Put bins out", RolloverEnabled = true, RolloverDays = 3 };
            tasks.Add(task);
        }
        tasks.Add(new TaskItem { Id = "future", Name = "Future appointment", CreatedOn = now.Date, OneTime = true, OneTimeDate = now.Date.AddDays(10) });
        var path = Path.Combine(AppContext.BaseDirectory, "tasks.csv");
        new TaskStore(path).Save(tasks);
        new AppSettings { PopupSeconds = 15 }.Save();
        using var main = new MainForm();
        using var timer = new System.Windows.Forms.Timer { Interval = 750 };
        var phase = 0;
        string? failure = null;
        static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        static IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
        static void Exit(MainForm form) => ((ToolStripMenuItem)((ToolStripMenuItem)form.MainMenuStrip!.Items[0]).DropDownItems[^1]).PerformClick();
        void Capture(Form form, string filename)
        {
            form.Refresh();
            form.Update();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.Combine(output, filename + ".png"), ImageFormat.Png);
        }
        timer.Tick += (_, _) =>
        {
            try
            {
                if (phase++ == 0)
                {
                    var reminders = Application.OpenForms.OfType<ReminderForm>().ToArray();
                    Assert(reminders.Length == 3, "Expected three visible reminders.");
                    var saved = new TaskStore(path).Load();
                    var tree = Descendants(main).OfType<TreeView>().Single();
                    Assert(tree.Nodes["today"]!.Nodes.Cast<TreeNode>().Count(n => n.Tag is TaskItem) == 4, "Today group is missing tasks.");
                    Assert(tree.Nodes["other"]!.Nodes["future"] != null, "Future task is hidden.");
                    Assert(main.HasAttention, "Tray did not select the alert icon for unsnoozed reminders.");
                    tree.Nodes["other"]!.Collapse(); Assert(!AppSettings.Load().OtherDaysExpanded, "Collapsed group was not saved.");
                    tree.Nodes["other"]!.Expand();
                    Console.WriteLine("PASS All tasks visible in two groups, collapse state persisted, and alert icon selected");
                    Assert(saved.Sum(t => t.RemindersShown) == 3, "Queued task consumed an attempt.");
                    Assert(reminders.All(f => (f.Handle != main.Handle) && f.Visible), "Reminder is not visible.");
                    Assert(reminders.Select(f => f.Bounds).All(b => Screen.PrimaryScreen!.WorkingArea.Contains(b)), "Reminder is outside the working area.");
                    for (var i = 0; i < reminders.Length; i++)
                    {
                        Capture(reminders[i], "reminder-" + i);
                        for (var j = i + 1; j < reminders.Length; j++) Assert(!reminders[i].Bounds.IntersectsWith(reminders[j].Bounds), "Reminder windows overlap.");
                    }
                    Capture(main, "grouped-tasks-rendered");
                    Console.WriteLine("PASS Three separate reminder windows, no overlap, queued attempt not consumed");
                    using var editor = new TaskEditor(saved[0], "Bugger", false);
                    editor.Show(main); editor.Refresh(); Capture(editor, "editor-rendered"); editor.Close();
                    var longTask = saved[0].Copy();
                    longTask.Name = "Unload the dishwasher and put everything back in the kitchen cupboards, including the awkward items on the top rack that need drying before putting them away";
                    using var longReminder = new ReminderForm(longTask, new AppSettings(), (_, _) => { });
                    longReminder.Show(); longReminder.Refresh();
                    Assert(longReminder.ClientSize.Height > 144, "Long task name did not expand the reminder.");
                    Capture(longReminder, "long-task-reminder"); longReminder.CloseSilently();
                    Console.WriteLine("PASS Native main, task editor, and reminder screenshots saved");
                    timer.Interval = 18000;
                }
                else
                {
                    var reminders = Application.OpenForms.OfType<ReminderForm>().ToArray();
                    Assert(reminders.Any(f => f.TaskId == tasks[1].Id), "Important reminder dismissed automatically.");
                    Assert(!reminders.Any(f => f.TaskId == tasks[0].Id), "Normal reminder did not auto-dismiss.");
                    Assert(new TaskStore(path).Load().Single(t => t.Id == tasks[3].Id).RemindersShown == 1, "Queued reminder was not eventually displayed.");
                    Console.WriteLine("PASS Normal timeout, important persistence, and queued reminder delivery");
                    Capture(reminders.Single(f => f.TaskId == tasks[1].Id), "important-persistent");
                    timer.Stop();
                    var tree = Descendants(main).OfType<TreeView>().Single();
                    foreach (var task in tasks.Take(4).ToArray())
                    {
                        var node = tree.Nodes.Cast<TreeNode>().SelectMany(n => n.Nodes.Cast<TreeNode>()).Single(n => n.Name == task.Id);
                        tree.SelectedNode = node;
                        Descendants(main).OfType<Button>().Single(b => b.Text == "Mark done").PerformClick();
                    }
                    Assert(!main.HasAttention, "Tray alert did not clear after completing outstanding tasks.");
                    Console.WriteLine("PASS Tray alert clears when tasks are completed");
                    main.Close(); Assert(!main.IsDisposed && !main.Visible, "Closing main window did not return to tray.");
                    main.Show(); Assert(main.Visible, "Main window did not reopen.");
                    Exit(main);
                    Assert(main.IsDisposed, "Exit did not close the main form.");
                    Assert(!Application.OpenForms.OfType<ReminderForm>().Any(), "Exit left reminder windows open.");
                    Console.WriteLine("PASS Close to tray, reopen, and explicit Exit closes reminders");
                    new TaskStore(path).Save([tasks[^1]]);
                    using var emptyToday = new MainForm(); emptyToday.Show(); emptyToday.Refresh();
                    var emptyTree = Descendants(emptyToday).OfType<TreeView>().Single();
                    Assert(emptyTree.Nodes["today"]!.Text == "Today (0)", "Empty Today group has wrong count.");
                    Assert(emptyTree.Nodes["other"]!.Nodes["future"] != null, "Saved future task disappeared after restart.");
                    Assert(!emptyToday.HasAttention, "Future task incorrectly shows an alert dot.");
                    Capture(emptyToday, "nothing-today-rendered"); Exit(emptyToday);
                    Console.WriteLine("PASS Restart reads saved future tasks, empty Today state, normal tray icon");
                }
            }
            catch (Exception error)
            {
                failure = error.ToString(); timer.Stop(); main.Dispose(); Application.ExitThread();
            }
        };
        main.Shown += (_, _) => timer.Start();
        Application.Run(main);
        if (failure != null) { Console.Error.WriteLine(failure); Environment.ExitCode = 1; }
        else Console.WriteLine("All native UI checks passed.");
    }
}
