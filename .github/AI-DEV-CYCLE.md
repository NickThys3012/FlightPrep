# 🤖 AI-Assisted Dev Cycle — BalloonPrep

A practical guide on how to use **GitHub Copilot CLI** across all 4 phases of the development cycle on this project.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Overview: The 4 Cycles](#overview-the-4-cycles)
3. [Cycle 1 — Planning & Analysis](#cycle-1--planning--analysis)
4. [Cycle 2 — Implementation](#cycle-2--implementation)
5. [Cycle 3 — Code Review](#cycle-3--code-review)
6. [Cycle 4 — Testing](#cycle-4--testing)
7. [Custom Agent Instructions](#custom-agent-instructions)
8. [Slash Command Cheatsheet](#slash-command-cheatsheet)
9. [Workflow: End-to-End Example](#workflow-end-to-end-example)

---

## Prerequisites

### Install GitHub Copilot CLI

```bash
# macOS / Linux
curl -fsSL https://gh.io/copilot-install | bash

# macOS via Homebrew
brew install copilot-cli

# Windows via WinGet
winget install GitHub.Copilot
```

### Launch & Authenticate

```bash
cd /path/to/BalloonPrep
copilot
```

On first launch, run `/login` and follow the prompts. You need an active GitHub Copilot subscription.

### Modes

| Mode | How to activate | What it does |
|------|----------------|-------------|
| **Interactive** | Default | Chat back and forth with Copilot |
| **Plan** | `Shift+Tab` | Copilot creates a plan before writing any code |
| **Autopilot** | `Shift+Tab` again | Copilot works uninterrupted until done |

---

## Overview: The 4 Cycles

```
┌─────────────────────────────────────────────────────────────┐
│                    AI Dev Cycle                             │
│                                                             │
│  1. Planning       2. Implementation   3. Review            │
│  /plan mode        Autopilot mode      /review command      │
│  explore agent     general-purpose     code-review agent    │
│       │                  │                   │              │
│       └──────────────────┴───────────────────┘              │
│                          │                                   │
│                   4. Testing                                 │
│                   task agent                                 │
│                   test generation prompts                    │
└─────────────────────────────────────────────────────────────┘
```

Each cycle uses a different **agent type** — lightweight specialised sub-processes that Copilot CLI can spawn in parallel. Agents run in separate context windows so they stay fast and focused.

| Agent Type | Best for | Speed |
|-----------|---------|-------|
| `explore` | Reading code, answering questions, analysis | ⚡ Fast |
| `task` | Running builds, tests, linters | ⚡ Fast |
| `code-review` | High-signal bug & security review | ⚡ Fast |
| `general-purpose` | Complex multi-file implementations | 🐢 Thorough |

---

## Cycle 1 — Planning & Analysis

**Goal:** Understand the codebase, identify problems, and create a structured backlog before writing a single line of code.

### How to use it

#### Option A — Conversational (recommended for first-time analysis)

Switch to **Plan mode** with `Shift+Tab`, then describe what you want:

```
Analyse the BalloonPrep codebase. I want to:
- Understand what it does and how it is structured
- Find bugs, missing test coverage, and security issues
- Produce a prioritised backlog of improvements
```

Copilot will explore the code, summarise findings, and generate a `plan.md` for you to review and edit before implementation starts.

#### Option B — Targeted exploration prompts

Use these prompts directly in interactive mode:

```
# Understand a specific subsystem
Explain how TrajectoryService.ComputeAsync works step by step.

# Find risks before adding a feature
What are the risks of adding multi-pilot support to FlightPreparation?

# Identify gaps
What is completely untested in this codebase? List files with no test coverage.

# Architecture review
Does this project follow clean architecture principles? 
Identify any violations of the dependency rule.
```

#### Option C — Background explore agent (for parallel analysis)

The CLI can run multiple exploration agents simultaneously. Useful when you have several independent questions:

```
# In the CLI, you can ask Copilot to fan out to parallel agents
Investigate these two things in parallel:
1. How does the Skeyes bulletin scraper work and where could it break?
2. What validation is missing from the trajectory parameter inputs?
```

### What to look for in Planning

| Area | Questions to ask Copilot |
|------|--------------------------|
| **Architecture** | "Does this follow DDD correctly? Where are the layer violations?" |
| **Security** | "Are there hardcoded secrets, exposed endpoints, or SQL injection risks?" |
| **Test coverage** | "Which files have zero test coverage? Which have the most risk?" |
| **Performance** | "Are there N+1 queries or blocking async calls?" |
| **Edge cases** | "What happens if the Open-Meteo API is down? What is the fallback?" |

### Output — GitHub Issues (automatic)

After analysis, the planning agent automatically creates one GitHub issue per finding in [`NickThys3012/FlightPrep`](https://github.com/NickThys3012/FlightPrep/issues) using the `gh` CLI. Each issue includes:
- Exact file + line reference
- Severity label (`bug`, `security`, `testing`, `enhancement`)
- A concrete suggested fix

You can also trigger this explicitly:

```
Analyse the codebase for bugs and security issues, then create a GitHub issue
for each finding in NickThys3012/FlightPrep with the appropriate labels.
```

Reference issue numbers in all commits and PRs:
```bash
git commit -m "fix: guard zero ascent rate in TrajectoryService (#12)"
```

Or save a sprint plan directly to the repo:

```
/plan
Create a sprint plan for the next 2 weeks based on open GitHub issues.
Save it to .github/SPRINT.md
```

---

## Cycle 2 — Implementation

**Goal:** Write, fix, or refactor code with AI assistance. Copilot handles the repetitive scaffolding so you focus on the logic.

### How to use it

#### For a single well-defined task

Just describe it in interactive mode:

```
Fix the divide-by-zero bug in TrajectoryService.ComputeAsync.
Add guards for ascentRateMs <= 0 and descentRateMs <= 0 that log an error and return (null, null).
```

#### For a large multi-file change — use Autopilot

Switch to **Autopilot mode** (`Shift+Tab` twice) before describing complex tasks:

```
Refactor all repository classes to use async/await consistently.
Make sure all methods that currently use .Result or .Wait() are properly awaited.
Run the build after to verify there are no errors.
```

Autopilot will work through the changes without interrupting you for confirmation on every step.

#### For changes that should become a PR — use /delegate

```
/delegate
Move all hardcoded credentials out of docker-compose.yml into a .env file.
Create a .env.example with placeholder values.
Update the README to document the new setup step.
```

This sends the task to GitHub's cloud agent, which opens a Pull Request for you to review.

### Implementation prompts for this project

```
# Add a new feature
Add a field to FlightPreparation for ChaseTeamPhoneNumber (string, max 20 chars).
Include it in the PDF output under Section 4 (Safety & Communication).

# Refactor
The SkeyesBulletinService has 9 empty catch blocks. 
Replace them all with catch (Exception ex) { _logger.LogDebug(ex, "..."); }

# Scaffold boilerplate
Generate a new CQRS command UpdatePaxBriefingCommand following the same
pattern as UpdateFlightPrepCommand. Include handler, validator, and wire it up in DI.

# Fix a bug
In FlightEdit.razor.cs, the _insertedId field can go stale on a retry.
Analyse the save flow and propose a fix.
```

### Tips

- Always **create a new branch** before letting Copilot make changes: `git checkout -b feature/my-change`
- Use **`/diff`** after implementation to review everything before committing
- Use **`/rewind`** to undo the last turn and revert file changes if something went wrong

---

## Cycle 3 — Code Review

**Goal:** Catch bugs, security issues, and logic errors before they reach `main`. AI review is fast, tireless, and has no ego.

### How to use it

#### Option A — Review staged/uncommitted changes

```
/review
```

This runs the `code-review` agent on your current unstaged + staged changes. It only surfaces issues that matter — bugs, security vulnerabilities, logic errors. It will not comment on style.

#### Option B — Review a specific file or PR

```
/review
Review src/BalloonPrep.Infrastructure/ExternalServices/TrajectoryService.cs
Focus on: null safety, edge cases in the simulation loop, and API error handling.

/pr review
Review the open PR on this branch for security issues and breaking changes.
```

#### Option C — Targeted review prompts

```
# Security audit
Check docker-compose.yml and all appsettings*.json for hardcoded secrets or 
credentials that should not be committed to source control.

# Data integrity
Can a FlightPreparation be saved in an inconsistent state? 
List all paths where required fields could be null or invalid at save time.

# API contract review
Does TrajectoryService correctly handle all Open-Meteo API error responses?
What happens on a 429 (rate limit) or a 503?
```

### What the code-review agent checks

The `code-review` agent intentionally skips style/formatting and only flags:

- 🔴 **Bugs** — logic errors, off-by-one, wrong conditions
- 🔴 **Security** — hardcoded secrets, injection risks, missing auth
- 🟡 **Null/exception safety** — unguarded `.First()`, missing null checks
- 🟡 **Data integrity** — missing validation, potential corrupt state
- 🟡 **Silent failures** — empty catch blocks, swallowed exceptions

### Review output interpretation

| Severity | Meaning | Action |
|----------|---------|--------|
| 🔴 High | Real bug or security issue | Fix before merging |
| 🟡 Medium | Risk or fragile code | Fix or document as known debt |
| ℹ️ Info | Worth knowing | Decide per case |

---

## Cycle 4 — Testing

**Goal:** Add test coverage for critical logic. AI generates tests faster than writing them by hand, and it won't forget the edge cases.

### How to use it

#### Generate tests for a class

```
Generate xUnit tests for FlightPreparation.cs.
Cover: AddPassenger (happy path, empty name, zero weight), 
RemovePassenger (found, not found), IsWithinWeightLimit (within, over, no balloon).
Place the tests in tests/BalloonPrep.Tests/Domain/FlightPreparationTests.cs
```

#### Generate tests for a service

```
Generate unit tests for TrajectoryService.ComputeAsync using Moq to mock HttpClient.
Cover these cases:
1. ascentRateMs = 0 → returns (null, null)
2. descentRateMs = 0 → returns (null, null)  
3. API returns empty timestamps → returns (null, null)
4. Happy path with mocked wind data → returns (model, points)
```

#### Run the tests

```
Run the test suite and show me only failures with their stack traces.
```

Or use the `task` agent for clean output:

```
dotnet test tests/BalloonPrep.Tests --logger "console;verbosity=minimal"
```

#### TDD workflow

Switch to **Plan mode** first:

```
I want to add input validation to the trajectory parameters in FlightEdit.razor.cs.
Write the failing tests first (TDD), then implement the validation to make them pass.
```

### Test generation prompts for this project

```
# Value object tests
Write tests for Coordinate.cs covering valid inputs, lat out of range, lon out of range, 
and record equality.

# Domain event tests  
Verify that FlightPreparation.Create() raises exactly one FlightPreparationCreatedEvent
and that UpdateWeather() raises a WeatherUpdatedEvent with the correct properties.

# Integration test skeleton
Generate an integration test that uses an in-memory SQLite database to test 
FlightPreparationRepository.CreateAsync followed by GetByIdAsync.

# Regression test from a bug
TrajectoryService previously crashed with divide-by-zero when ascentMs was 0.
Write a regression test that proves the fix works.
```

---

## Custom Agent Instructions

You can teach Copilot about your project so every agent starts with the right context. Instructions are picked up automatically from these files:

| File | Scope |
|------|-------|
| `.github/copilot-instructions.md` | Global project context |
| `.github/instructions/planning.instructions.md` | Planning-specific rules |
| `.github/instructions/implementation.instructions.md` | Implementation rules |
| `.github/instructions/review.instructions.md` | Review checklist |
| `.github/instructions/testing.instructions.md` | Test conventions |
| `AGENTS.md` (repo root) | Shared instructions for all agents |

These files are **already set up** for BalloonPrep — see `.github/instructions/` and `.github/copilot-instructions.md`.

### Viewing / toggling instructions

```
/instructions
```

This shows which instruction files are active and lets you toggle them on or off.

### Writing good instructions

Keep instructions short and specific. Focus on:

```markdown
## Architecture Rules
- Domain layer must never reference Infrastructure or Web layers
- All external service calls go through Application/Interfaces contracts

## Conventions
- CQRS: commands mutate state, queries only read
- Repositories return domain entities, not DTOs
- Use MediatR for all command/query dispatch

## Do Not
- Do not add direct DbContext usage in Web or Application layers
- Do not swallow exceptions with empty catch blocks
- Do not hardcode credentials or API keys
```

---

## Slash Command Cheatsheet

| Command | When to use |
|---------|------------|
| `Shift+Tab` | Switch to Plan mode (Copilot drafts a plan before coding) |
| `Shift+Tab` ×2 | Switch to Autopilot (Copilot works uninterrupted) |
| `/plan` | Explicitly ask Copilot to write out a plan |
| `/review` | Run code-review agent on current changes |
| `/diff` | See all changes made so far this session |
| `/pr` | Work with the current branch's pull request |
| `/delegate` | Hand off a task to GitHub's cloud agent (creates a PR) |
| `/fleet` | Run parallel sub-agents simultaneously |
| `/tasks` | View all background agents and their status |
| `/rewind` | Undo last turn and revert file changes |
| `/research` | Deep research using GitHub search + web sources |
| `/instructions` | View/toggle active instruction files |
| `/context` | See how much context window is used |
| `/compact` | Summarise history to free up context |
| `/share` | Export session to markdown or GitHub Gist |

---

## Workflow: End-to-End Example

Here is the full cycle as run on BalloonPrep. Use it as a template for your own features.

### Step 1 — Analyse & create issues (Plan mode)

```
[Press Shift+Tab to enter Plan mode]

Analyse the BalloonPrep codebase and identify:
1. The top 3 bugs most likely to cause a crash in production
2. Any hardcoded secrets that should not be in source control
3. Which parts of the code have zero test coverage

Then create a GitHub issue for each finding in NickThys3012/FlightPrep
with the correct labels (bug, security, testing).
```

Copilot will produce a plan **and** open issues in the repo. You'll see issue numbers (e.g. `#12`, `#13`) that you can reference in every subsequent commit.

### Step 2 — Branch

```
! git checkout -b ai-dev-cycle-fixes
```

> The `!` prefix runs a command directly in your shell, bypassing Copilot.

### Step 3 — Implement (Autopilot)

```
[Press Shift+Tab twice for Autopilot]

Fix the 3 bugs from the plan:
1. Add guards in TrajectoryService.ComputeAsync for zero ascent/descent rates
2. Guard the empty Timestamps list before calling .First() / .Last()
3. Throw in FrameTarget constructor if both page and frame are null

After each fix, make sure the project still builds.
```

### Step 4 — Review

```
/review
Review all changes made since the last commit.
```

### Step 5 — Generate tests

```
Write xUnit tests for the 3 fixes. Place them in tests/BalloonPrep.Tests/
Then run dotnet test to confirm all tests pass.
```

### Step 6 — Commit and delegate PR

```
/delegate
Create a PR for branch ai-dev-cycle-fixes with:
- Title: "fix: trajectory divide-by-zero, empty timestamps guard, FrameTarget null safety"
- Body summarising the 3 fixes and linking to any relevant issues
```

---

## Resources

- [GitHub Copilot CLI official docs](https://docs.github.com/copilot/concepts/agents/about-copilot-cli)
- [Copilot CLI on GitHub](https://github.com/github/copilot-cli)
- [Custom instructions reference](https://docs.github.com/copilot/customizing-copilot/adding-repository-custom-instructions-for-github-copilot)
