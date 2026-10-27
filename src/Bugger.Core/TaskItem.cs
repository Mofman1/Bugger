namespace Bugger.Core;

public sealed class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool OneTime { get; set; }
    public string Weekdays { get; set; } = "Mon Tue Wed Thu Fri";
    public DateTime OneTimeDate { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = new(12, 0, 0);
    public int IntervalMinutes { get; set; } = 30;
    public int TotalReminders { get; set; } = 5;
    public bool Important { get; set; }
    public bool RolloverEnabled { get; set; }
    public int? RolloverDays { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.Today;
    public DateTime? EvaluatedDate { get; set; }
    public DateTime? OccurrenceDate { get; set; }
    public DateTime? OriginalDueDate { get; set; }
    public int RemindersShown { get; set; }
    public DateTime? NextReminderAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? SnoozedUntil { get; set; }
    public bool ImportantPending { get; set; }
    public string Status { get; set; } = "Waiting";

    // Snapshot the current occurrence so changing a schedule cannot move an active boundary.
    public TimeSpan ActiveStart { get; set; }
    public int ActiveInterval { get; set; }
    public int ActiveTotal { get; set; }
    public bool ActiveRollover { get; set; }
    public int? ActiveRolloverDays { get; set; }
    public DateTime? WindowEnd { get; set; }

    public static readonly string[] DayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    public bool Scheduled(DateTime day) => OneTime ? day.Date == OneTimeDate.Date
        : Weekdays.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(DayNames[(int)day.DayOfWeek]);
    public string ScheduleSummary => OneTime ? OneTimeDate.ToString("yyyy-MM-dd") : Weekdays.Replace(" ", ", ");
    public TaskItem Copy() => (TaskItem)MemberwiseClone();
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("Enter a task name.");
        if (Name.Length > 200) throw new ArgumentException("Keep the task name within 200 characters.");
        if (!OneTime && !DayNames.Any(d => Weekdays.Split(' ').Contains(d))) throw new ArgumentException("Choose at least one weekday.");
        if (StartTime < TimeSpan.Zero || StartTime >= TimeSpan.FromDays(1)) throw new ArgumentException("Enter a valid first reminder time.");
        if (IntervalMinutes < 1 || IntervalMinutes > 1439 || TotalReminders < 1 || TotalReminders > 1440)
            throw new ArgumentException("Use 1 to 1439 minutes and 1 to 1440 total reminders.");
        if ((long)(TotalReminders - 1) * IntervalMinutes >= 1440)
            throw new ArgumentException("The last reminder must be before the next daily start. Reduce the interval or total reminders.");
        if (RolloverDays is < 1 or > 36500) throw new ArgumentException("Rollover days must be between 1 and 36500, or unlimited.");
    }
}
