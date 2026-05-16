# Design Requirement: Dedicated Pending Payment Handling

**Task ID:** task-008

## Overview
This feature adds a dedicated frontend handling path for Midtrans pending or unfinish browser callbacks. The backend already resolves pending redirects to `PendingResponseUrl` when configured and falls back to `FailureResponseUrl` when it is blank, so the frontend work should stay narrow: add a dedicated pending result page, keep the existing centered result-page pattern, and make the environment admin flow clearer about what Pending URL is for.

Although Advine Payment Gateway is an internal product, this callback page is briefly seen by the payer in the browser. The UX therefore needs to be precise, calm, and low-friction: pending must read as unresolved, not failed, and must avoid pushing users into duplicate payment attempts.

## Empathy Summary

### Persona: Internal Developer
- Goal: Configure a distinct browser callback URL so pending payments are handled separately from hard failures.
- Context: They are in the dashboard, usually on Application Detail, setting up an environment or validating the flow with Test Purchase.
- Anxiety: If pending falls into the failure path, their product team and users will treat an incomplete payment as a broken payment, causing duplicate retries and support noise.
- Trigger: A product team needs clearer handling for Midtrans unfinish or pending states after task-007 introduced `PendingResponseUrl`.
- Success state: The environment clearly stores a Pending URL, the callback lands on a dedicated page, and the page copy matches the actual transaction state.

### Persona: Ops / Finance Team
- Goal: Distinguish unresolved transactions from failed ones when validating flows or investigating payment complaints.
- Context: They monitor transaction states in the dashboard and may reproduce flows through Test Purchase.
- Anxiety: A red or failure-like pending page creates false certainty that the payment is dead when it may still settle or still be payable.
- Trigger: Support escalations, QA checks, or reconciliation questions involving a transaction that remains in `pending`.
- Success state: Pending is visually and verbally distinct from failure, and the environment configuration makes the redirect behavior obvious.

## Primary User Flow
- Flow name: Dedicated pending browser callback
- Entry point: Application owner configures an environment in Application Detail, then a payer reaches the browser callback after leaving Midtrans in a pending or unfinished state.
- Trigger: Midtrans returns a verified browser callback for a pending or unfinish state.
- Success state: The payer lands on a dedicated pending page that explains the payment is still in progress, shows the reference details, and gives the safest next action without implying success or failure.

## User Flow Breakdown
1. Step 1: Application owner opens the environment create or edit dialog from Application Detail.
   - User intent: Register where browser callbacks should land for each payment outcome.
   - UI response: The form shows Success URL, Pending URL, Failure URL, and Webhook URL in one response URL group.
   - System feedback: Pending URL is described as optional, with a clear fallback to Failure URL when left blank.
2. Step 2: Application owner saves an environment with a dedicated Pending URL.
   - User intent: Separate incomplete payments from hard failures.
   - UI response: The environment summary card displays Success URL, Pending URL, and Failure URL side by side.
   - System feedback: The saved Pending URL is visible immediately so the owner can verify the routing contract.
3. Step 3: A payer exits Midtrans in a pending or unfinished state.
   - User intent: Understand whether the payment is still open and what to do next.
   - UI response: The backend redirects to the configured Pending URL with verified query parameters.
   - System feedback: The pending page renders a clear amber status state instead of a red failure state.
4. Step 4: The pending page explains the transaction state.
   - User intent: Confirm whether payment is still being processed, still waiting for action, or can be resumed later.
   - UI response: The page shows a status header, short explanation, and a compact details block.
   - System feedback: The details block prioritizes `caller_order_id` as the human-facing reference when present, with `transaction_status` and `status_code` visible for support/debugging.
5. Step 5: The payer chooses the next action.
   - User intent: Leave the gateway safely and continue in the original product flow.
   - UI response: The primary action returns the user to the most useful known destination. If no app-specific return target is available, the page falls back to the same safe navigation actions already used by the existing result pages.
   - System feedback: The copy explicitly warns against assuming failure or creating duplicate payments unless the original app asks them to retry.

## Decision Points & Branches
- Decision: Is `PendingResponseUrl` configured?
- If yes: Pending or unfinish browser callbacks resolve to the dedicated pending page.
- If no: Pending continues to fall back to the failure URL, preserving current behavior.

- Decision: Does the pending page receive `caller_order_id`?
- If yes: Show it as the primary reference label because it matches the product team's original order identifier.
- If no: Fall back to `order_id` and keep the page generic.

- Decision: Is an app-specific return destination available to the page?
- If yes: Use it as the primary CTA label, such as "Return to Application".
- If no: Reuse the current fallback navigation pattern so the page still has a deterministic exit.

