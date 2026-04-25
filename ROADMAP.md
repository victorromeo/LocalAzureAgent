# LocalAzureAgent Roadmap

Goal: improve Azure DevOps pipeline simulation fidelity while keeping LocalAgent safe for local execution.

## High Priority

1. Fix variable precedence/evaluation ordering
- Ensure pipeline/stage/job/step/runtime precedence matches Azure behavior.
- Current evaluation order is likely incorrect and can cause subtle script/task argument expansion bugs.

2. Implement template reference execution
- Replace runtime NotImplementedException for job templates with real template loading/expansion.
- Add stage and step template execution support as part of one coherent template subsystem.

3. Implement step-level continueOnError behavior
- Apply `continueOnError` at step scope, not just job scope.
- Align pipeline status propagation with Azure `SucceededWithIssues` semantics.

4. Wire native non-task step types
- Add concrete runner paths for `bash`, `powershell`, and `checkout` step forms.
- Keep behavior consistent with existing task runners and variable/secret handling.

5. Remove process-global working directory mutation in static analysis
- Stop mutating `Environment.CurrentDirectory` during tool runs.
- Use process-level `WorkingDirectory` to avoid concurrency and cross-run contamination.

## Medium Priority

1. Surface service pipeline run outcomes
- Return/record explicit run result metadata (status, duration, warnings, errors, first error).
- Avoid always returning accepted without an observable run result contract.

2. Enforce timeouts
- Implement `timeoutInMinutes` and `cancelTimeoutInMinutes` for jobs/steps.
- Terminate process trees safely on timeout and map status correctly.

3. Propagate trigger source into build metadata
- Set `Build.Reason` based on trigger type (`Manual`, `Schedule`, `Webhook`, `IndividualCI`/file-trigger equivalent).
- Improve parity for conditions, logs, and diagnostics.

4. Improve source path resolution robustness
- Honor rooted/absolute source paths directly.
- Prevent incorrect path composition in CLI/service execution modes.

5. Export evaluated variables to child process environments
- Provide predictable environment projection for scripts/tools that rely on env vars.
- Preserve secret masking protections in logs.

## Low Priority

1. Harden webhook secret comparison and guidance
- Use constant-time comparison patterns for shared-secret checks.
- Expand secure secret storage guidance for local and team setups.

2. Expand webhook provider parity
- Add provider-specific handling beyond current baseline (GitHub-first support).
- Include common event header conventions and signature schemes.

## Easy Product Extensions

1. Add service run history endpoint
- Expose last N runs with trigger source, pipeline name, status, and durations.

2. Add compatibility matrix documentation
- Publish supported YAML constructs and task behaviors with parity notes.

3. Add offline tool mode for static analysis
- Allow strict no-network mode with pre-provisioned tool caches and explicit failures.

## Recently Completed

1. Condition evaluation in normal and dry-run execution
- Job and step `condition` expressions now gate execution in both modes.
- Dry-run and normal execution share the same evaluator.

2. Dry-run/plan mode
- Added `--dry-run` CLI option to parse and evaluate pipeline flow/conditions without executing external tools.
- Added docs and tests for dry-run and condition behavior.

## Suggested Execution Order

1. Correctness blockers: variable precedence, template execution, step continueOnError.
2. Runtime parity: native step types, timeout enforcement, per-process working directory safety.
3. Service productization: run outcomes, build reason mapping, path robustness, env projection.
4. Security and ecosystem: secret hardening, provider parity, offline support, docs matrix.

## Done Criteria

- Core YAML execution paths no longer hit NotImplementedException for supported constructs.
- Job/step flow control (`dependsOn`, `continueOnError`, timeouts) behaves close to Azure expectations.
- Service-triggered runs expose clear outcomes and metadata.
- Safety guarantees remain intact for local execution (path guards, process isolation, secret handling).
