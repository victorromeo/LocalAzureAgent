# LocalAzureAgent

A simple build agent, which can run on a local .Net Core host and build source code using a Azure DevOps compliant yaml file.

The aim of the project is to assist the local validation of build yaml scripts prior to committing to Azure DevOps repos. 

LocalAgent is a single-run CLI for executing a pipeline once. Triggered/continuous execution is handled by LocalAgent.Service.

## Quick Start

If this is your first time using the project, start with one of these two paths:

### Option 1: Run one pipeline once (CLI)

Use this when you want to validate a pipeline locally and exit.

1. Build the solution:

```cmd
dotnet build
```

2. Run the sample WebApplication pipeline:

```cmd
dotnet run --project .\LocalAgent\LocalAgent.csproj -- Samples\WebApplication1 pipeline.yml --work Samples\work --id 2
```

3. Confirm success:

- the console shows `Pipeline started`
- steps run and finish
- the pipeline ends with a summary line

### Option 2: Run pipelines automatically (Service)

Use this when you want scheduled, file-watch, or webhook-triggered execution.

1. Start the service:

```cmd
dotnet run --project .\LocalAgent.Service\LocalAgent.Service.csproj
```

2. Check health:

```cmd
curl http://localhost:5071/health
```

3. Trigger a configured webhook:

```cmd
curl -X POST http://localhost:5071/webhooks/webapp1 ^
  -H "X-LocalAgent-Secret: changeme" ^
  -H "Content-Type: application/json" ^
  -d "{}"
```

## Choose Your Mode

- Use `LocalAgent` if you want to run one pipeline once.
- Use `LocalAgent.Service` if you want cron schedules, file monitoring, or webhooks.

## Prerequisites

- .NET 9 SDK installed
- A pipeline YAML file to run
- Any external tools required by your specific pipeline tasks:
  - Docker for Docker tasks
  - Node.js for Node-related tasks
  - MSBuild / Visual Studio build tools for MSBuild and VSTest scenarios

If a pipeline references a tool that is not installed, the relevant task may fail even though LocalAgent itself starts correctly.

## What Happens When You Run It

By default LocalAgent tries to behave like a local Azure DevOps-style build:

- source is copied into a work folder unless `--inplace true` is used
- temp files are created in the agent temp directory and cleaned after the job
- artifacts go into the artifact staging directory
- secrets registered with `task.setvariable isSecret=true` are masked in logs

Typical locations:

- work folder: `--work` value or default work location
- temp folder: `${Agent.UserProfileDirectory}/.temp`
- logs: console output plus LocalAgent log file
- service trigger files: `LocalAgent.Service/.triggers`

## Comments and Recommendations

- It is assumed that the build always runs upon the local machine, where local means the same server as the source code.  There is no specific reason why this must be enforced, but it is the philosophy.  If intend to build off the current machine, use a real Azure DevOps build agent. 
- When running the utility, its is advised to not build into the source code directory. Whilst there is no limitation preventing this, the intent is to support continuous builds whenever the source code is modified.  As a result, the build typically gets built away from the source code folder, inside a separate "work" folder.  This replicates the behaviour also of Azure Dev Ops build agents.
- When a build is initiated, the source code is first copied into the work folder, then the build commences, by executing the supplied yaml
- Standard Azure DevOps predefined variables are supported, albeit to a limited extent due to the change in context

## Supported Azure DevOps yaml functions

- Elements
  - Deploy
  - DeployStep
  - Inputs
  - Job
  - JobStep
  - Stage
  - StageJob
  - Step
  - Strategy
  - Variable
  - Template
  - Workspace
- Task runners
  - ArchiveFiles
  - BatchScript
  - CmdLine
  - CopyFiles
  - DotnetCli
  - Docker
  - ExtractFiles
  - Kubernetes
  - MSBuild
  - NodeTool
  - NuGetCommand
  - NuGetToolInstaller
  - Powershell
  - PublishBuildArtifacts
  - ReplaceTokens
  - UpdateAssemblyInfo
  - UseDotNet
  - VSTest

## Condition Evaluation (Current Behavior)

LocalAgent currently has two condition-evaluation modes:

- Normal execution (`--dry-run false`, default):
  - YAML `condition` expressions are evaluated and used to gate job/step execution
- Dry-run (`--dry-run true`):
  - pipeline is parsed and walked without executing external tools
  - the same condition evaluator is used, so gating decisions match normal execution

Condition functions currently supported:

- `succeeded()`
- `failed()`
- `always()`
- `canceled()`
- `and(...)`
- `or(...)`
- `not(...)`
- `eq(...)`
- `ne(...)`

