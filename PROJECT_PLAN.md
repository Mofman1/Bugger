# Bugger working plan

## GitHub publication

The user requested a shorter, visual GitHub front page. README.md now leads with the approved blue beetle logo, an actual native main-window screenshot with sample tasks, and a reminder popup screenshot. These images are copied into docs/images/ for durable repository links. The full prior instructions are retained in docs/USER_GUIDE.md. No application behavior or layout changes are involved.

The user also requested a release with the compiled executable. `.github/workflows/release.yml` builds on a Windows runner with .NET 8, runs behavior checks, and publishes a self-contained Windows x64 executable, a ZIP containing only the executable/README/LICENSE, and SHA-256 checksums. It creates a draft, uploads all assets, then publishes. Existing releases are never overwritten. It runs manually or when its workflow file changes on main. Release version comes from the application project. Personal CSV/settings and logs are never packaged. Native UI checks remain the locally recorded development checks.

The user created the public GitHub repository https://github.com/Mofman1/Bugger with a LICENSE file. Publish source, design references, tests, and documentation on main, preserving that license. Generated outputs, work logs, task CSV files, settings, editor state, and environment files are excluded. The local origin points to this repository. Version 0.3.0 is the initial source publication; application behavior is unchanged.

## Purpose and status

Build a small Windows habit and obligation reminder application. Example: remind the user to unload the dishwasher at lunch on selected weekdays. The task remains outstanding until Done; dismissing a popup is not completion.

Working title: Bugger. The user is not committed to the name. Centralize application identity so renaming updates window titles, tray text, menus, messages, executable metadata, and packaging with minimal changes. Keep task storage named `tasks.csv`, independent of branding.

Planning used Matt Pocock's grill-me and grilling skills, followed by interactive HTML studies. Behavior and visual direction are approved. The user subsequently authorized the build and explicitly asked for screenshot validation. The first implementation is now in src/, with automated checks in tests/ and a portable executable in outputs/Bugger/.

## User constraints

- Compiled Windows app; C# with Windows Forms was explicitly accepted.
- Classic Windows utility appearance inspired by Hiren's BootCD 15.2 Program Launcher, hbcdlauncher.exe.
- Compact gray surfaces, standard menus, simple buttons, standard desktop controls. Avoid a modern web dashboard aesthetic.
- No arrows and no em dashes in application UI. User also prohibits em dashes in assistant output.
- All task creation and editing should be convenient in the UI. User does not want to learn JSON or manually maintain structured configuration.
- Human-readable CSV storage, no separate database or cloud dependency.
- Portable: `tasks.csv` beside the executable, in a writable folder.
- User will handle startup through Windows themselves. No automatic startup registration.

## Approved visual design

Use the tight split view. Main window has Tasks, Settings, Help menus, a compact New task and Edit toolbar, task list on the left, selected task details on the right, and a Mark done button. Bottom status indicates tray operation and `tasks.csv`.

Selected task details show its name, scheduled days and start time, reminder count and cadence, and rollover details. Task states include outstanding, completed, paused, and tasks not due today when showing all tasks.

The compact task editor has:

- Task name.
- Schedule choice: Weekly or One time.
- Weekday checkboxes for weekly tasks, or date for one-time tasks.
- First reminder time.
- Every (minutes).
- Total reminders, including the first appearance.
- Important checkbox.
- Roll over daily checkbox.
- Optional maximum extra rollover days, with an unlimited option.
- Calculated last reminder time, including next-day indication.
- Save task and Cancel.

Tasks menu contains New task, Edit task, Pause task or Resume task, Duplicate task, Delete task, Show all tasks or Show Today only, and Exit Bugger using the central application name. Deleting requires confirmation. All tasks uses the same list and split layout, not new tabs or a separate navigation system.

Closing the main window returns it to the tray. Tray context menu provides Open application, New task, and Exit application. Exit fully quits and stops reminders. No startup-management UI is needed.

Reminder windows are individual small windows stacked vertically upward from the bottom-right of the main screen. They repeat the same compact UI with task name, reminder progress, due/rollover information, Done, Snooze, and snooze duration. Important reminders have a brief ding and stay open. They should not steal keyboard focus.

