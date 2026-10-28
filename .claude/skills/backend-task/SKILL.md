---
name: backend-task
description: >
  Orchestrate spec-driven backend development tasks using OpenSpec
  and the architect, dotnet-developer, and code-reviewer subagents.
  Use for implementing features, fixing bugs, and refactoring
  C#/.NET backend applications. Run without arguments to resume
  an in-progress change.
disable-model-invocation: true
---

# Backend Development Workflow (OpenSpec)

You are the MAIN ORCHESTRATOR.

Your responsibility is to coordinate specialized subagents
to complete the user's backend development task, using an
OpenSpec change as the source of truth.

User request:
$ARGUMENTS

## General Rules

- Use existing project-level subagents: `architect`,
  `dotnet-developer`, `code-reviewer`.
- Do not create additional agents.
- Do not modify the subagent definitions.
- Do not perform implementation directly as the main agent.
  The main agent writes only OpenSpec artifacts and
  `workflow-state.md`; application code and tests are
  changed only by `dotnet-developer`.
- Delegate work to the appropriate subagent.
- Execute stages sequentially.
- Wait for each stage to finish before starting the next.
- Do not skip mandatory stages.
- Do not create Git commits.
- Do not deploy applications.
- Do not modify production credentials.
- Do not execute destructive database operations.
- Follow existing project conventions.
- Keep changes focused on the requested task.

## OpenSpec Usage

Use the installed OpenSpec skills and CLI. Do not
reimplement their logic in this workflow.

| Purpose | Installed capability |
|---|---|
| Create change and artifacts | `openspec-propose` skill (`/opsx:propose`) |
| Revise artifacts | `openspec-update-change` skill (`/opsx:update`) |
| Implementation instructions | `openspec instructions apply --change "<name>" --json` |
| Verify implementation | `openspec-verify-change` skill (`/opsx:verify`) |
| Archive | `openspec-archive-change` skill (`/opsx:archive`) |
| Status / listing | `openspec status --change "<name>" --json`, `openspec list --json` |
| Structural validation | `openspec validate "<name>" --strict` |

Subagent tool limits:
- `architect` and `code-reviewer` are read-only (Read, Grep,
  Glob). They cannot run the OpenSpec CLI or Git. Give them
  file paths to read, and save any Git diff they need to a
  temporary file outside the repository.
- `dotnet-developer` can run the OpenSpec CLI and edit files.

## Workflow State

Each change keeps its orchestration state in:

`openspec/changes/<change-name>/workflow-state.md`

OpenSpec ignores this file during validate and status, and it
moves with the change when archived. Keep it separate from
`proposal.md` and the other artifacts. Update it at the end of
every stage and whenever the user approves or decides something.

Template:

```markdown
# Workflow State: <change-name>

- Request: <original user request>
- Current stage: <0-7 and name>
- Status: in-progress | awaiting-approval | blocked | done
- Plan approved: no | yes (<date>, <what was approved>)
- Correction cycles used: 0/2
- Archive approved: no | yes (<date>)

## Decisions
- <date>: <decision and who made it>

## Stage Log
- <date> Stage N: <outcome, files, build/test results>

## Next Step
<exactly what to do when resuming>
```

## Stage 0: Start or Resume

Always run this stage first.

1. Run `openspec list --json` and read `root`.
   If `root` is null, stop and tell the user that OpenSpec
   is not initialized. Do not run `openspec init` yourself.
2. If `$ARGUMENTS` is empty, resume:
   - List active changes. If there is exactly one, select it;
     otherwise ask the user which change to resume.
   - Read its `workflow-state.md` and run
     `openspec status --change "<name>" --json`.
   - Continue from the recorded "Next Step". Re-confirm any
     pending approval with the user; never treat an approval
     as given unless `workflow-state.md` records it.
   - If `workflow-state.md` is missing, infer the stage from
     `openspec status` and `tasks.md` checkboxes, then ask the
     user to confirm before continuing.
3. If `$ARGUMENTS` contains a request, check for duplicates
   before creating anything:
   - Compare the request against active change names and
     their `proposal.md` and `workflow-state.md`.
   - If a matching change exists, STOP and ask the user
     whether to resume it or create a new change.
     Never create a duplicate without the user's answer.
   - Never overwrite or reuse an existing change directory
     for a different request.

## Stage 1: OpenSpec Proposal

Use the `openspec-propose` skill with the user's request.

- Choose a new, unused kebab-case change name.
- Generate all required artifacts (`proposal.md`, delta
  specs, `design.md`, `tasks.md`) following
  `openspec instructions <artifact> --change "<name>" --json`.
- Do not modify application source code.
- Create `workflow-state.md` from the template.
- Run `openspec validate "<name>" --strict` and fix
  artifact issues until it passes.

