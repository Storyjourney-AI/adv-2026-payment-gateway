# Visual Guide

## Brand Personality
Precise, trustworthy, modern, engineering-first, understated.

---

## Color Palette

Derived from the Advine brand logo (eagle + gear, blue gradient wordmark).

| Role | Name | Hex |
|------|------|-----|
| Primary | Deep Navy | `#1B3A7A` |
| Primary Gradient Start | Royal Blue | `#2B72D9` |
| Primary Gradient End | Sky Blue | `#5BB4F8` |
| Accent | Electric Blue | `#4CA8F0` |
| Neutral Dark | Slate 900 | `#0F172A` |
| Neutral Mid | Slate 600 | `#475569` |
| Neutral Light | Slate 100 | `#F1F5F9` |
| Surface | White | `#FFFFFF` |
| Success | Emerald | `#10B981` |
| Warning | Amber | `#F59E0B` |
| Error / Danger | Red | `#EF4444` |
| Border | Slate 200 | `#E2E8F0` |

**Usage rules:**
- Deep Navy (`#1B3A7A`) is used for primary actions, key headings, and navigation emphasis.
- The blue gradient (Royal → Sky) is reserved for the logo, hero elements, and key brand moments — not for bulk UI components.
- Slate tones form the base of all surfaces, text, and borders. Keep backgrounds light (white / Slate 100) for a clean, readable feel.
- Accent Electric Blue is used sparingly for interactive states (hover, active, focus rings) and status badges.

---

## Typography

| Role | Recommendation |
|------|---------------|
| Heading font | Inter or Geist (clean, geometric, similar to Vercel's wordmark weight) |
| Body font | Inter |
| Code / monospace | JetBrains Mono or Geist Mono |
| Wordmark style | All-caps, wide letter-spacing (mirrors Advine logo treatment) |

**Tone guidance:**
- Professional and precise — no casual slang in labels or microcopy.
- Concise labels. Tables and dashboards should be scannable in seconds.
- Error messages must be specific and actionable, not generic ("Payment forwarding failed — endpoint returned 503. Retrying in 30s." not "Something went wrong.")

---

## UI Patterns

**Reference product:** Vercel Dashboard — clean white surfaces, strong typographic hierarchy, minimal chrome, data-dense but not cluttered.

**Density:** Medium-compact. Tables and lists should maximize information per viewport. Avoid excessive padding or card stacking.

**Component style:**
- Flat, minimal borders. Prefer subtle `Slate 200` dividers over heavy box shadows.
- Tables are the primary data component — sortable columns, status badges, pagination.
- Status badges: small, pill-shaped, with muted background fill (not solid color): success = emerald tint, error = red tint, pending = amber tint.
- Buttons: solid fill for primary actions (Deep Navy), ghost/outline for secondary, destructive red for delete/revoke.
- Forms: single-column, generous label clarity, inline validation feedback.
- Navigation: left sidebar (collapsible on small screens), minimal icon + label pairs.
- Modals: used for confirmations and quick edits only — not for complex multi-step flows.

**Icon style:** Lucide icons (already in use) — outlined, consistent 24px grid.

**Imagery:** No decorative illustrations. Use only functional diagrams (e.g. routing flow diagrams for documentation views).

---

## Reference Products
- **Vercel Dashboard** — layout, typography scale, table patterns, and neutral color base.
- **Stripe Dashboard** — status badge treatment, audit log presentation, financial data density.

---

## Anti-patterns
- **Do not** use playful, colorful gradients across component backgrounds — the brand gradient is reserved for logo/hero use only.
- **Do not** use dark mode as the *default* — the primary audience is ops/finance who expect light, print-friendly interfaces. Dark mode may be added as an option later.
- **Do not** use heavy card grids as the primary layout pattern — tables and lists are preferred for operational data.
- **Do not** use rounded corners larger than `rounded-lg` — keep the feel geometric and precise, not bubbly.
- **Do not** use red for anything other than errors and destructive actions.
- **Do not** add loading skeletons everywhere — reserve them for heavyweight data fetches only.
