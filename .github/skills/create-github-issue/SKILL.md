---
name: create-github-issue
description: 'Create a well-formatted GitHub issue from a described problem. Use for: reporting bugs, requesting features, flagging security concerns, documenting technical debt. Mandatory workspace exploration before drafting. Triggers: "create issue", "open issue", "report bug", "github issue", "file an issue", "log a problem".'
argument-hint: 'Describe the problem in plain language. E.g., "login fails when token expires silently"'
---

# Create GitHub Issue — Payment Gateway

Produces a complete, ready-to-submit GitHub issue by first studying the affected workspace areas to accurately assess criticality, impact, and risk — then writing the issue in clear human language with just enough technical detail to be actionable.

---

## When to Use
- A bug, regression, or unexpected behavior is discovered
- A new feature or improvement needs to be tracked
- A security concern is identified
- Technical debt or a refactor need needs to be logged

---

## Guiding Principles

1. **Understand before writing** — Always explore the relevant code before drafting. Impact cannot be assessed from description alone.
2. **Human first, technical second** — The title and summary must be readable by non-engineers. Technical detail lives in a dedicated section, not the opening.
3. **Honest severity** — Criticality is based on what you actually found in the code, not what feels dramatic.
4. **Actionable** — Every issue must have a clear outcome: what does "fixed" look like?

---

## Procedure

### Step 1 — Parse the Problem

Extract from the user's request:
- **Subject**: What is broken or missing?
- **Context**: Where does it happen? (route, controller, service, component)
- **Trigger**: What action causes it?
- **Expected vs Actual**: What should happen vs what does happen?

If any of these are unclear, ask one targeted clarifying question before proceeding.

---

### Step 2 — Explore the Workspace (Mandatory)

Use `semantic_search`, `grep_search`, and `read_file` to study relevant code.

Map the problem to this project's structure:

| Affected Area | Where to Look |
|---------------|---------------|
| Authentication / JWT | `PaymentGateway.Server/Authorization/` |
| Payment (Midtrans) | `PaymentGateway.Server/Midtrans/` |
| Application / Environment management | `PaymentGateway.Server/Applications/` |
| API endpoints | `PaymentGateway.Server/*/Controllers/` |
| Database / Migrations | `PaymentGateway.Server/Databases/`, `Migrations/` |
| Frontend routes | `paymentgateway.client/app/routes/` |
| Frontend services | `paymentgateway.client/app/services/` |
| Auth UI | `paymentgateway.client/app/routes/auth/` |
| Dashboard UI | `paymentgateway.client/app/routes/dashboard/` |

Answer these during exploration:
- Which files are directly affected?
- Does this touch auth, payments, or data integrity? (escalates severity)
- Is there existing error handling for this case?
- Are there related issues visible in the code (TODOs, FIXMEs, known workarounds)?

---

### Step 3 — Assess Severity

Use the findings from Step 2 to assign one of:

| Label | Criteria |
|-------|----------|
| `critical` | Data loss, payment failure, security breach, system-wide outage |
| `high` | Core flow broken, auth bypass, significant data corruption risk |
| `medium` | Feature degraded but workaround exists, UX significantly impaired |
| `low` | Minor UX issue, cosmetic, non-blocking edge case |

Also identify the issue type:
- `bug` — Something is broken that should work
- `enhancement` — Something that could be improved
- `security` — A vulnerability or sensitive data exposure
- `tech-debt` — Code quality, maintainability, or architecture concern
- `question` — Needs investigation before action

---

### Step 4 — Draft the Issue

Compose using the template below. Keep the opening paragraph free of code, jargon, and acronyms.

```markdown
## Summary
<!-- 1–3 sentences in plain language. What's wrong, and why it matters to a user or operator. -->

## What's Happening
<!-- Describe the current (broken) behavior clearly. -->

## What Should Happen
<!-- Describe the expected behavior. -->

## Steps to Reproduce
<!-- If applicable. Numbered list, concrete and minimal. Omit if not a bug. -->
1. 
2. 

## Impact
<!-- Who is affected? How often might this occur? Does it block a critical flow? -->

## Affected Areas
<!-- List specific files or modules identified in Step 2. -->
- `PaymentGateway.Server/…`
- `paymentgateway.client/…`

## Potential Risks
<!-- What could go wrong if this isn't fixed, or if fixed incorrectly? -->

## Technical Context
<!-- Brief technical detail for engineers. Keep to 3–6 bullet points. -->
- 
- 

## Suggested Fix
<!-- Optional. Only include if a clear, low-risk fix is evident from exploration. -->
```

---

### Step 5 — Finalize Metadata

Before submitting or presenting the issue, confirm:

| Field | Value |
|-------|-------|
| **Title** | ≤72 chars, starts with a verb, no punctuation at end |
| **Labels** | At least one type label + one severity label |
| **Milestone** | Match to active sprint or release if known |
| **Assignee** | Leave blank unless obvious from code ownership |

**Title format examples:**
- `Fix silent JWT expiry not redirecting users to login`
- `Add error feedback when Midtrans webhook signature fails`
- `Prevent duplicate environment creation per application`

---

### Step 6 — Submit or Present

If the GitHub MCP tool (`mcp_github_*`) is available:
1. Search for tool with pattern `create.*issue|issue.*create`
2. Submit directly using the correct owner/repo from the workspace attachment
3. Report the created issue URL to the user

If the tool is not available:
- Present the full formatted issue body in a fenced markdown block
- Include the recommended title and labels separately so the user can copy-paste directly into GitHub

---

## Quality Checklist

Before finalizing, verify:
- [ ] Issue is understandable without reading any code
- [ ] Severity label is justified by actual code findings, not assumption
- [ ] Affected files are real paths that exist in the workspace
- [ ] Suggested fix (if present) does not introduce new risks
- [ ] Title is ≤72 characters and starts with a verb