## Stage 2: Architect Review

Delegate to the `architect` subagent.

Provide:
- Original user request.
- Paths to the change's artifacts.

Tasks:
1. Review `proposal.md`, `design.md`, `tasks.md` and the
   delta specs.
2. Inspect relevant existing code.
3. Identify affected components.
4. Determine the root cause if fixing a bug.
5. Identify risks, missing requirements, and
   architectural inconsistencies.
6. Recommend the simplest appropriate solution and minimal
   artifact changes.
7. Recommend necessary tests.

The architect must not modify files.

Wait for the architect to finish.

Apply the architect's recommended artifact changes yourself
using the `openspec-update-change` skill. Then run
`openspec validate "<name>" --strict` again.

Present the final plan to the user: summary, scope,
out-of-scope items, risks, tests, and files expected to change.

Set `workflow-state.md` to `awaiting-approval`.

STOP and wait for explicit user approval
before proceeding to implementation.
Record the approval and any decisions in `workflow-state.md`.

## Stage 3: Implementation

After the user approves the plan:

Delegate to the `dotnet-developer` subagent.

Provide:
- Original user request.
- Change name and artifact paths.
- Architect's findings and the user's decisions.
- Relevant constraints and safety rules.

Tasks:
1. Run `openspec instructions apply --change "<name>" --json`
   and follow it. The approved artifacts are the source of truth.
2. Inspect the relevant source code.
3. Implement the tasks from `tasks.md`.
4. Mark each completed task `- [x]` in `tasks.md`.
5. Follow existing coding conventions.
6. Add or update appropriate tests.
7. Keep modifications minimal and avoid unrelated refactoring.
8. If the artifacts are wrong or incomplete, stop and report
   instead of improvising.

Validation:
- Run dotnet build when safe.
- Run unit tests that do not require external services.
- Do not run integration tests against external databases
  without explicit approval.
- Do not run destructive database operations.
- Report every test that was added but not executed.

Wait for implementation to finish.

## Stage 4: Independent Code Review

Save the Git diff of the changed files to a temporary file
outside the repository.

Delegate to the `code-reviewer` subagent.

Provide:
- Original requirements.
- Paths to the approved OpenSpec artifacts.
- List of modified files and the diff file path.

Tasks:
1. Independently inspect the implementation.
2. Review the Git diff.
3. Verify correctness against the approved specs and requirements.
4. Identify potential bugs.
5. Check performance and security.
6. Identify missing tests.
7. Report potential regressions.

The reviewer must not modify files.

Classify findings:
- Critical
- High
- Medium
- Low

Wait for review to finish.

### Review Resolution

If Critical or High issues are found:

1. Delegate fixes to `dotnet-developer`.
2. Provide the review findings.
3. Keep fixes within the approved scope.
4. Delegate another review to `code-reviewer`.
5. Update the correction cycle count in `workflow-state.md`.

Maximum automatic correction cycles: 2.

If issues remain unresolved after two cycles:
- Stop.
- Report unresolved findings.
- Ask the user how to proceed.

For Medium or Low issues:
- Report findings.
- Do not automatically expand the scope.

## Stage 5: OpenSpec Verification

Use the `openspec-verify-change` skill for the change.

- Check completeness, correctness, and coherence.
- Ensure the implementation matches the specifications.
- Run `openspec validate "<name>" --strict`.
- Report unresolved issues. Do not fix them automatically;
  fixes go through Stage 3 and Stage 4 with user approval.

## Stage 6: Final Report

Provide:

1. Summary of implemented changes.
2. Modified files.
3. Completed OpenSpec tasks (and any incomplete ones).
4. Tests added or updated.
5. Build and test results.
6. Code review findings.
7. OpenSpec verification results.
8. Remaining risks.
9. Recommended next steps.

Never claim tests passed unless they were executed.

Do not create a Git commit.

## Stage 7: Archive

- Never archive automatically.
- Ask the user for explicit approval to archive.
- After approval, record it in `workflow-state.md` and use
  the `openspec-archive-change` skill.
- Report the archive location and synced specs.

## Safety Rules

- Never deploy automatically.
- Never create Git commits automatically.
- Never execute destructive database operations
  (for example `DeleteAllData`, `DROP`, `TRUNCATE`, or
  `docker compose down -v`) without explicit approval.
- Never run integration tests against an existing database
  without explicit approval.
- Before any approved integration test, verify that the
  connection targets the project's own local disposable test
  container, not another service on the same port.
- Run approved integration tests with exact test-name filters
  so that workload or benchmark tests cannot run by accident.
- Never modify production credentials.
- Preserve existing working behavior.
- Do not overwrite existing OpenSpec changes.
- Never archive an OpenSpec change without explicit approval.
- Do not modify application source code outside Stage 3
  and the review-resolution loop.
