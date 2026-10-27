using Bugger.Core;

var passed = 0;
static DateTime At(int day, int hour = 12, int minute = 0) => new(2026, 10, day, hour, minute, 0);
static TaskItem Task() => new() { Name = "Dishwasher", CreatedOn = At(23).Date, Weekdays = "Fri", StartTime = new(12, 0, 0) };
void Check(string label, Action test) { test(); Console.WriteLine("PASS " + label); passed++; }
static void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }

Check("First appearance counts, five reminders stop after the fifth", () => {
    var t = Task(); Scheduler.Advance(t, At(23));
    for (var i = 0; i < 5; i++) { Equal(true, Scheduler.Due(t, At(23).AddMinutes(30 * i))); Scheduler.Shown(t, At(23).AddMinutes(30 * i)); }
    Equal(5, t.RemindersShown); Equal(false, Scheduler.Due(t, At(23, 16))); Equal("Pending", t.Status);
});
Check("Sleep misses no allowance and returns one reminder with fresh cadence", () => {
    var t = Task(); Scheduler.Advance(t, At(23, 17)); Equal(0, t.RemindersShown); Scheduler.Shown(t, At(23, 17));
    Equal(At(23, 17, 30), t.NextReminderAt!.Value); Equal(false, Scheduler.Due(t, At(23, 17, 1)));
});
Check("Overnight schedule is valid, full-day overlap is invalid", () => {
    var t = Task(); t.StartTime = new(23, 0, 0); t.IntervalMinutes = 60; t.TotalReminders = 3; t.Validate();
    Scheduler.Advance(t, At(23, 23)); Scheduler.Shown(t, At(23, 23)); Equal(true, Scheduler.Due(t, At(24, 0)));
    t.TotalReminders = 25; try { t.Validate(); throw new Exception("Validation accepted overlap"); } catch (ArgumentException) { }
});
Check("No rollover expires and does not return on an unchecked day", () => {
    var t = Task(); Scheduler.Advance(t, At(23)); Scheduler.Advance(t, At(24)); Equal("Expired", t.Status); Equal(false, Scheduler.Due(t, At(24)));
});
Check("Rollover uses calendar days and fresh allowance", () => {
    var t = Task(); t.RolloverEnabled = true; t.RolloverDays = 3; Scheduler.Advance(t, At(23)); Scheduler.Shown(t, At(23));
    Scheduler.Advance(t, At(24)); Equal(0, t.RemindersShown); Equal(At(23).Date, t.OriginalDueDate!.Value); Equal(true, Scheduler.Due(t, At(24)));
    Scheduler.Advance(t, At(26)); Equal("Pending", t.Status); Scheduler.Advance(t, At(27)); Equal("Expired", t.Status);
});
Check("Many days offline does not extend finite rollover", () => {
    var t = Task(); t.RolloverEnabled = true; t.RolloverDays = 1; Scheduler.Advance(t, At(28)); Equal("Expired", t.Status);
});
Check("Regular occurrence merges into rollover without resetting original date", () => {
    var t = Task(); t.Weekdays = "Fri Sat"; t.RolloverEnabled = true; Scheduler.Advance(t, At(23)); Scheduler.Advance(t, At(24));
    Equal(At(23).Date, t.OriginalDueDate!.Value); Equal(At(24).Date, t.OccurrenceDate!.Value);
});
Check("Completion suppresses today and allows next scheduled occurrence", () => {
    var t = Task(); t.Weekdays = "Fri Sat"; Scheduler.Advance(t, At(23)); Scheduler.Done(t, At(23, 13));
    Scheduler.Advance(t, At(23, 14)); Equal(false, Scheduler.Due(t, At(23, 14))); Scheduler.Advance(t, At(24)); Equal(true, Scheduler.Due(t, At(24)));
});
Check("One-time task rolls over and then expires", () => {
    var t = Task(); t.OneTime = true; t.OneTimeDate = At(23).Date; t.RolloverEnabled = true; t.RolloverDays = 1;
    Scheduler.Advance(t, At(24)); Equal("Pending", t.Status); Scheduler.Advance(t, At(25)); Equal("Expired", t.Status);
});
Check("Final important reminder can snooze without consuming extra attempts", () => {
    var t = Task(); t.Important = true; t.TotalReminders = 1; Scheduler.Advance(t, At(23)); Scheduler.Shown(t, At(23)); Scheduler.Snooze(t, At(23), 15);
    Equal(false, Scheduler.Due(t, At(23, 12, 14))); Equal(true, Scheduler.Due(t, At(23, 12, 15))); Scheduler.Shown(t, At(23, 12, 15)); Equal(1, t.RemindersShown);
});
Check("Snooze crossing the boundary merges into a fresh allowance", () => {
    var t = Task(); t.RolloverEnabled = true; Scheduler.Advance(t, At(23)); Scheduler.Shown(t, At(23)); Scheduler.Snooze(t, At(24, 11, 55), 30);
    Scheduler.Advance(t, At(24)); Equal(0, t.RemindersShown); Equal(At(24), t.NextReminderAt!.Value);
});
Check("Paused task consumes nothing and resumes with one due reminder", () => {
    var t = Task(); t.Enabled = false; Scheduler.Advance(t, At(23)); Equal(false, Scheduler.Due(t, At(23)));
    t.Enabled = true; Scheduler.Advance(t, At(23, 18)); Equal(0, t.RemindersShown); Equal(true, Scheduler.Due(t, At(23, 18)));
});
Check("Schedule edit preserves the active occurrence boundary", () => {
    var t = Task(); Scheduler.Advance(t, At(23)); var end = t.WindowEnd; t.StartTime = new(8, 0, 0); t.TotalReminders = 1;
    Scheduler.Advance(t, At(23, 14)); Equal(end, t.WindowEnd); Equal(5, t.ActiveTotal);
});
Check("CSV quotes, restart progress, backup, and invalid-file protection", () => {
    var folder = Path.Combine(AppContext.BaseDirectory, "csv-check"); Directory.CreateDirectory(folder); var file = Path.Combine(folder, "tasks.csv");
    var t = Task(); t.Name = "A, quoted \"name\"\nsecond line"; t.Important = true; t.TotalReminders = 1;
    Scheduler.Advance(t, At(23)); Scheduler.Shown(t, At(23));
    var store = new TaskStore(file); store.Save([t]); var copy = store.Load().Single();
    Equal(t.Name, copy.Name); Equal(t.RemindersShown, copy.RemindersShown); Equal(true, Scheduler.Due(copy, At(23, 13)));
    store.Save([copy]); Equal(true, File.Exists(file + ".bak"));
    File.WriteAllText(file, "bad header\n"); try { store.Load(); throw new Exception("Accepted corrupt file"); } catch (FormatException) { }
    Equal("bad header\n", File.ReadAllText(file));
});
Check("Weekday tasks remain available in Other days on Saturday", () => {
    var t = Task(); t.Weekdays = "Mon Wed Fri"; t.CreatedOn = At(24).Date;
    Scheduler.Advance(t, At(24, 9)); Equal(false, TaskView.VisibleToday(t, At(24, 9)));
    Equal(At(26), TaskView.NextScheduledStart(t, At(24, 9))!.Value);
    Equal(true, TaskView.VisibleToday(t, At(26, 9))); // Upcoming today is in Today even before noon.
    t.Enabled = false; Equal(false, TaskView.VisibleToday(t, At(26, 9)));
});
Check("Alert dot follows outstanding, snoozed, completed, and rollover states", () => {
    var t = Task(); t.RolloverEnabled = true; Scheduler.Advance(t, At(23));
    Equal(true, Scheduler.NeedsAttention(t, At(23))); Scheduler.Shown(t, At(23));
    Equal(true, Scheduler.NeedsAttention(t, At(23, 12, 1))); // Still alerts between reminders and after popup timeout.
    Scheduler.Snooze(t, At(23, 12, 1), 15); Equal(false, Scheduler.NeedsAttention(t, At(23, 12, 2)));
    Equal(true, Scheduler.NeedsAttention(t, At(23, 12, 16)));
    t.Enabled = false; Equal(false, Scheduler.NeedsAttention(t, At(23, 12, 17))); t.Enabled = true;
    Scheduler.Done(t, At(23, 12, 17)); Equal(false, Scheduler.NeedsAttention(t, At(23, 12, 17)));
});
Check("Old CSV loads without new snooze column, and snooze survives restart", () => {
    var folder = Path.Combine(AppContext.BaseDirectory, "csv-check"); var file = Path.Combine(folder, "migration.csv");
    var t = Task(); Scheduler.Advance(t, At(23)); Scheduler.Shown(t, At(23)); Scheduler.Snooze(t, At(23), 30);
    var store = new TaskStore(file); store.Save([t]);
    var copy = store.Load().Single(); Equal(false, Scheduler.NeedsAttention(copy, At(23, 12, 15)));
    var lines = File.ReadAllLines(file); var header = lines[0].Split(','); var index = Array.IndexOf(header, "SnoozedUntil");
    lines[0] = string.Join(",", header.Where((_, i) => i != index));
    lines[1] = string.Join(",", lines[1].Split(',').Where((_, i) => i != index));
    File.WriteAllLines(file, lines); var old = store.Load().Single(); Equal(t.Name, old.Name); Equal(null, old.SnoozedUntil);
});
Check("One-time dates beyond next week still show their next reminder", () => {
    var t = Task(); t.OneTime = true; t.OneTimeDate = new DateTime(2026, 12, 1);
    Equal(new DateTime(2026, 12, 1, 12, 0, 0), TaskView.NextScheduledStart(t, At(24))!.Value);
});
Check("CLI add, edit, list, pause, resume, complete and delete", () => {
    var tasks = new List<TaskItem>(); var saves = 0;
    CommandResult Run(params string[] a) => TaskCommands.Execute(a, tasks, _ => saves++, At(23));
    var added = Run("add", "--name", "CLI, test", "--days", "fri,sat", "--time", "12:00");
    Equal(true, added.Success); Equal(1, tasks.Count); Equal("Pending", tasks[0].Status);
    Equal(true, Run("edit", "--id", added.Output, "--name", "Updated", "--count", "3").Success);
    Equal("Updated", tasks[0].Name); Equal(5, tasks[0].ActiveTotal);
    Equal(true, Run("list").Output.Contains("Updated")); Equal(2, saves);
    Equal(true, Run("pause", "--id", added.Output).Success); Equal(false, tasks[0].Enabled);
    Equal(true, Run("resume", "--id", added.Output).Success); Equal(true, tasks[0].Enabled);
    Equal(true, Run("complete", "--id", added.Output).Success); Equal("Done", tasks[0].Status);
    Equal(true, Run("delete", "--id", added.Output).Success); Equal(0, tasks.Count);
});
Check("CLI rejects invalid requests and preserves memory on failed save", () => {
    var tasks = new List<TaskItem> { Task() }; var original = tasks[0]; var saves = 0;
    foreach (var a in new[] {
        new[] { "add", "--name", "Bad", "--days", "Fry" },
        new[] { "edit", "--id", original.Id, "--count", "2000" },
        new[] { "delete", "--id", "missing" },
        new[] { "list", "--unknown", "x" },
        new[] { "add", "--name" },
        new[] { "add", "--name", "Bad", "--date", "2026-10-23", "--days", "Fri" }
    }) Equal(false, TaskCommands.Execute(a, tasks, _ => saves++, At(23)).Success);
    Equal(0, saves); Equal(original, tasks[0]);
    var failure = TaskCommands.Execute(["edit", "--id", original.Id, "--name", "Changed"], tasks, _ => throw new IOException("Locked"), At(23));
    Equal(false, failure.Success); Equal(original, tasks[0]); Equal("Dishwasher", tasks[0].Name);
});
Console.WriteLine($"All {passed} behavior checks passed.");
if (args.Length == 2 && args[0] == "--seed-preview")
{
    var now = DateTime.Now;
    var preview = new List<TaskItem>();
    foreach (var title in new[] { "Unload dishwasher", "Put bins out", "Water plants", "Clean desk" })
    {
        var item = new TaskItem { Name = title, CreatedOn = now.Date, Weekdays = string.Join(" ", TaskItem.DayNames), StartTime = new TimeSpan(now.Hour, now.Minute, 0), Important = title == "Put bins out", RolloverEnabled = true, RolloverDays = 3 };
        Scheduler.Advance(item, now);
        if (title == "Water plants") Scheduler.Done(item, now);
        if (title == "Clean desk") item.Enabled = false;
        preview.Add(item);
    }
    Directory.CreateDirectory(args[1]);
    new TaskStore(Path.Combine(args[1], "tasks.csv")).Save(preview);
    File.WriteAllText(Path.Combine(args[1], "settings.txt"), "Name=Bugger\nPopupSeconds=60\n");
    Console.WriteLine("Preview data saved in " + args[1]);
}