Keep task names dark on white backgrounds. Earlier HTML had white-on-white text; explicit text colors fixed it. Ensure real controls remain readable, including selected rows and disabled states.

## Approved reminder behavior

### Schedules and attempts

- Each task defines its own active weekdays or a one-time date, first reminder time, cadence, total attempts, importance, and rollover policy.
- No work-location calendar or separate work-from-home schedule.
- No global quiet hours. The task's start time, count, and cadence define its reminder window.
- Five total reminders every 30 minutes starting at noon means 12:00, 12:30, 13:00, 13:30, 14:00.
- Overnight schedules are allowed. An occurrence's reminder window must end strictly before the next daily start time. Display the calculated last reminder and reject overlapping settings with a useful explanation.
- Popups that have not actually appeared do not consume attempts.
- After sleep, shutdown, or other missed trigger times, show one eligible reminder on return, not a burst of all missed reminders. Space remaining attempts from that appearance. Keep them within the occurrence's valid window and prevent overlap with the next daily start.
- Normal reminder auto-hides after 15 seconds; application setting can change this duration. Hover pauses the timeout.
- Hiding or closing a normal reminder leaves its task outstanding and eligible at the next cadence.
- Once the attempt allowance is exhausted, stop further ordinary reminders for that occurrence but retain its unfinished status in Today.
- Critical/important reminders do not auto-hide. They remain until Done or an explicit Snooze. Closing one snoozes rather than completes it.
- Snooze postpones a reminder without resetting the allowance. The popup already shown counts as an attempt.
- The final allowed important popup may still be snoozed and brought back as the same final attempt.
- A snooze crossing the next daily boundary merges into that day's fresh allowance, rather than creating parallel schedules.

### Stacking and queuing

- Separate popup windows, not one combined window containing multiple tasks.
- Maximum three visible at a time; additional due reminders wait.
- Important reminders can occupy slots persistently. Every outstanding/waiting task must remain accessible from the main window and tray.
- Only one outstanding obligation per task. Completing it clears that obligation; never accumulate duplicate windows for separate daily instances of the same chore.

### Rollover

- Toggle per task.
- Optional numeric limit counts extra calendar days to retry. For example, 3 means the original day plus at most 3 additional days.
- With rollover enabled and no numeric limit, continue until Done.
- Rollover uses the next calendar day, even when that weekday is not checked in the regular weekly schedule.
- Each rollover day gets a fresh full reminder allowance at the configured start time.
- An outstanding rollover and a new regular occurrence merge into one obligation.
- Without rollover, an unfinished occurrence expires when its occurrence window ends; it must not generate future reminders for that expired occurrence.
- One-time tasks support rollover too.

## Persistence

One CSV contains task definitions plus program-maintained progress columns, which the user accepted. Persist enough to survive restarts without duplicating reminders, losing completion, or resetting allowances. No separate database or JSON sidecar.

Proposed schema, subject to routine implementation refinement:

`Id,Name,Enabled,ScheduleType,Weekdays,OneTimeDate,StartTime,IntervalMinutes,TotalReminders,Important,RolloverEnabled,RolloverDays,OccurrenceStart,OriginalDueDate,RemindersShown,NextReminderAt,CompletedAt,Status`

- Stable task ID is independent of name.
- Use conventional CSV quoting for commas, quotes, and newlines; do not hand-split lines on commas.
- Prefer readable dates and times with unambiguous formats. Document column meanings.
- Empty RolloverDays means unlimited when rollover is enabled.
- Persist state on meaningful transitions and before exit, not continuously on every timer tick.
- Write atomically so interruption does not truncate the only task file. A temporary write and recoverable backup are implementation details, not a second data store.
- Invalid rows or unwritable storage should produce a clear actionable message and preserve existing data.
- Do not overwrite user data with sample tasks. Samples are appropriate only in design previews or explicit demo mode.

## Implementation approach

