# Static Analysis Tools

This project includes a built-in `StaticAnalysis@1` task that auto-downloads tools into the per-user `.tools` directory when missing. Tools run against `$(Build.SourcesDirectory)` by default.

## How to add to a pipeline

```yaml
steps:
  - task: StaticAnalysis@1
    inputs:
      tools: 'horusec;trufflehog;semgrep;dependency-check;gitleaks;grype;lizard'
      workingDirectory: '$(Build.SourcesDirectory)'
      arguments: ''
```

### Tool selection
- `tools` accepts a semicolon/comma/space-separated list. If omitted, all tools in the manifest run.
- `workingDirectory` defaults to `$(Build.SourcesDirectory)`.
- `arguments` lets you pass extra arguments for all tools.

## Tool versions and “latest”
Tool versions are pinned in the manifest at [LocalAgent/Tools/ToolManifest.json](../LocalAgent/Tools/ToolManifest.json). To use “latest”, set the tool’s `version` to `latest` and ensure `latestUrl` is defined for the OS/arch entry.

For tools defined with a `pythonModule` (like `lizard`), the runner will install the module into the per-user `.tools` directory and execute it via a generated wrapper. The module version is pinned in the manifest.

## Lizard prerequisites (Linux)
Lizard requires Python 3 and `pip` so the agent can install it into `.tools`.

Install the prerequisite package on Ubuntu/Debian:

sudo apt-get install -y python3-pip

## When to use each tool

- Horusec (`horusec`): Static code analysis across multiple languages with security rules. Good for general SAST coverage.
- Trufflehog (`trufflehog`): Secret scanning for code and history. Use in repos with sensitive credentials risk.
- Semgrep (`semgrep`): Fast SAST with rule packs; good for custom rule policies and CI gating.
- OWASP Dependency-Check (`dependency-check`): Dependency vulnerability scanning; use for Java/NET dependency CVEs.
- Gitleaks (`gitleaks`): Secrets scanning (similar to Trufflehog), often faster for repo-only checks.
- Grype (`grype`): Vulnerability scanning of files, directories, and images; good for container/file system scans.
- dotnet vulnerable (`dotnet-vulnerable`): Built-in `dotnet list package --vulnerable` for .NET dependency checks.
- Lizard (`lizard`): Cyclomatic complexity and length metrics across multiple languages. Automatically installed into `.tools` when configured with `pythonModule`.

## Output
Each tool writes its own output (typically JSON) to stdout. You can capture logs from the agent’s `.logs` folder under the UserProfile directory.

## Trusted downloads
Tool downloads use official release URLs defined in the manifest. Update versions and URLs there to control downloads.
