namespace Bugger.Core;

public static class Scheduler
{
    public static bool Advance(TaskItem task, DateTime now)
    {
        if (!task.Enabled) return false;
        var changed = false;
        // Each occurrence owns up to 24 hours, including overnight attempts.
        if (task.Status == "Pending" && task.WindowEnd > now) return false;
        var bucket = now.TimeOfDay >= task.StartTime ? now.Date : now.Date.AddDays(-1);
        var cursor = (task.EvaluatedDate ?? task.CreatedOn.Date.AddDays(-1)).AddDays(1);
        for (; cursor <= bucket; cursor = cursor.AddDays(1))
        {
            var carry = task.Status == "Pending" && task.ActiveRollover && task.OriginalDueDate.HasValue
                && (!task.ActiveRolloverDays.HasValue || (cursor - task.OriginalDueDate.Value).Days <= task.ActiveRolloverDays.Value);
            if (carry || task.Scheduled(cursor))
            {
                if (!carry) task.OriginalDueDate = cursor;
                Begin(task, cursor);
            }
            else if (task.Status == "Pending")
            {
                task.Status = "Expired";
                task.NextReminderAt = null;
                task.ImportantPending = false;
                task.SnoozedUntil = null;
            }
            task.EvaluatedDate = cursor;
            changed = true;
        }
        return changed;
    }

    private static void Begin(TaskItem task, DateTime day)
    {
        task.OccurrenceDate = day;
        task.Status = "Pending";
        task.RemindersShown = 0;
        task.ImportantPending = false;
        task.SnoozedUntil = null;
        task.ActiveStart = task.StartTime;
        task.ActiveInterval = task.IntervalMinutes;
        task.ActiveTotal = task.TotalReminders;
        task.ActiveRollover = task.RolloverEnabled;
        task.ActiveRolloverDays = task.RolloverDays;
        task.NextReminderAt = day + task.StartTime;
        task.WindowEnd = day.AddDays(1) + task.StartTime;
    }

    public static bool Due(TaskItem t, DateTime now) => t.Enabled && t.Status == "Pending"
        && t.WindowEnd > now && t.NextReminderAt <= now
        && (t.RemindersShown < t.ActiveTotal || t.ImportantPending);

    public static void Shown(TaskItem t, DateTime now)
    {
        if (!Due(t, now)) throw new InvalidOperationException("This reminder is not due.");
        t.RemindersShown = Math.Min(t.RemindersShown + 1, t.ActiveTotal);
        t.SnoozedUntil = null;
        t.ImportantPending = t.Important;
        t.NextReminderAt = t.RemindersShown < t.ActiveTotal ? now.AddMinutes(t.ActiveInterval) : null;
        // A persistent important popup must be recoverable after restarting.
        if (t.ImportantPending && t.NextReminderAt == null) t.NextReminderAt = now;
    }

    public static void Snooze(TaskItem t, DateTime now, int minutes)
    {
        if (t.Status == "Pending") { t.NextReminderAt = now.AddMinutes(minutes); t.SnoozedUntil = t.NextReminderAt; }
    }

    public static void Done(TaskItem t, DateTime now)
    {
        t.Status = "Done";
        t.CompletedAt = now;
        t.NextReminderAt = null;
        t.ImportantPending = false;
        t.SnoozedUntil = null;
    }

    public static bool NeedsAttention(TaskItem t, DateTime now) => t.Enabled && t.Status == "Pending"
        && t.WindowEnd > now && !(t.SnoozedUntil > now)
        && (t.RemindersShown > 0 || Due(t, now));
}