1. Inspect repository instructions and available Windows/.NET tooling. Choose a supported Windows Forms target based on actual installed SDKs and packaging needs. Verify changing toolchain facts from primary documentation when necessary.
2. Keep implementation small: application identity, task model, CSV repository, scheduler, main form, task editor, reminder form, tray integration, and application settings.
3. Separate scheduling decisions from UI and from the clock. Use an injectable clock or equivalent to test time transitions without waiting in real time.
4. Build the approved compact main form and editor with native Windows Forms controls. Match spacing and hierarchy from the final HTML study, not browser-specific rendering artifacts.
5. Implement CSV round-tripping and validation before wiring reminder delivery.
6. Implement a deterministic occurrence/scheduler model handling daily boundaries, overnight windows, attempts, snooze, rollover limits, duplicates, sleep/resume, and completion.
7. Add focus-preserving reminder windows, stacking, queueing, normal timeout with hover pause, and brief sound for important tasks.
8. Add tray behavior and menu actions. Closing main form hides it; explicit Exit disposes tray icon and closes the process cleanly. Prevent multiple processes from racing on the same portable task file.
9. Package a compiled Windows application, preferably self-contained if practical, so the user can run it without separately managing a runtime. Confirm actual build/package behavior rather than assuming a single-file distribution is available.
10. Verify the application and deliver run instructions with the executable and readable CSV format documentation.

Application-wide settings are stored in a small plain-text settings.txt with Name= and PopupSeconds= lines. This implementation choice was communicated before development. Tasks and progress remain in one CSV, with no database or JSON configuration.

## Required verification

Test behavior rather than mirroring implementation:

- Count includes first reminder; cadence and final trigger calculation.
- Overnight windows and rejection of a full-day or overlapping window.
- Weekly and one-time schedules, completion, and expiration.
- Rollover to unchecked calendar days, finite extra-day limits, unlimited rollover, and fresh allowances.
- Merge new scheduled occurrences with outstanding rollover.
- Sleep/resume and restart without a reminder burst or resetting progress.
- Snooze within and across boundaries, including final important reminder.
- Queue counts attempts only when shown; no more than three reminder windows.
- CSV round-trip with punctuation and recovery from invalid or interrupted writes.
- Native UI inspection for readable task names, compact spacing, menu behavior, focus preservation, tray hiding/exiting, and persistent important reminders.
- Search application strings for prohibited arrow symbols and em dashes.

Do not add unnecessary infrastructure or tests for trivial cosmetic details. Use a visible terminal for long builds/downloads as requested by the user.

## Design references

Approved final interactive mockup, with task and tray menus:

`C:/Users/Stuart/.codex/visualizations/2026/10/24/01a16c04-9ecd-7680-8fd5-a114cb33bf6c/bugger-task-menus.html`

Earlier approved compact split study:

`C:/Users/Stuart/.codex/visualizations/2026/10/24/01a16c04-9ecd-7680-8fd5-a114cb33bf6c/bugger-compact-split.html`

Original screenshot attachments, possibly temporary:

- `C:/Users/Stuart/AppData/Local/Temp/codex-clipboard-364d3bb7-cba3-4344-8988-1ad7e8f56431.png`
- `C:/Users/Stuart/AppData/Local/Temp/codex-clipboard-ddde0e72-f6f7-4817-897a-96082aa67606.png`

Copy approved HTML references into the project's `design/` folder for durable access. These are previews, not application code. Their scripts simulate interactions and are not an authoritative scheduling implementation.

## Implemented edge decisions

These implementation choices were communicated during development:

- Offline days are processed in calendar order from persisted progress. Finite rollover is anchored to OriginalDueDate and cannot be extended by restarting. Expired one-time tasks do not reappear. A later weekly occurrence may start a new obligation once the old one has expired.
- Important reminders refresh as the occurrence changes. A task can have only one visible popup. Old windows are silently removed when completed, paused, deleted, or advanced.
- Default and close-to-snooze duration is 15 minutes. Other choices are 30 minutes and 1 hour.
- settings.txt holds application-wide settings. The distribution is a self-contained Windows x64 single executable. The official runtime is bundled; first run may extract native components to the runtime's normal temporary cache.
- Active schedule fields snapshot the current occurrence. Schedule edits apply at the next occurrence; name and importance are immediate. If a start time is edited, the current snapshot boundary is preserved, and a later new start is respected before initiating another occurrence.

