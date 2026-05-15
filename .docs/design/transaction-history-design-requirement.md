# Design Requirement: Transaction History & PDF Export

## Overview
Transaction History is a dashboard page that allows authenticated users to view, filter, and export their payment transaction records. It provides paginated browsing of transactions with date-range and status filtering, plus the ability to export filtered results to PDF for reconciliation and record-keeping.

## Empathy Summary

### Persona: Internal Developer (80%)
- **Goal**: Quickly check the status of transactions their application processed through the gateway — especially during debugging or incident response.
- **Context**: Coming from their own product's dashboard, they need to cross-reference gateway transaction records with their app's data. They think in order IDs and status codes.
- **Anxiety**: "Did the payment actually land? Is it stuck in pending? Did the webhook fire?" — they need immediate, accurate status visibility.

### Persona: Ops / Finance Team (20%)
- **Goal**: Pull transaction reports for a specific period for reconciliation, bookkeeping, or management reporting. They need to generate printable/PDF records.
- **Context**: Working from spreadsheets and accounting tools. They need to filter by date range, see totals, and export clean data.
- **Anxiety**: "Am I looking at all transactions? Is this data complete? Can I trust these numbers for reconciliation?" — they need confidence in data completeness and accuracy.

## UX Intent
Give users immediate, filterable access to all their transaction history with zero hunting — and let them export any date range to PDF in one click.

## Page Vibe
**Tone**: Precise, operational, data-dense but not cluttered.
**Density**: Medium-compact. The table is the star — maximize information per viewport.
**Mood**: Confident and functional. Like a Stripe or Vercel data table.
**Visual references**: Slate 100 background, white surface cards, Slate 200 borders, status badges with muted tint fills (emerald for success, amber for pending, red for errors). Per `app-visual-guide.md`.

## Layout Description

### Structure
1. **Page Header** — Title "Transactions" with subtitle. Export to PDF button in the top-right corner.
2. **Filter Bar** — Horizontal row below header: Date From input, Date To input, Status dropdown, Search input. All inline.
3. **Data Table** — Full-width table with columns: Order ID, Application, Environment, Amount, Status, Provider, Created At.
4. **Pagination Bar** — Bottom of table: "Page X of Y" on left, Previous/Next buttons on right.

### Information Hierarchy
- **Most prominent**: Transaction status badges and order IDs (primary scan targets).
- **Secondary**: Amount, application name, dates.
- **Tertiary**: Environment type (sandbox/production badge), Midtrans transaction ID.

### Primary Action
- **Export to PDF** — Button in page header, exports the currently filtered date range.

### Secondary Actions
- Date range filtering, status filtering, search by order ID.

## Key Interaction Patterns
1. **Page Load**: Fetch first page of transactions (default: last 30 days, all statuses).
2. **Date Range Filter**: User selects "From" and "To" dates using native date inputs. Changing either triggers a re-fetch (debounced) resetting to page 1.
3. **Status Filter**: Dropdown with options: All, Pending, Settlement, Capture, Deny, Cancel, Expire, Error. Changing triggers re-fetch at page 1.
4. **Search**: Text input for searching by Order ID. Debounced 300ms, resets to page 1.
5. **Pagination**: Previous/Next buttons. Page indicator shows current position.
6. **Export PDF**: Clicking "Export PDF" sends the current filter parameters (date range, status) to the backend, which generates and returns a PDF file. The browser downloads it.

## Component Inventory
- **Table** (existing: `~/components/ui/table`)
- **Badge** (existing: `~/components/ui/badge`)
- **Button** (existing: `~/components/ui/button`)
- **Input** (existing: `~/components/ui/input` — used for native date inputs and search)
- **Select** (existing: `~/components/ui/select` — for status filter)
- **Skeleton** (existing: `~/components/ui/skeleton` — for loading state)
- **Lucide icons**: `FileDown` (export), `Search`, `Loader2`, `Receipt`

## Edge Cases & States
- **Empty state (no transactions)**: Centered message with receipt icon: "No transactions yet. Transactions will appear here once your applications start processing payments."
- **Empty state (no results for filter)**: "No transactions found matching your filters. Try adjusting the date range or status filter."
- **Error state**: Red-bordered dashed container with error message and "Try Again" button (matches Page_Applications pattern).
- **Loading state**: Centered spinner (Loader2 with animate-spin), matching existing pattern.
- **PDF export with no data**: Show toast notification: "No transactions found for the selected date range."
- **Large date range PDF**: Backend generates PDF server-side with pagination consideration. Maximum 10,000 records per export to prevent server overload.

## Constraints & Rules
- Follow existing naming conventions: `Page_Transactions.tsx`, `Dto_TransactionListItem`, etc.
- Use `authenticatedFetch` from `@services/auth/utils/api.helper` for all API calls.
- Backend pagination must use the existing `PaginationWrapper<T>` and `DataWrapper<T>` patterns.
- Status badges must use muted tint fills per visual guide (not solid colors).
- Users can only see transactions for their own applications (Super Admin sees all).
- PDF export must happen server-side to avoid loading all data into the browser.
- Maximum page size of 50 to limit server load per request.
- Date inputs use native HTML date inputs (no new dependencies needed).

## Open Questions
None — all design decisions are resolved based on existing patterns and visual guide.

## Design Confidence: High
All personas are clear, layout is unambiguous, all states are defined, no open questions remain. The design follows established patterns already present in the codebase (Page_Applications serves as the reference implementation for table + pagination + filter patterns).
