# Issue intake and delivery gates

[Client board](https://github.com/users/berniemackie97/projects/4) |
[Server board](https://github.com/users/berniemackie97/projects/5) |
[Client roadmap](https://github.com/berniemackie97/OpenConquerClient/issues/258) |
[Server roadmap](https://github.com/berniemackie97/OpenConquerServer/issues/205)

## Choose one issue class

| Class | Use when | Completion evidence |
| --- | --- | --- |
| Epic | An outcome spans multiple child issues | All release exit criteria evidenced, not merely child PRs |
| Feature | A capability needs distinct behavior contracts | Observable success, refusal and recovery acceptance |
| Story | One player or operator scenario has a testable result | Scenario passes on the required platforms |
| Task | A bounded implementation or governance change is understood | Linked change, tests, documentation, review and CI |
| Spike | Behavior, ownership or a design choice is unresolved | A dated finding with sources, counterexamples and decision |
| Bug | A concrete defect has been reproduced | Reproduction fails before the fix and passes after it |

Choose a Spike, not a Bug, for a suspected incompatibility without reproduction.
An issue is not completed by implementation alone if its runtime acceptance is
unverified. Use cross-repository links for shared contracts rather than creating
two canonical tickets for the same decision.

## Intake and tracking

Each repository's issue forms apply the existing `type:*` label and request
specific evidence. The templates also request addition to the corresponding
owner-level GitHub Project. This requires the submitting account to have write
access to that project; the auto-add workflow is the alternative for other
contributors. Do not treat either mechanism as proof that every item is on
the board.

Assign the correct project status, Stage (R1–R5), Priority (P0–P3) and
Workstream. Assign the repository release milestone when the work has a
known release gate. Forms do not set custom Projects fields automatically
and no default priority is inferred from the issue class. Parent/child issue
relationships and cross-repository blockers must remain visible in GitHub.

## Engineering transition

Before implementation, audit current code and source evidence. Record what was
observed, what was derived, and what remains unverified. Scope the work to
existing owners and avoid duplicate parsers, redundant wrappers, speculative
options and commentary that repeats code.

An issue moves to Done only after its own acceptance is met, relevant pull
requests have been reviewed and merged, and required tests, formatting,
builds, documentation, security and adverse-path checks have passed. CI
results are always tied to the final reviewed commit. If evidence requires
external hardware, private game content or a controlled executable probe,
keep that gate open until it is actually run. Do not substitute a synthetic
fixture or agreement between both repositories for independent observation.

Do not automatically merge PRs or close parent epics when a partial slice
merges. Review comments should be resolved through code changes without
thread chatter unless clarification is required.

## Pull requests

The PR template records scope, evidence, verification and unresolved gates.
Include the canonical issue and companion PR when a change crosses both
repositories. Reviewers should be able to distinguish completed checks from
checks not run. Security-sensitive samples must not contain credentials,
tokens, encryption keys, machine identifiers or private player data.
