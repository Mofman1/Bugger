# Bugger

A small portable Windows reminder app with weekly and one-time tasks, snoozing, rollover, a tray icon, and command line support. Tasks and progress stay in a readable CSV file beside the executable.

Version 0.3.0. Download the compiled Windows x64 app from [GitHub Releases](https://github.com/Mofman1/Bugger/releases/latest). Choose `Bugger.exe` for the standalone app or the ZIP for the app, instructions, and license. The .NET runtime is included. The executable is unsigned, so Windows may show a SmartScreen warning. Release assets include SHA-256 checksums.

For updates, exit Bugger and replace only `Bugger.exe`, keeping your `tasks.csv` and `settings.txt`. Build from source using the Development instructions below. Generated executables and personal task files are excluded from the source repository.

Run `outputs/Bugger/Bugger.exe`. The published application includes its Windows runtime. No installation, account, or database is needed. Keep the executable in a writable folder such as a folder under Documents, not Program Files.

## Daily use

The main window always shows all saved tasks in two collapsible groups: **Today** and **Other days**. Today includes scheduled and outstanding tasks, with completed items marked Done. Other days includes future schedules and paused tasks. Use the plus/minus boxes to expand or collapse groups; the application remembers your choice. A day with no reminders shows Today (0) without hiding your other tasks. Selecting a future task shows its next scheduled date and time.

Choose **New task** to set a name, weekly days or a one-time date, first reminder, interval, total reminders, importance, and rollover. The first reminder counts toward the total. The editor shows the last planned reminder and rejects schedules that overlap the next daily start.

Closing the main window keeps the application running in the tray. Double-click its tray icon to reopen it. Right-click the icon for Open, New task, and Exit. Windows may put the icon in its hidden tray icons area. Use Exit to stop reminders completely. Startup registration is left to you.

Normal reminders close after 15 seconds and return at the task's cadence until Done or the allowance is exhausted. Hover over one to pause its timeout. Important reminders ding and stay open until Done or Snooze. Closing an important reminder snoozes it for 15 minutes. Snooze choices are 15 minutes, 30 minutes, and 1 hour.

Three separate reminder windows can be visible at once. Other tasks wait without consuming their reminder attempts. Mark tasks done from their popup or the main window. Outstanding tasks remain visible even after the day's allowance is used.

The Tasks menu provides Edit, Pause or Resume, Duplicate, Delete, Expand all groups, and Collapse all groups. Delete asks for confirmation.

The blue beetle is the application and tray icon. An orange dot in the tray means at least one unsnoozed reminder needs attention, including a normal popup that has already hidden itself. Snoozing or completing all outstanding reminders clears the dot. It returns when a snooze ends. Paused tasks and tasks scheduled for later do not turn it on.

## Scheduling details

- Rollover retries on the next calendar day, even if that weekday is unchecked.
- A limit of 3 extra days means the original occurrence plus 3 rollover days. Unlimited continues until Done.
- Each new rollover day starts a fresh allowance at the task's first reminder time. A new scheduled occurrence merges with an existing rollover obligation.
- Overnight reminders are supported within the 24 hours following the first reminder. A final reminder cannot coincide with or pass the next daily start.
- Returning from sleep or restarting produces one eligible reminder, not a burst of missed alerts. Remaining attempts are spaced from that appearance and stop at the occurrence boundary.
- Missed rollover days still count toward a finite limit. Restarting does not reset that limit.
- A snooze crossing the daily boundary merges into the new daily allowance. An important popup refreshes at that boundary rather than duplicating itself.
- Schedule changes preserve the current occurrence and apply when a new daily occurrence begins. Task name and importance changes take effect immediately. Pause stops reminders without removing the task.
- The application must be running and Windows signed in for reminders to appear. Closing the main window is fine; Exit, shutting down, or signing out stops delivery until it runs again.

## Files and renaming

The app creates these beside the executable:

- `tasks.csv`: all task definitions and saved reminder progress. Back up this file to keep your tasks.
- `tasks.csv.bak`: previous successful save, for recovery.
- `settings.txt`: application name and normal popup duration.

Exit before manually editing or replacing files. If the CSV cannot be read, the application reports the problem and leaves it untouched. For recovery, exit, preserve the damaged file, and copy `tasks.csv.bak` to `tasks.csv`.

To rename the application in its UI, exit and change `Name=Bugger` in `settings.txt`. To rename the executable and build metadata too, change `AppDisplayName` in `Directory.Build.props` and rebuild. Source namespaces do not need to change. The CSV filename is independent of branding.

`settings.txt` is plain text with `Name=`, `PopupSeconds=`, `TodayExpanded=`, and `OtherDaysExpanded=` lines. Duration can also be changed through Settings. No JSON configuration is used. Runtime-generated build metadata files are not user configuration and are bundled into the published executable.

## CSV columns

Ordinary settings are `Name`, `Enabled`, `OneTime`, `Weekdays`, `OneTimeDate`, `StartTime`, `IntervalMinutes`, `TotalReminders`, `Important`, `RolloverEnabled`, and `RolloverDays`. Empty rollover days means unlimited when rollover is enabled. Weekdays use English three-letter names separated by spaces. Dates use `yyyy-MM-dd HH:mm:ss`, times use `HH:mm:ss`, booleans use `True` or `False`.

Other columns are maintained by the application: stable `Id`, `CreatedOn`, `EvaluatedDate`, `OccurrenceDate`, `OriginalDueDate`, `RemindersShown`, `NextReminderAt`, `CompletedAt`, `SnoozedUntil`, `ImportantPending`, `Status`, `ActiveStart`, `ActiveInterval`, `ActiveTotal`, `ActiveRollover`, `ActiveRolloverDays`, and `WindowEnd`. Active fields preserve the current schedule while edits wait for the next occurrence. SnoozedUntil keeps the tray status accurate after a restart. Older files without this column are read safely and gain it on the next save. CSV quoting supports commas, quotes, and multiline task names.

## Development

Read `AGENTS.md` and `PROJECT_PLAN.md` first. Approved HTML studies are in `design/`.

Use the .NET 8 SDK on Windows. `build.ps1` runs the behavior checks and builds the app. `build.ps1 -Publish` also creates the self-contained Windows x64 executable in `outputs/Bugger/`. Run long commands in a visible terminal as requested by the user.

Behavior checks: `dotnet run --project tests/Bugger.Checks -c Release`.

Native UI checks: `dotnet run --project tests/Bugger.UiChecks -c Release -- outputs/screenshots`. This opens a separate test application with its own test data, verifies stacking and timeouts, and captures native rendered forms. It does not touch tasks beside the published executable. Automated UI tests need an unlocked Windows desktop. Test screenshots are for layout verification; manual Computer Use screenshots also verify the actual running app.

The implementation uses native Windows Forms and the .NET standard library. NuGet is used only to obtain official runtime publishing packs; there are no third-party application libraries.

The release workflow runs the behavior checks, publishes the self-contained Windows x64 executable, and packages only the executable, README, and license. It publishes a draft only after every asset uploads successfully. Run it manually from GitHub Actions for a new project version; existing releases are never overwritten. Changes to the release workflow on main also run it.

# Command line support (0.3.0)

Run commands against the executable beside your tasks.csv. Requests go to that copy's running application, including when it is in the tray. If it is closed, commands read or update its CSV and exit without starting reminders. Do not edit the CSV externally while Bugger is running.

PowerShell examples (piping output makes PowerShell wait for this Windows GUI executable):

```powershell
.\Bugger.exe help | Out-String
.\Bugger.exe list | ConvertFrom-Json
.\Bugger.exe add --name "Unload dishwasher" --days Mon,Tue,Wed,Thu,Fri --time 12:00 --interval 30 --count 5 | Out-String
.\Bugger.exe add --name "Appointment" --date 2026-12-01 --time 09:00 --count 1 --important true | Out-String
.\Bugger.exe edit --id TASK_ID --time 13:00 --rollover unlimited | Out-String
.\Bugger.exe complete --id TASK_ID | Out-String
.\Bugger.exe pause --id TASK_ID | Out-String
.\Bugger.exe resume --id TASK_ID | Out-String
.\Bugger.exe delete --id TASK_ID | Out-String
```

`list` returns a JSON array with stable IDs, definitions and saved progress. JSON is command output only; storage remains CSV. Add and edit return the task ID. Success exits with code 0; errors print to standard error and exit with code 1. Scripts should wait for completion and check the exit code. Delete is immediate, without an interactive confirmation.

Add defaults match the task editor: weekdays, noon, every 30 minutes, five reminders, normal importance, rollover off. Options require values: `--important true|false`; `--rollover off|unlimited|NUMBER`. `--days` uses comma-separated three-letter weekdays; `--date` selects a one-time task. Edit accepts the same options, preserving unspecified values. Existing occurrence schedules retain their snapshot, just like edits in the window. Complete requires an outstanding occurrence. Invalid requests and failed saves do not change the live task list.

The local connection is restricted to the current Windows user and identified by the executable folder. Different portable copies keep separate task lists. If the running copy does not respond, the command fails rather than writing around it. An older running version must be exited before CLI support can be used.

