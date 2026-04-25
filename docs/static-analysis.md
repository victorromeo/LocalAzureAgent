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

## How tools work in LocalAgent

For each selected tool, LocalAgent:

1. Reads the tool definition from `LocalAgent/Tools/ToolManifest.json`
2. Downloads the tool if it is not already installed locally
3. Stores it in the per-user tools directory
4. Runs it as a local process on the same machine as the pipeline
5. Logs the tool output into the normal LocalAgent logs

### Where the tool runs

- The tool runs locally on the machine running `LocalAgent` or `LocalAgent.Service`.
- It scans the checked-out working copy, usually `$(Build.SourcesDirectory)`.
- LocalAgent does not send your source code to a LocalAgent-managed cloud service.

### Where tool files and output go

- Tool binaries and Python tool environments:
  - Windows: `%LocalAppData%\LocalAgent\.tools`
  - Linux: `~/.LocalAgent/.tools`
- Temporary output files: `$(Agent.TempDirectory)`
- Dependency-check vulnerability data cache: `$(Agent.CacheDirectory)/dependency-check/<version>`
- Logs and JSON/stdout output: LocalAgent console and log files

### Important data-flow note

The tools run locally, but some tools may still access external services depending on their configuration.

Examples:
- Tool downloads come from release URLs defined in the manifest
- `dependency-check` may download or refresh vulnerability data from NVD
- `semgrep --config=auto` may fetch rules from Semgrep-managed sources

If you need a fully restricted/offline environment, choose tools and arguments with that in mind.

## Tool versions and "latest"
Tool versions are pinned in the manifest at [LocalAgent/Tools/ToolManifest.json](../LocalAgent/Tools/ToolManifest.json). To use "latest", set the tool's `version` to `latest` and ensure `latestUrl` is defined for the OS/arch entry.

For tools defined with a `pythonModule` (like `lizard`), the runner will install the module into the per-user `.tools` directory and execute it via a generated wrapper. The module version is pinned in the manifest.

## Lizard prerequisites (Linux)
Lizard requires Python 3 and `pip` so the agent can install it into `.tools`.

Install the prerequisite package on Ubuntu/Debian:

```bash
sudo apt-get install -y python3-pip
```

## Tool-by-tool reference

### Horusec (`horusec`)

- **What it is**: Multi-language security static analysis (SAST). Good for general SAST coverage.
- **How it works**: Scans `$(Build.SourcesDirectory)` and writes JSON output.
- **Where it runs**: Locally on the pipeline machine.
- **Where your data goes**: Reads local source files; writes a JSON report to `$(Agent.TempDirectory)/horusec.json`; stores the binary in `.tools`.

```yaml
steps:
  - task: StaticAnalysis@1
    inputs:
      tools: 'horusec'
```

### TruffleHog (`trufflehog`)

- **What it is**: Secret scanner for files and repositories. Use in repos with sensitive credentials risk.
- **How it works**: Runs `trufflehog filesystem $(Build.SourcesDirectory) --json`.
- **Where it runs**: Locally on the pipeline machine.
- **Where your data goes**: Reads local files in the source directory; writes JSON to stdout/logs; stores the binary in `.tools`.

```yaml
steps:
  - task: StaticAnalysis@1
    inputs:
      tools: 'trufflehog'
```

### Semgrep (`semgrep`)

- **What it is**: Rule-based static analysis and code scanning. Good for custom rule policies and CI gating.
- **How it works**: LocalAgent installs the Python package and runs it against `$(Build.SourcesDirectory)`. Default arguments use `--config=auto`.
- **Where it runs**: Locally through Python on the pipeline machine.
- **Where your data goes**: Reads local source files; writes JSON to stdout/logs; stores the Python environment in `.tools`; may use network access for rule retrieval depending on configuration.

```yaml
steps:
  - task: StaticAnalysis@1
    inputs:
      tools: 'semgrep'
```

### OWASP Dependency-Check (`dependency-check`)