## Implementation and verification status

- .NET 8 SDK and Windows Forms were available locally. There are no third-party runtime dependencies. NuGet.Config explicitly enables the official NuGet feed for runtime publishing packs because the host's default feed list was empty.
- src/Bugger.Core contains task data, validation, deterministic scheduling, and atomic CSV persistence. src/Bugger.App contains the Windows Forms interface, tray lifecycle, settings, and popup queue.
- 14 deterministic behavior and CSV checks pass, including overnight schedules, offline catch-up, finite/unlimited calendar-day rollover, merging, pause/resume, completion, snoozing the last important reminder, daily boundaries, edits, quoting, backups, and invalid-file preservation.
- Native UI checks open isolated sample data, verify three separate windows and their bounds, ensure queued attempts are not consumed, verify normal timeout and important persistence, and capture rendered screenshots. They also test closing to tray, reopening, and explicit Exit.
- Actual main-window, task-editor, and Tasks-menu screenshots were inspected with Windows Computer Use. Native rendered editor and normal/important reminder screenshots were inspected as well. Compact controls and text were readable with no observed clipping.
- Long task names expand the reminder window; the details pane scrolls when needed. A long-name reminder screenshot was inspected to verify response buttons remain visible.
- Evidence and screenshots are in outputs/screenshots/. Logs are in work/. User-facing instructions are in README.md and copied into the release folder.
- The release starts with no demo tasks. It creates tasks.csv and settings.txt beside the executable on first successful launch. Test data remains under work/ and tests/bin/.
- The final self-contained executable was launched from outputs/Bugger/ and inspected in a real screenshot. First-run storage creation succeeded with zero tasks. It was left open for the user. The screenshot is outputs/screenshots/release-first-run.png.
- Runtime UI name comes from settings.txt, defaulting to build metadata. Build executable name and metadata come from AppDisplayName in Directory.Build.props.
- Tests use fixed local times and do not prove behavior across every daylight-saving or time-zone change. Time is local Windows time; there is no background Windows service. User must be signed in and the app running.

Update this document with the final choices and implementation/verification status as work progresses.

## Follow-up: task visibility and grouped main view

The user reported that CSV saving appeared to work but reading did not. Inspection found two saved Dishwasher tasks, both limited to weekdays, while the current date was Saturday. The Today filter therefore hid them. The user confirmed they had not realized the main window was Today-only and requested a new HTML mockup showing all tasks, separated into collapsible groups for those scheduled today and those on other days.

Approved for implementation by the user's request to update the build: design/all-tasks-groups-proposal.html. It keeps the tight split view and shows Today and Other days folder groups, with plus/minus expand controls rather than arrows. A second example shows the empty-Today state with the two weekday Dishwasher tasks still visible under Other days. The native implementation now uses a compact TreeView with folder icons and saves each group's expanded state. There is no Today-only filtering mode in the main window.

The earlier source-only troubleshooting changes were reconciled with the grouped view: all tasks remain present, the status bar shows saved and today counts, and future tasks show their next start. Settings now store TodayExpanded and OtherDaysExpanded rather than a ShowAllTasks filter.

The user requested constructed vector icons rather than an image-generation model. Six original SVG options were built: amber bell, blue bell, plain bell (each with a red notification dot), alarm clock, checklist, and little beetle. The editable sources are in design/icons/. Preview sheet, transparent PNGs, SVGs, and multi-resolution Windows ICO files are in outputs/icons/. Each ICO contains 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixel sizes. The preview sheet was visually inspected on light and dark tray backgrounds, XML sources were parsed, and ICO sizes were validated. Regenerate with python design/build-icons.py, which layers original vector shapes and renders via Sharp, then packages via Pillow. The icon is not chosen or installed in the application yet. Amber bell is the recommended direction; simple shapes preserve readability and allow a later app rename.

The user then requested a beetle with an alert dot for an unsnoozed reminder. The layered red-dot variant is design/icons/little-beetle-alert.svg, with PNG and ICO exports in outputs/icons/. The normal/alert light- and dark-tray comparison is outputs/icons/beetle-alert-preview.png, generated by design/build-beetle-alert.py. This remains a proposal, not an installed icon or implemented status behavior.

