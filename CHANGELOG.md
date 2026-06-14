# CHANGELOG — IssuesTodo

<!-- Format:
commit-hash - YYYY-MM-DD - commit message
description of changes
-->

TBD - 2026-06-13 - Add optional priority, task comments, nag banner, tray icon, open folder, rename, link existing, drag-drop
Added Optional/Explorative (-ep) priority type with purple colour stripe. Added task comment support: indented lines in issues.md are parsed as comments; comment icon on each task row opens a dedicated CommentDialog. Added dismissible HP nag banner (red) that appears on startup listing projects with high-priority tasks. Added system tray NotifyIcon with balloon tip on startup when HP tasks exist. Added Open Folder button in project header (Explorer). Added Rename project via sidebar context menu. Added Link Existing Project mode in new-project dialog (browse to existing path). Added drag-drop reordering of tasks within a project (writes new order back to issues.md). CHANGELOG.md is now created for every new project. Fixed global path in CLAUDE.md to point at IssuesTodo\issues.md. Added GlobalUsings.cs to resolve WinForms/WPF namespace conflicts.