## Failure & Recovery Paths
- Validation failure: Invalid Pending URL entry is blocked inline in the environment form using the same URL validation pattern already used for Success URL and Failure URL.
- Network or system failure: If the environment save request fails, keep the existing toast-based save failure pattern.
- Missing pending configuration: The browser callback safely falls back to Failure URL, so no route breaks for existing environments.
- Missing query parameters on the pending page: Render generic pending copy and omit unavailable fields rather than showing a broken state.
- Unexpected non-pending status on the pending route: Still render the page, but use a neutral "Payment status updated" sublabel and show the raw transaction status in the detail block.
- Abandon or resume path: The page should make it clear that the transaction may still complete later and that the user should resume from the original app if they intend to continue.

## UX Intent
Give users immediate confidence that their payment is still open, not broken, and steer them toward the safest next step without forcing a retry.

## Page Vibe
Preserve the existing full-page result pattern already used by Success and Failed pages, but shift the pending variant toward the product visual guide: light surface, strong hierarchy, muted amber accent, and minimal drama. The page should feel calm and operational, not celebratory and not alarming.

Per `app-visual-guide.md`, pending should use muted amber treatment rather than red. The page should stay single-column and centered, with concise copy and a compact detail summary.

## Layout Description
The layout should reuse the current result-page structure with four regions:

1. Status header
Supports flow step 4. This is the first scan target and should contain an amber eyebrow, a clear title such as "Payment still in progress", and one short explanatory sentence.

2. Reference details block
Supports flow step 4. Keep the existing compact summary-card pattern, but show fields in this order: Reference ID (`caller_order_id` when present), Transaction Status, Status Code, then Gateway Order ID as a secondary identifier if available.

3. Next steps block
Supports flow step 5. This is the key difference from the failure page. Use 2 or 3 short guidance lines explaining that the payment may still complete later, may still need action in the original app, and should not be retried blindly.

4. Action row
Supports flow step 5. Keep two buttons maximum. Primary action should be the best-known return path. Secondary action can remain the existing gateway fallback action for consistency.

The hierarchy should be: title first, status explanation second, details third, actions last. Do not add tabs, accordions, or additional navigation chrome.

## Key Interaction Patterns
- The pending page is read-only. It should not require authentication and should not make an API request in the minimal implementation.
- The page should read the same callback query pattern already used by Success and Failed, plus `caller_order_id` when available.
- Pending messaging must differ from Failure in both tone and guidance:
  - Success confirms completion and closure.
  - Failure confirms the payment did not complete and points toward retry.
  - Pending confirms the payment is unresolved and points toward waiting, resuming, or checking in the original product.
- Reuse the same button, spacing, and centered container conventions as the current result pages to keep the change coherent.
- If the team later adds an app return parameter such as `back_url` or `app_name`, the pending page is the first place where that extra context materially improves the CTA label and destination.

## Component Inventory
- Existing public result-page layout pattern from the current payment pages
- `Button`
- `Badge` or equivalent muted status label styling
- Existing typography and spacing tokens already used by public pages
- No new complex components are required

## Edge Cases & States
- Empty state: Not applicable. This is a single transaction status page.
- Loading state: Not needed in the minimal implementation because the page is query-driven and static.
- Missing `caller_order_id`: Show `order_id` as the main reference.
- Missing all identifiers: Keep the copy generic and avoid rendering placeholder-heavy detail rows.
- Pending URL omitted in admin: Keep the current fallback to Failure URL, but the form help text should make that fallback explicit.
- Sandbox testing: The same page should work for both sandbox and production because the callback payload shape is the same.

## Constraints & Rules
- Preserve the current public result-page pattern. This is a targeted addition, not a redesign.
- Do not collapse pending into the failure page. Pending needs its own route and its own copy.
- Keep the page unauthenticated and query-driven.
- Use the backend callback contract that already appends verified query parameters, including `order_id`, `caller_order_id`, `status_code`, `transaction_status`, `fraud_status`, `payment_type`, and `transaction_id`.
- In the admin UI, the nearest and correct place for Pending URL is the existing environment form on Application Detail; no separate admin page is needed.
- The environment summary card should continue showing the Pending URL distinctly from the Failure URL so routing is auditable.
- Use muted amber status styling for pending to stay aligned with the existing dashboard badge language and the visual guide.

## Open Questions
None. The minimal implementation can proceed with the existing backend callback contract and current environment management surface.

## Design Confidence
High

The primary flow is explicit end to end, the pending state is clearly separated from success and failure, the admin change is localized to an existing form, and the routing requirements are concrete. No additional product decisions are required for the minimal implementation.

Design confidence is high. Handing off to `gh-developer`.