# Condition Evaluation Guidance

This document explains the current condition-evaluation behavior in LocalAgent and how to use it safely while Azure DevOps parity is still being expanded.

## Summary

LocalAgent enforces condition evaluation in both normal and dry-run modes.

- Normal execution (`--dry-run false`, default):
  - YAML `condition` expressions are evaluated and used to gate execution.
  - If the condition evaluates to false, the job/step is skipped and its runner is not executed.
- Dry-run (`--dry-run true`):
  - pipeline graph is parsed and walked
  - step runners and external tools are not executed
  - the same condition evaluator is used, so gating decisions should match normal execution

## Supported Condition Functions

- `succeeded()`
- `failed()`
- `always()`
- `canceled()`
- `and(...)`
- `or(...)`
- `not(...)`
- `eq(...)`
- `ne(...)`

Notes:

- Nested `and/or/not/eq/ne` expressions are supported.
- If an expression is not recognized, LocalAgent currently logs a warning and treats it as `true`.

## Practical Advice

- Use dry-run first when introducing or changing conditions.
- Keep conditions explicit and testable (for example: `and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))`).
- Avoid relying on advanced Azure expression features unless you have verified behavior locally and in Azure DevOps.
- Use full execution after dry-run to validate task-level side effects and actual command outcomes.

## Example

```yaml
steps:
  - task: CmdLine@2
    displayName: Only on main
    condition: and(succeeded(), eq('refs/heads/main', 'refs/heads/main'))
    inputs:
      script: echo "hello"
```

Run dry-run:

```cmd
dotnet run --project .\LocalAgent\LocalAgent.csproj -- <source> <yaml> --dry-run true
```

## Recommended Workflow

1. Validate parse/graph and conditions using dry-run.
2. Run normal execution to validate actual task behavior.
3. If Azure DevOps behavior differs, simplify condition expressions and re-test.