- **What it is**: Dependency vulnerability scanner. Use for Java/.NET dependency CVEs.
- **How it works**: Scans `$(Build.SourcesDirectory)` for dependency manifests and known CVEs.
- **Where it runs**: Locally on the pipeline machine.
- **Where your data goes**: Reads local project/dependency files; caches vulnerability data under `$(Agent.CacheDirectory)/dependency-check/<version>`; writes results to stdout/logs; stores the binary in `.tools`; may contact NVD services depending on configuration.

```yaml
steps:
  - task: StaticAnalysis@1
    inputs:
      tools: 'dependency-check'
```

### Gitleaks (`gitleaks`)

- **What it is**: Secret detection tool for source trees and repositories. Often faster than Trufflehog for repo-only checks.
- **How it works**: Runs `gitleaks detect --source $(Build.SourcesDirectory)`.
- **Where it runs**: Locally on the pipeline machine.
- **Where your data goes**: Reads local source files; writes JSON to stdout/logs; stores the binary in `.tools`.

```yaml
steps:
  - task: StaticAnalysis@1
    inputs:
      tools: 'gitleaks'
```

### Grype (`grype`)

- **What it is**: Vulnerability scanner for directories, packages, and images. Good for container/file system scans.
- **How it works**: Scans `dir:$(Build.SourcesDirectory)`.
- **Where it runs**: Locally on the pipeline machine.
- **Where your data goes**: Reads local files and package metadata; writes JSON to stdout/logs; stores the binary in `.tools`.

```yaml
steps:
  - task: StaticAnalysis@1
    inputs:
      tools: 'grype'
```

### Syft (`syft`)

- **What it is**: SBOM (Software Bill of Materials) generator for directories and images.
- **How it works**: Scans `dir:$(Build.SourcesDirectory)` and produces JSON output.
- **Where it runs**: Locally on the pipeline machine.
- **Where your data goes**: Reads local files and package metadata; writes JSON to stdout/logs; stores the binary in `.tools`.

```yaml
steps:
  - task: StaticAnalysis@1
    inputs:
      tools: 'syft'
```

### dotnet vulnerable (`dotnet-vulnerable`)

- **What it is**: LocalAgent wrapper around `dotnet list package --vulnerable`.
- **How it works**: Uses the local `dotnet` installation; no separate binary is downloaded.
- **Where it runs**: Locally on the pipeline machine in the selected working directory.
- **Where your data goes**: Reads local .NET solution/project files; writes output to stdout/logs.

```yaml
steps:
  - task: StaticAnalysis@1
    inputs:
      tools: 'dotnet-vulnerable'
```

### Lizard (`lizard`)

- **What it is**: Code complexity and maintainability metrics tool. Measures cyclomatic complexity and length across multiple languages.
- **How it works**: LocalAgent installs the Python package and runs it over `$(Build.SourcesDirectory)`. Automatically installed into `.tools` when configured with `pythonModule`.
- **Where it runs**: Locally through Python on the pipeline machine.
- **Where your data goes**: Reads local source files; writes metrics to stdout/logs; stores the Python environment in `.tools`.

```yaml
steps:
  - task: StaticAnalysis@1
    inputs:
      tools: 'lizard'
```

## Starter combinations

| Goal | Tools |
|---|---|
| Secret scanning | `trufflehog;gitleaks` |
| Dependency risk | `dependency-check;dotnet-vulnerable` |
| Security baseline | `horusec;semgrep;dependency-check` |
| Quality plus SBOM | `lizard;syft` |
| Full baseline | `horusec;trufflehog;semgrep;dependency-check;gitleaks` |

```yaml
steps:
  - task: StaticAnalysis@1
    displayName: Full security baseline
    inputs:
      tools: 'horusec;trufflehog;semgrep;dependency-check;gitleaks'
      workingDirectory: '$(Build.SourcesDirectory)'
```

## Output
Each tool writes its own output (typically JSON) to stdout. You can capture logs from the agent's `.logs` folder under the UserProfile directory.

## Trusted downloads
Tool downloads use official release URLs defined in the manifest. Update versions and URLs there to control downloads.
