# Notion Config

## Database
- **Name**: Advine Tasks
- **ID**: 376c6bf0-a9ef-8015-bcbe-de75af302ce7
- **Data Source ID**: 376c6bf0-a9ef-808f-b031-000b5c122a1e
- **URL**: https://app.notion.com/p/376c6bf0a9ef8015bcbede75af302ce7

## Schema
| Field | Type | Notes |
|---|---|---|
| Task name | title | |
| Status | status | Backlog, Hold, Task Ready, In progress, Review, Done, Archived |
| Project | select | Fastreels, Echocast, Payment Gateway, Storyjourney V1 |
| Tags | multi_select | 🐞 Bug, 💬 Feature request, 💅 Polish |
| Assignee | person | |
| Due date | date | |
| Done Date | date | |
| Action Order | number | |
| Batch Id | text | |
| ID | auto_increment_id | read-only, system-managed |

## Project Filter
- **Field**: Project
- **Value**: Payment Gateway

## Rules: Creating Tasks

- **Title style**: GitHub issue style (short imperative title)
- **Body style**: Structured markdown — Overview, Problem Statement, Acceptance Criteria, Notes
- **Required fields**:
  - **Project**: always `Payment Gateway`
  - **Status**: default `Backlog` for new tasks
  - **Tags**: set when obvious (🐞 Bug / 💬 Feature request / 💅 Polish)
  - **Due date**: end of the current week
  - **Assignee**: Yoshua S (yoshua.paradigma@gmail.com, id `f4399f7b-feec-47d9-8168-5b1f517947d9`)
  - **Batch Id**: pattern `PGW-{YY}{WW}-{NN}` — Payment Gateway · 2-digit year · ISO week-of-year · batch number that week. Example: `PGW-2623-01` = Payment Gateway, 2026, week 23, first batch.
  - **Action Order**: sequential integer (1, 2, 3…) in intended execution order. **Unique per Batch Id** — no duplicate orders, no parallel/tied ordering within a batch.

## Rules: Updating Tasks

- Add a comment summarizing what changed.
- Update **Status** to reflect progress (e.g. → In progress / Review / Done).