The user selected the cooler Windows-style blue beetle (#6099c3, pale blue highlight). This is the approved application icon direction. Use the normal blue beetle for the application and tray, and the orange-dot variant in the tray when an unsnoozed reminder needs attention. Both sources are design/icons/blue-beetle.svg and blue-beetle-alert.svg, with multi-resolution PNG and ICO exports in outputs/icons/. The comparison is outputs/icons/blue-beetle-preview.png. Regenerate with design/build-blue-beetle.py. Executable resources and live tray status behavior are implemented for version 0.2.0, with canonical embedded resources in src/Bugger.App/Assets/. Do not treat the earlier amber-bell recommendation as the selected design. No application rename was requested with this selection.

Version 0.2.0 adds optional SnoozedUntil persistence. Older CSV files missing this new column retain their definitions and progress. The tray dot is on for a due or already-shown outstanding reminder, even after its normal popup disappears, and off while snoozed, completed, paused, or expired. Multiple tasks combine: the dot stays on if any task still needs attention. At an occurrence boundary, rollover starts a new allowance and clears the old snooze as previously agreed. New behavior checks cover group classification, future dates, icon attention state, snooze across restart, and legacy CSV loading.

Version 0.2.0 is published to outputs/Bugger/Bugger.exe and was launched successfully. Eighteen behavior checks and native UI checks pass, covering grouping, remembering collapse state, queued reminders, important persistence, clearing attention, close-to-tray/exit, and loading future tasks after restart. Pre-update files were backed up under work/backup-before-v02-20261024-103723/. The user clarified that the current CSV contains disposable test data, so do not block routine updates on preserving this particular test fixture. Continue to treat future real task data carefully.

The final v0.2 native screenshot with visible folder icons is outputs/screenshots/updated-build.png. It was visually inspected for readability and compact spacing. Final native UI checks pass after correcting a test-only screenshot helper that pumped an application-exit message during the restart capture. Production code did not use that helper. Test evidence is work/grouped-build.log (18 behavior checks), work/ui-checks.log (native UI checks), and work/grouped-publish.log (portable executable).

Latest cosmetic update: the user requested only changing the blue beetle alert dot from red to orange (#e68a22), with no other behavior changes, no full retesting, and no UI automation or computer control. The generator and alert resource were updated, and a direct publish rebuild was requested without launching the application.

## CLI support, version 0.3.0

The user approved CLI support after discussing a local named pipe rather than live CSV reloading. Implemented commands: help, list, add, edit, complete, pause, resume, delete. Options and examples are in README.md. List emits JSON to stdout for scripts; task storage remains CSV. Exit codes are 0 for success and 1 for errors. Delete is explicitly noninteractive. No navigation or layout changes.

Commands target the portable folder's running process using a current-user-only named pipe, executed on the UI thread with the normal scheduler and persistence. The existing folder mutex serializes offline access. If closed, commands update storage and exit without launching reminders. A running process that cannot be reached causes an error, never an offline fallback. No external CSV reload is implemented.

Command validation and save use copied tasks; live state changes only after the atomic CSV save succeeds. Existing occurrence snapshots survive edits. Successful changes refresh the list, dismiss obsolete popups and update tray attention. Help does not require opening task storage. GUI executable console attachment supports interactive use and redirected output; PowerShell examples pipe output to wait for completion.

Twenty behavior checks pass, including CLI lifecycle, strict options, invalid requests and failed-save preservation (work/cli-checks.log). Compiled executable integration checks pass for offline add/list and live add/edit/list/pause/resume/delete, redirected stdout/stderr and error exit codes (work/cli-integration-result.txt). An initial integration harness failure was caused by Windows PowerShell array wrapping; fixing the harness allowed the checks to pass. The isolated test process was stopped after its final saved changes. Version 0.3.0 was published self-contained and installed in outputs/Bugger/, retaining tasks.csv and settings.txt. The previous executable is backed up under work/backup-before-cli/. No application layout changes were made.

