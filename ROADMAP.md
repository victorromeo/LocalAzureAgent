# LocalAgent Roadmap

## Decisions
- Move all triggers (cron, file monitoring, webhooks) into LocalAgent.Service. Keep LocalAgent as a single-run CLI.
- Add a UserProfile store for persistent, user-scoped data.
- Store trigger configs under LocalAgent.Service/.triggers.
- Persist build iteration counters in the UserProfile cache.

## Planned Work (Prioritized)

1) Move all triggers into LocalAgent.Service
- Consolidate cron/FileDiskMonitor/webhooks in the service.
- LocalAgent remains single-run and can be invoked by the service.
- Migration notes for moving triggers from main CLI to service.

2) .triggers schema + validation
- Define a stable schema/versioning for trigger files.
- Validate trigger config on startup with clear errors.

3) Security for triggers
- Secrets storage guidance (avoid plaintext in repo).
- Webhook signature handling for supported providers.

4) UserProfile store + variables
- Add variables:
  - Agent.UserProfileDirectory
  - Agent.ToolsDirectory
  - Agent.CacheDirectory
- Expose these to tasks and environment.

5) Platform-specific defaults
- Windows: %LocalAppData%\\LocalAgent
- Linux: ~/.LocalAgent
- Subfolders: .temp, .cache, .tools
- Default Agent.TempDirectory => <UserProfile>/.temp if not provided.

6) Create/verify directories at startup
- Ensure .temp, .cache, .tools exist.
- Only clean .temp per job.

7) Tool bootstrap/install strategy
- Auto-download/install tools into <UserProfile>/.tools when missing.
- Use OS-specific sources/binaries.
- Keep versions easily updatable (central version config).
- Ensure version pinning and offline support.

8) Static analysis tooling support
- Add a task runner that uses tools from <UserProfile>/.tools against Build.SourcesDirectory.
- Tools: Horusec, Trufflehog, Semgrep, OWASP dependency-check, dotnet list package --vulnerable.

9) Cache policy
- Cache invalidation rules and size limits for <UserProfile>/.cache.

10) Incremental build iteration storage
- Persist build iteration counters under <UserProfile>/.cache/builds/, scoped by pipeline + branch.

11) Telemetry/logging locations
- Decide on log locations inside UserProfile for service/CLI.

12) Docs + tests
- Document service vs CLI split, new variables, and trigger file format.
- Update tests to reflect new default temp path.