Authoring advice:

- Use dry-run to validate condition logic before full execution.
- Prefer simple, explicit conditions while parity is being expanded.
- Treat complex Azure-specific expressions as best-effort today.

For detailed guidance and examples, see [docs/condition-evaluation.md](docs/condition-evaluation.md).

## Build

```cmd
dotnet build
```

## Test

```cmd
dotnet test
```

## CLI usage

### Run once via dotnet CLI

```cmd
dotnet run --project .\LocalAgent\LocalAgent.csproj -- <yaml> <options>
```

### Run once via published executable

```cmd
LocalAgent.exe <yaml> <options>
```

### Example: run with implicit source directory

When the YAML file is in the same folder as your source code, the source directory is automatically inferred:

```cmd
dotnet run --project .\LocalAgent\LocalAgent.csproj -- Samples\WebApplication1\pipeline.yml
```

### Example: run with explicit source directory

You can explicitly specify the source directory using --source:

```cmd
dotnet run --project .\LocalAgent\LocalAgent.csproj -- pipeline.yml --source Samples\WebApplication1 --work Samples\work --id 2
```

Plain-language argument meaning:

- `<yaml>`: the pipeline YAML file path (required)
- `--source`: (Optional) the folder that contains the code. If omitted, uses the directory of the yaml file.
- `--work`: where LocalAgent creates its working copy and outputs

### Example: run in place

Use in-place mode when you explicitly want the pipeline to operate directly in the source folder instead of cloning into a work folder.

```cmd
dotnet run --project .\LocalAgent\LocalAgent.csproj -- Samples\ConsoleApp1 pipeline.yml --inplace true
```

Use `--inplace true` only when you intentionally want the pipeline to work directly in the source directory.

## Command Line options

```txt
  --build            (Default: ${Agent.WorkFolder}/${Agent.Id}) Agent.BuildDirectory - The local path on the agent where all folders for a given build pipeline are created. This variable has the same value as Pipeline.Workspace. For example /home/vsts/work/1

  --id               (Default: 1) Agent.Id - The Id of the Agent

  --name             (Default: LocalAgent) Agent.Name - The name of the agent that is registered with the pool. If you are using a self-hosted agent, then this name is specified by you.

  --tmp              (Default: ${Agent.UserProfileDirectory}/.temp) Agent.TempDirectory - A temporary folder that is cleaned after each pipeline job. This directory is used by tasks such as .NET Core CLI task to hold temporary items like test results before they are published.

  --work             (Default: ${Agent.UserProfileDirectory}/work) Agent.WorkFolder - The working directory for this agent. For example: c:\agent_work

  --def              (Default: dev) Build.DefinitionName - Alias of build, For example. dev

  --nuget            (Default: ../nuget) Folder used to store Nuget Packages for use by the pipeline

  --inplace          (Default: false) If true, the build does not occur in a work folder, but instead builds in the source folder

  --dry-run          (Default: false) Parse and evaluate pipeline flow/conditions without executing task runners or external tools

  --help             Display this help screen.

  --version          Display version information.

  --source           (Optional) Source Path: The absolute or relative path to the source folder. If omitted, uses the directory of the yaml file.

  yml (pos. 0)       Required. YAML Path: The path to the yaml pipeline file. Acts as the entry point for the pipeline build process.
```

## Service usage

`LocalAgent.Service` hosts triggers and invokes the CLI runner model internally by building a `PipelineOptions` instance and running one pipeline at a time.

### Run the service

```cmd
dotnet run --project .\LocalAgent.Service\LocalAgent.Service.csproj
```

By default the service reads:

- pipeline definitions from `LocalAgent.Service/appsettings.json`
- inline trigger configuration from the `Triggers` section in `LocalAgent.Service/appsettings.json`
- additional trigger files from `LocalAgent.Service/.triggers/*.json`

### Service configuration example

The service maps named pipelines and trigger definitions. Example:

```json
{
  "Service": {
    "Http": {
      "Urls": ["http://0.0.0.0:5071"]
    },
    "AllowConcurrentRuns": false
  },
  "Pipelines": [
    {
      "Name": "WebApplication1",
      "SourcePath": "Samples/WebApplication1",
      "YamlPath": "pipeline.yml",
      "AgentId": 2,
      "AgentWorkFolder": "Samples/work",
      "BuildDefinitionName": "dev",
      "NugetFolder": "../nuget",
      "BuildInplace": false
    }
  ],
  "Triggers": [
    {
      "Type": "cron",
      "Name": "webapp1-interval",
      "Pipeline": "WebApplication1",
      "Cron": "0 * * * *"
    }
  ]
}
```

