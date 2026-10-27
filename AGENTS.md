# Project instructions

Read `PROJECT_PLAN.md` before making changes. It records the user's approved behavior, interface, constraints, and implementation plan. Update it when decisions change so future sessions can resume without relying on chat history.

## Working preferences

- No em dashes in output or application UI. No arrow symbols in application UI.
- Preserve the approved compact classic Windows appearance. Do not introduce new navigation or redesign screens without discussing it with the user.
- The working name is Bugger, but it must be easy to rename. Centralize user-facing application identity and avoid scattered name literals.
- Keep this a simple portable compiled Windows application with readable CSV storage. No separate database, cloud service, or JSON configuration.
- The user manages Windows startup themselves. Do not add startup registration.
- For long-running commands, downloads, builds, or generation, use a visible terminal window by default so the user can inspect progress. Do not use hidden or background runners unless requested.
- When delegating to subagents, do not wait for, poll, or check their status unless the user explicitly asks. Only delegate when authorized by applicable instructions.
- Prefer nontechnical explanations. Make reasonable implementation decisions within the approved scope, but do not silently change product behavior.

## Current stage

The user authorized implementation and explicitly requested screenshot validation. The first Windows Forms implementation is in `src/`, behavior checks are in `tests/`, and the portable build is in `outputs/Bugger/`. Read the implementation status in PROJECT_PLAN.md before continuing. Keep existing task files safe when rebuilding. Use the approved tight split view, compact editor, and task/tray menus as the design baseline.
