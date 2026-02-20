# LocalAzureAgent

A simple build agent, which can run on a local .Net Core host and build source code using a Azure DevOps compliant yaml file.

The aim of the project is to assist the local validation of build yaml scripts prior to committing to Azure DevOps repos. 

The built utility is suitable for one hit compilation as a command line utility, or alternatively, can be installed and run as service, and continuously executing the pipeline.

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
  - ExtractFiles
  - MSBuild
  - NuGetCommand
  - NuGetToolInstaller
  - Powershell
  - PublishBuildArtifacts
  - ReplaceTokens
  - UpdateAssemblyInfo
  - VSTest

## Build

```cmd
dotnet build
```

## Test

```cmd
dotnet test
```

## Run

### Run via dotnet cli

```cmd
dotnet run --project --project .\LocalAgent\LocalAgent.csproj  <source> <yaml> <options>
```

### Run via executable

```cmd
LocalAgent.exe <source> <yaml> <options>
```

## Command Line options

```txt
  --build            (Default: ${Agent.WorkFolder}/${Agent.Id}) Agent.BuildDirectory - The local path on the agent where all folders for a given build pipeline are created. This variable has the same value as Pipeline.Workspace. For example /home/vsts/work/1

  --id               (Default: 1) Agent.Id - The Id of the Agent

  --name             (Default: LocalAgent) Agent.Name - The name of the agent that is registered with the pool. If you are using a self-hosted agent, then this name is specified by you.

  --tmp              (Default: ${Agent.WorkFolder}/temp) Agent.TempDirectory - A temporary folder that is cleaned after each pipeline job. This directory is used by tasks such as .NET Core CLI task to hold temporary items like test results before they are       
                     published.

  --work             (Default: ${Agent.EntryFolder}/work) Agent.WorkFolder - The working directory for this agent. For example: c:\agent_work

  --daemon           (Default: false) True - Run as Windows Service, False - Run then exit immediately

  --def              (Default: dev) Build.DefinitionName - Alias of build, For example. dev

  --nuget            (Default: ../nuget) Folder used to store Nuget Packages for use by the pipeline

  --help             Display this help screen.

  --version          Display version information.

  source (pos. 0)    Required. Source Path: The absolute or relative path to the source folder, which will be cloned into the agent work folder

  yml (pos. 1)       Required. Relative YAML Path: The path to the yaml pipeline file, which acts as the entry point for the pipeline build process
```

## Samples

### Console Application

```cmd
cd Samples
..\LocalAgent\bin\Debug\net9.0\LocalAgent.exe ConsoleApp1 pipeline.yml --work "work"  --id 1
```

### Web Application

```cmd
cd Samples
..\LocalAgent\bin\Debug\net9.0\LocalAgent.exe WebApplication1 pipeline.yml --work "work"  --id 2
```

### Windows Form Application

```cmd
cd Samples
..\LocalAgent\bin\Debug\net9.0\LocalAgent.exe WindowsFormsApp1 pipeline.yml --work "work" --id 3
```