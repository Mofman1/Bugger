using System.Globalization;
using System.Text.Json;

namespace Bugger.Core;

public sealed record CommandResult(bool Success, string Output);

public static class TaskCommands
{
    public const string Help = "Commands: help, list, add, edit, complete, pause, resume, delete\nUse --id ID for existing tasks. add requires --name NAME.\nOptions: --name TEXT --days Mon,Tue --date yyyy-MM-dd --time HH:mm\n--interval MINUTES --count NUMBER --important true|false --rollover off|unlimited|DAYS\nlist returns JSON. add/edit return the task ID. Changes save immediately.\nWhen the application is closed, commands update storage without starting reminders.";

    // Work on copies so invalid requests and failed saves never alter live state.
    public static CommandResult Execute(string[] args, List<TaskItem> tasks, Action<List<TaskItem>> save, DateTime now)
    {
        try
        {
            if (args.Length == 0) throw new ArgumentException("Specify a command. Use help for usage.");
            var command = args[0].ToLowerInvariant();
            if (command is "help" or "--help" or "-h") return new(true, Help);
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < args.Length; i += 2)
            {
                if (!args[i].StartsWith("--") || i + 1 >= args.Length || !options.TryAdd(args[i][2..], args[i + 1]))
                    throw new ArgumentException("Options must be unique --name value pairs.");
            }
            var allowed = command switch
            {
                "list" => Array.Empty<string>(),
                "add" => ["name", "days", "date", "time", "interval", "count", "important", "rollover"],
                "edit" => ["id", "name", "days", "date", "time", "interval", "count", "important", "rollover"],
                "complete" or "pause" or "resume" or "delete" => ["id"],
                _ => throw new ArgumentException("Unknown command. Use help for usage.")
            };
            foreach (var key in options.Keys) if (!allowed.Contains(key, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Unknown option: --" + key);
            if (command == "list") return new(true, JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true }));
            var updated = tasks.Select(t => t.Copy()).ToList();
            var task = command == "add" ? new TaskItem { CreatedOn = now.Date } : updated.SingleOrDefault(t => t.Id == options.GetValueOrDefault("id"))
                ?? throw new ArgumentException("Supply an existing task ID with --id. Use list to find IDs.");
            if (command is "add" or "edit")
            {
                if (options.TryGetValue("name", out var name)) task.Name = name;
                if (options.ContainsKey("days") && options.ContainsKey("date")) throw new ArgumentException("Choose --days or --date.");
                if (options.TryGetValue("days", out var days))
                {
                    var tokens = days.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
                    var canonical = tokens.Select(d => TaskItem.DayNames.FirstOrDefault(n => n.Equals(d, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Use weekday names Sun,Mon,Tue,Wed,Thu,Fri,Sat.")).Distinct();
                    task.Weekdays = string.Join(" ", canonical); task.OneTime = false;
                }
                if (options.TryGetValue("date", out var date)) { task.OneTimeDate = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture); task.OneTime = true; }
                if (options.TryGetValue("time", out var time)) task.StartTime = TimeSpan.ParseExact(time, @"hh\:mm", CultureInfo.InvariantCulture);
                if (options.TryGetValue("interval", out var interval)) task.IntervalMinutes = int.Parse(interval, CultureInfo.InvariantCulture);
                if (options.TryGetValue("count", out var count)) task.TotalReminders = int.Parse(count, CultureInfo.InvariantCulture);
                if (options.TryGetValue("important", out var important)) task.Important = bool.Parse(important);
                if (options.TryGetValue("rollover", out var rollover)) { task.RolloverEnabled = rollover != "off"; task.RolloverDays = rollover is "off" or "unlimited" ? null : int.Parse(rollover, CultureInfo.InvariantCulture); }
                task.Validate();
                if (command == "add") updated.Add(task);
            }
            else if (command == "delete") updated.Remove(task);
            else if (command == "pause") task.Enabled = false;
            else if (command == "resume") task.Enabled = true;
            if (command != "delete") Scheduler.Advance(task, now);
            if (command == "complete")
            {
                if (task.Status != "Pending") throw new ArgumentException("This task has no outstanding occurrence to complete.");
                Scheduler.Done(task, now);
            }
            save(updated);
            tasks.Clear(); tasks.AddRange(updated);
            return new(true, task.Id);
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or IOException or UnauthorizedAccessException)
        { return new(false, e.Message); }
    }
}