The `Pipelines` section gives each pipeline a friendly name. Triggers reference that name instead of repeating all pipeline settings.

### Versioned `.triggers` file example

Trigger files can now use a versioned schema. Place them under `LocalAgent.Service/.triggers`.

```json
{
  "version": 1,
  "triggers": [
    {
      "type": "webhook",
      "name": "webapp1",
      "pipeline": "WebApplication1",
      "provider": "github",
      "path": "/webhooks/webapp1",
      "secret": "changeme",
      "allowedEvents": ["push"]
    },
    {
      "type": "cron",
      "name": "webapp1-interval",
      "pipeline": "WebApplication1",
      "cron": "0 * * * *"
    },
    {
      "type": "file",
      "name": "webapp1-files",
      "pipeline": "WebApplication1",
      "watchPath": "Samples/WebApplication1",
      "include": ["**/*.cs", "**/*.csproj", "**/*.yml", "**/*.yaml"],
      "exclude": ["**/bin/**", "**/obj/**"],
      "recursive": true,
      "debounceSeconds": 3
    }
  ]
}
```

This is the preferred format for new trigger files.

### Trigger validation behavior

The service validates trigger configuration at startup and fails fast with clear diagnostics when:

- the trigger schema version is unsupported
- a trigger references an unknown pipeline
- a cron expression is invalid
- a trigger type is unknown
- required fields such as `type`, `name`, or `pipeline` are missing

If validation fails, the service stops at startup instead of silently ignoring broken trigger configuration.

### File watch trigger example

When a watched file changes, the service debounces the event and runs the configured pipeline:

```json
{
  "type": "file",
  "name": "console-files",
  "pipeline": "ConsoleApp1",
  "watchPath": "Samples/ConsoleApp1",
  "include": ["**/*.cs", "**/*.csproj", "**/*.yml"],
  "exclude": ["**/bin/**", "**/obj/**"],
  "recursive": true,
  "debounceSeconds": 2
}
```

### Webhook example

For webhook triggers, configure a path and optional shared secret.

Example request:

```cmd
curl -X POST http://localhost:5071/webhooks/webapp1 ^
  -H "X-LocalAgent-Secret: changeme" ^
  -H "Content-Type: application/json" ^
  -d "{}"
```

For GitHub webhooks, when a secret is configured the service also validates `X-Hub-Signature-256`. Allowed events can be restricted with `allowedEvents`.

### Health endpoint

The service exposes a basic health endpoint:

```cmd
curl http://localhost:5071/health
```

## Static Analysis Tools

LocalAgent includes a `StaticAnalysis@1` task that downloads and runs analysis tools locally against your source tree.

```yaml
steps:
  - task: StaticAnalysis@1
    displayName: Security baseline
    inputs:
      tools: 'horusec;trufflehog;dependency-check'
      workingDirectory: '$(Build.SourcesDirectory)'
```

See [docs/static-analysis.md](docs/static-analysis.md) for tool descriptions, file and data-flow details, per-tool examples, and starter combinations.

## Troubleshooting

### Pipeline file not found

Check that:

- `source` points to the correct project folder
- `yaml` is relative to that source folder

### Service fails at startup

Check for:

- unsupported trigger schema version
- trigger referencing a pipeline name that does not exist
- invalid cron expression
- missing required trigger fields

### Build works once but not in service mode

Check that:

- the pipeline exists in `Pipelines`
- the trigger references the exact pipeline name
- service paths are correct relative to `LocalAgent.Service`

### Task fails unexpectedly

Check whether the pipeline requires an external tool that is not installed locally, such as Docker or MSBuild.

## Secrets and logging

LocalAzureAgent masks any values registered as secrets before writing to logs (including evaluated variable output). To mark a value as secret, emit a `task.setvariable` command with `isSecret=true` from your scripts:

```txt
##vso[task.setvariable variable=ApiKey;isSecret=true]supersecret
```

Notes:
- Secrets are masked as `********` in log output.
- Avoid echoing secrets directly; set them as secret variables instead.

## Samples

### Console Application

```cmd
dotnet run --project .\LocalAgent\LocalAgent.csproj -- Samples\ConsoleApp1 pipeline.yml --work Samples\work --id 1
```

### Web Application

```cmd
dotnet run --project .\LocalAgent\LocalAgent.csproj -- Samples\WebApplication1 pipeline.yml --work Samples\work --id 2
```

### Windows Form Application

```cmd
dotnet run --project .\LocalAgent\LocalAgent.csproj -- Samples\WindowsFormsApp1 pipeline.yml --work Samples\work --id 3
```