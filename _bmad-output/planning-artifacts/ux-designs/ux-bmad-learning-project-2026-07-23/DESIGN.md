---
name: Fake Barbershop
description: Barbershop appointment scheduler (portfolio/learning project). React frontend, fully custom CSS/components except Radix UI primitives for calendar, dropdown/select, and modal — "Modern Warmth" palette, Manrope, subtly rounded corners, light-only.
status: final
sources:
  - "{planning_artifacts}/prds/prd-bmad-learning-project-2026-07-21/prd.md"
  - "{planning_artifacts}/prds/prd-bmad-learning-project-2026-07-21/addendum.md"
updated: 2026-07-27
colors:
  # "Modern Warmth" — picked direction, variation 5 of 6 explored in
  # .working/color-themes-1.html. Hex values are locked client decisions.
  primary: '#0E7C9B'
  primary-foreground: '#FFFFFF'
  primary-hover: '#0B5F79'
  destructive: '#C93A3A'
  destructive-foreground: '#FFFFFF'
  # destructive-hover is NOT an explicit client decision — it's inferred by
  # applying the same "darker hue on hover" rule already locked for primary,
  # since destructive buttons (Cancel Appointment, Delete account) need a
  # hover state too. Flagged as a default; revisit with client.
  destructive-hover: '#A83030'
  # error is a semantically distinct token from destructive -- same hex,
  # reused deliberately (client decision, 2026-07-27) rather than
  # introducing a new hue. Used for validation-message text only
  # (password mismatch, duplicate email) -- never on a button/fill;
  # destructive stays reserved for actual destructive actions (Cancel,
  # Delete) per the Do's and Don'ts rule below.
  error: '#C93A3A'
  background: '#FFFFFF'
  neutral: '#EFF6F8'
  border: '#D3E4E9'
  # text / text-muted are NOT explicit client decisions — the memlog only
  # locked interactive-element colors (primary, destructive, border,
  # background, neutral). A body-text color is required for the app to
  # render at all, so these are proposed defaults: a dark neutral that
  # reads as "ink" against the cool white/teal palette, not pure black.
  text: '#17242A'
  text-muted: '#5B7480'
typography:
  # Family (Manrope, single family for headings + body) is the one locked
  # decision. The full size/weight ramp below is a standard editorial
  # scale proposed as a sensible default — not yet reviewed with the client.
  display:
    fontFamily: 'Manrope'
    fontSize: 40px
    fontWeight: '700'
    lineHeight: '1.15'
    letterSpacing: -0.01em
  h1:
    fontFamily: 'Manrope'
    fontSize: 32px
    fontWeight: '700'
    lineHeight: '1.2'
  h2:
    fontFamily: 'Manrope'
    fontSize: 24px
    fontWeight: '600'
    lineHeight: '1.25'
  h3:
    fontFamily: 'Manrope'
    fontSize: 20px
    fontWeight: '600'
    lineHeight: '1.3'
  body:
    fontFamily: 'Manrope'
    fontSize: 16px
    fontWeight: '400'
    lineHeight: '1.5'
  body-sm:
    fontFamily: 'Manrope'
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.5'
  label:
    fontFamily: 'Manrope'
    fontSize: 14px
    fontWeight: '600'
    lineHeight: '1.4'
  caption:
    fontFamily: 'Manrope'
    fontSize: 12px
    fontWeight: '500'
    lineHeight: '1.4'
    letterSpacing: 0.01em
rounded:
  # Revised, subtly-rounded scale — previously this was all-0px/square (see
  # git history / Shapes below for that earlier direction). That square,
  # hairline-bordered language was flagged at the time as exploratory and
  # revisitable, and direct client feedback after seeing the finished
  # product asked for something closer to "Figma-based style, like rounded
  # buttons." The client explicitly wants subtle rounding, not pill-shaped
  # controls, so the scale below stays in the 4–8px range at every step.
  # `full` remains a standard available step (e.g. the circular profile-icon
  # avatar) — it is not a signal that buttons or other rectangular surfaces
  # should go pill-shaped anywhere in this product.
  sm: 4px
  DEFAULT: 6px
  md: 6px
  lg: 8px
  xl: 8px
  full: 9999px
spacing:
  # No spacing scale was discussed with the client. This is a standard
  # 4px-base scale, proposed as a sensible default — not a bespoke decision.
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 20px
  '6': 24px
  '8': 32px
  '10': 40px
  '12': 48px
  '16': 64px
  gutter-mobile: 16px
  gutter-desktop: 32px
  content-max-width: 1120px
components:
  button-primary:
    background: '{colors.primary}'
    foreground: '{colors.primary-foreground}'
    hover-background: '{colors.primary-hover}'
    active-background: '{colors.primary-hover}'
    radius: '{rounded.DEFAULT}'
    padding: '{spacing.3} {spacing.6}'
    fontSize: '{typography.label.fontSize}'
    fontWeight: '{typography.label.fontWeight}'
    border: 'none'
  button-secondary:
    # "Go Back" in confirm popups; also Sign In in the nav.
    background: '{colors.background}'
    foreground: '{colors.primary}'
    border: '1px solid {colors.border}'
    hover-background: '{colors.neutral}'
    radius: '{rounded.DEFAULT}'
    padding: '{spacing.3} {spacing.6}'
  button-destructive:
    background: '{colors.destructive}'
    foreground: '{colors.destructive-foreground}'
    hover-background: '{colors.destructive-hover}'
    active-background: '{colors.destructive-hover}'
    radius: '{rounded.DEFAULT}'
    padding: '{spacing.3} {spacing.6}'
    fontSize: '{typography.label.fontSize}'
    fontWeight: '{typography.label.fontWeight}'
  nav-bar:
    background: '{colors.background}'
    border-bottom: '1px solid {colors.border}'
    height: 64px
    logo-foreground: '{colors.text}'
    link-foreground: '{colors.text-muted}'
    link-foreground-active: '{colors.primary}'
    link-underline-active: '{colors.primary}'
  footer:
    background: '{colors.neutral}'
    border-top: '1px solid {colors.border}'
    foreground: '{colors.text-muted}'
    wordmark-foreground: '{colors.text}'
    padding: '{spacing.6} {spacing.4}'
    fontSize: '{typography.body-sm.fontSize}'
  input:
    background: '{colors.background}'
    border: '1px solid {colors.border}'
    border-focus: '{colors.primary}'
    radius: '{rounded.DEFAULT}'
    foreground: '{colors.text}'
    placeholder-foreground: '{colors.text-muted}'
    padding: '{spacing.3} {spacing.4}'
    fontSize: '{typography.body.fontSize}'
  calendar:
    # Radix Popover/date-picker primitive, fully restyled.
    trigger-background: '{colors.background}'
    trigger-border: '1px solid {colors.border}'
    trigger-radius: '{rounded.DEFAULT}'
    panel-background: '{colors.background}'
    panel-shadow: 'floating (open-state popover)'
    panel-radius: '{rounded.DEFAULT}'
    selected-day-background: '{colors.primary}'
    selected-day-foreground: '{colors.primary-foreground}'
    today-indicator-foreground: '{colors.primary}'
    disabled-day-foreground: '{colors.text-muted}'
  select-dropdown:
    # Radix Select, customer-facing contexts (barber select on Schedule
    # Appointment, time-slot select). Closed trigger sits in normal page
    # flow — border, no shadow. Open menu is floating — shadow, no border
    # needed beyond a hairline.
    trigger-background: '{colors.background}'
    trigger-border: '1px solid {colors.border}'
    trigger-radius: '{rounded.DEFAULT}'
    menu-background: '{colors.background}'
    menu-shadow: 'floating (open-state)'
    menu-radius: '{rounded.DEFAULT}'
    option-foreground: '{colors.text}'
    option-hover-background: '{colors.neutral}'
    option-selected-background: '{colors.neutral}'
    option-selected-foreground: '{colors.primary}'
  select-dropdown-admin-barber:
    # My Schedule (admin view) barber-select, next to the date header.
    # Deliberate exception: carries the floating shadow at REST, not only
    # when open — see Elevation & Depth.
    trigger-background: '{colors.background}'
    trigger-border: '1px solid {colors.border}'
    trigger-shadow: 'floating (at rest — exception to the border-only rule)'
    trigger-radius: '{rounded.DEFAULT}'
  modal:
    # Radix Dialog primitive — account-edit popup, confirm-action popup.
    background: '{colors.background}'
    shadow: 'floating'
    radius: '{rounded.DEFAULT}'
    overlay-scrim: 'rgba(23, 36, 42, 0.4)'
  schedule-row-open:
    # Tinted-section treatment (client feedback: non-Home pages "lacked
    # color"). Neutral fill, no border; hover deepens to the border tint.
    background: '{colors.neutral}'
    hover-background: '{colors.border}'
    foreground: '{colors.text-muted}'
  schedule-row-booked:
    # Same tinted-section treatment as schedule-row-open.
    background: '{colors.neutral}'
    hover-background: '{colors.border}'
    foreground: '{colors.text}'
    cancel-button: '{components.button-destructive}'
  admin-account-row:
    # Same tinted-section treatment; hover-background is now a distinct,
    # visibly darker tint since the resting state is already neutral-filled.
    background: '{colors.neutral}'
    hover-background: '{colors.border}'
    foreground: '{colors.text}'
  form-section:
    # New tinted-card treatment for the booking-form container (and other
    # single-form pages: Account, Login, Register). Part of the "Tinted
    # sections" fix for client feedback that non-Home pages "lacked color."
    # Previously this container had no dedicated token — it relied on a
    # border via generic "form sections" prose in Colors, not a real entry.
    background: '{colors.neutral}'
    radius: '{rounded.lg}'
    padding: '{spacing.6}'
  date-nav-arrow:
    # My Schedule's day-step controls, either side of the date header.
    foreground: '{colors.text-muted}'
    foreground-hover: '{colors.primary}'
    disabled-foreground: '{colors.border}'
    size: 20px
  admin-account-popup:
    # Field layout inside {components.modal} for both the edit and create
    # variants — composition of modal + input + select-dropdown + buttons,
    # given its own entry so the stacking/spacing isn't left to inference.
    background: '{colors.background}'
    field-gap: '{spacing.4}'
    section-gap: '{spacing.6}'
    field: '{components.input}'
    permission-select: '{components.select-dropdown}'
    footer-gap: '{spacing.3}'
  confirm-popup:
    background: '{colors.background}'
    shadow: 'floating'
    radius: '{rounded.DEFAULT}'
    go-back: '{components.button-secondary}'
    confirm-destructive-context: '{components.button-destructive}'
    confirm-nondestructive-context: '{components.button-primary}'
  confirmation-screen:
    background: '{colors.background}'
    accent-foreground: '{colors.primary}'
    body-foreground: '{colors.text}'
---

## Brand & Style

Fake Barbershop is a portfolio/learning project built as a real, working appointment scheduler — not a mockup. The brand posture follows the product's actual claim: booking a haircut should feel as fast and unfussy as the cut itself. Nothing about the visual language should slow a customer down or make an admin second-guess a click.

The identity has two ingredients doing all the work: a warm-but-professional teal-blue that means "this is the thing you can act on," and a subtly-rounded surface language, built from tinted-section fills on grouped content, that reads as approachable and considered rather than sterile. There's no logo mark — just a wordmark, "Fake Barbershop," set in Manrope. This revises the original all-square, border-only direction (full rule in Shapes, below) after direct client feedback that it read as too monochrome and not "Figma-style" enough — that square language had been explicitly flagged at the time as the one exploratory, revisitable choice in the system, and this is that revision. Every other decision here is in service of making the surface feel calm and trustworthy — which is why the palette leans warm and the shadow language is disciplined rather than decorative.

The frontend is fully custom CSS and components, with one exception: Radix UI's unstyled, accessible primitives power the calendar/date-picker, the dropdown/select menus, and the modal/dialog. Those three surfaces are the highest-risk spots for accessibility bugs (focus trapping, keyboard navigation, ARIA semantics) if hand-rolled — Radix supplies the behavior, this document supplies 100% of the visual skin on top of it. Buttons, nav, forms, schedule rows, and layout are fully custom, no library involved.

## Colors

- **Primary Teal (`{colors.primary}`, `#0E7C9B`)** is the one interactive color in the system. It fills every primary button ("Schedule Appointment," "Submit," non-destructive "Confirm"), marks the active nav link, and fills the blue half of the Home hero. **Primary-foreground (`{colors.primary-foreground}`, `#FFFFFF`)** is the paired text/icon color for anything sitting on a primary fill.
  - *Contrast:* `{colors.primary}` on `{colors.background}` (white) computes to **≈4.79:1**, which clears WCAG 2.2 AA's 4.5:1 threshold for normal-size text — this is the pairing used for the active nav-link label and any teal-on-white link text. White on primary (button labels) is the same ratio in reverse, ≈4.79:1 — passing, but not by a wide margin. Don't add opacity, tints, or overlays on top of button labels; that margin doesn't have slack to spare.
- **Primary Hover/Active (`{colors.primary-hover}`, `#0B5F79`)** is the single darker-hue state used for hover *and* pressed/active, on desktop/pointer devices only — never triggered on touch, per the hover-vs-touch constraint. It's also meaningfully higher-contrast against white (≈7.17:1) than the resting color, so the hover state is a genuine accessibility improvement, not just a cosmetic one.
- **Destructive Red (`{colors.destructive}`, `#C93A3A`)** is reserved *only* for destructive actions — buttons and fills: the "Cancel" button on a booked schedule slot, "Delete" in the admin account-edit popup, and the red "Confirm" button inside a confirm-popup when — and only when — the action being confirmed is destructive. It never appears for emphasis or decoration, and it never labels a non-destructive confirm (see Do's and Don'ts). `{colors.error}` (below) shares its hex but is a distinct token reserved for validation text, never a button/fill — the two tokens are not interchangeable even though they render identically.
  - *Contrast:* `{colors.destructive}` against white computes to **≈5.06:1**, clearing the 4.5:1 AA threshold for normal-size text with a bit of margin. This is a deliberately darkened shade — the original candidate (`#D64545`) sat at ≈4.38:1, just under AA for the 14px/600 button labels this color is used on; client decision was to darken the fill rather than accept the shortfall or carve out a smaller-text exception, so `#C93A3A` is the single destructive value everywhere, no size-dependent variants.
- **Destructive Hover (`{colors.destructive-hover}`, `#A83030`)** extends the same darker-on-hover pattern already locked for primary — not an explicit client decision on the exact hex, but inferred from that rule and re-tuned to match the darkened destructive base. Computes to ≈6.71:1 against white, consistent with hover states being a genuine contrast improvement over resting state, not just cosmetic.
- **Background (`{colors.background}`, `#FFFFFF`)** is the page canvas everywhere. No off-white, no tinted surface — pure white, consistent with "slick modern" and the light-only mandate.
- **Neutral/Muted (`{colors.neutral}`, `#EFF6F8`)** is now a primary fill color, not just a wash — it's the "Tinted sections" fix for client feedback that non-Home pages read as too monochrome/white. It's the resting-state background for every grouped, in-flow container: schedule rows (open and booked), admin account rows/bars, and the booking-form card (`{components.form-section}`, also reused by other single-form pages). It's still never used for a full-page background — that stays `{colors.background}` white — and it's still not part of Home's hero, which stays the untouched primary-teal/white split.
- **Border (`{colors.border}`, `#D3E4E9`)** now covers a narrower, more specific set of jobs than before, following the tinted-sections change: the nav-bar bottom rule, the footer top rule, individual input-field outlines, and dropdown/calendar trigger outlines in their resting customer-facing state. Schedule rows, admin account bars, and form sections no longer use it as a resting-state outline (they're neutral-tinted with no border — see above); instead, `{colors.border}` is repurposed as the *hover-state fill* for those tinted rows/cards, a darker tint layered on top of `{colors.neutral}` rather than a hairline around it.
- **Text (`{colors.text}`, `#17242A`) and Text-Muted (`{colors.text-muted}`, `#5B7480`)** are proposed defaults, not explicit client decisions — the memlog locked interactive-element and surface colors but never a body-text color. `{colors.text}` is a dark, cool-leaning near-black (not pure `#000`) chosen to sit comfortably next to the teal palette; `{colors.text-muted}` is used for secondary copy, placeholder text, and open (unbooked) schedule-slot labels.
  - *Contrast:* `{colors.text-muted}` against white computes to **≈4.93:1** — clears the 4.5:1 AA threshold for normal-size text, but by a narrower margin than the primary/destructive pairings above. Worth stating explicitly for the same reason those two are: this is the color used for open-slot labels and placeholder text, and it doesn't have much room to get lighter or shift hue before it drops below AA.
- **Error (`{colors.error}`, `#C93A3A`)** is the validation-message color: password-confirmation mismatches (Register/Account/Admin-edit) and duplicate-email rejections (Register/Admin-edit/Admin-create). Resolved 2026-07-27 — the client's call was to reuse the destructive hue directly rather than introduce a new one, so `{colors.error}` is a separate *token* carrying the same value as `{colors.destructive}` (contrast identical, ≈5.06:1 against white — clears AA at the 12px caption size these messages render at), kept semantically distinct so a future re-tune of one doesn't silently drag the other along. It's used for message text only, never a button/fill or icon — see Components below for the input-mismatch treatment.

## Typography

Manrope is the single family for both headings and body — no serif or secondary face, no display-font "moment" the way some brands use one. The full size/weight ramp (`{typography.display}` through `{typography.caption}`) is a standard editorial scale proposed as a sensible default; only the family choice itself was reviewed with the client.

- `{typography.display}` (40px/700) — reserved for the Home hero headline only, set on the white half of the hero split.
- `{typography.h1}` (32px/700) / `{typography.h2}` (24px/600) / `{typography.h3}` (20px/600) — page and section titles (e.g. "My Schedule," the date header, "Admin Panel").
- `{typography.body}` (16px/400) — default running copy: form help text, About-page content, confirmation-screen body line.
- `{typography.body-sm}` (14px/400) — secondary copy inside dense UI: schedule-row metadata, admin-panel search results.
- `{typography.label}` (14px/600) — all button labels, form-field labels, nav links.
- `{typography.caption}` (12px/500) — timestamps, helper microcopy, field validation hints.

Sample UI copy in this document (nav labels, button labels, confirmation-screen text) is written in the locked voice register: clean, professional, plain-spoken, no exclamation points. Full voice/tone rules live in EXPERIENCE.md — this document doesn't restate them, only ensures the type ramp above can carry that voice without strain (e.g. no all-caps treatments, no condensed tracking that would fight a neutral tone).

## Layout & Spacing

No spacing scale was discussed with the client; the ramp in frontmatter (`{spacing.1}` through `{spacing.16}`, a standard 4px-base scale) is a default, not a bespoke decision, proposed here so components have something concrete to reference.

Content is capped at `{spacing.content-max-width}` (1120px) on wide viewports — this is a scheduling tool with tabular/list-shaped content (schedule rows, admin account bars), not an infinite-canvas app, so a bounded reading/working width keeps rows scannable rather than stretching thin across an ultrawide screen. Page gutters are `{spacing.gutter-mobile}` (16px) below the tablet breakpoint and `{spacing.gutter-desktop}` (32px) above it.

The product is responsive across mobile and desktop viewports (no native app), with one binding interaction rule: hover-driven affordances (button hover states, dropdown hover previews) exist only on pointer devices and must never be the *only* way to discover an action on touch — every hover-revealed state has a tap-visible equivalent. Breakpoint values are locked at 640px/1024px (client decision, 2026-07-27 — the conventional split already drafted throughout both spines and the mockups, confirmed rather than changed). The mobile nav-collapse pattern (see EXPERIENCE.md's Responsive & Platform table) remains this spine's own proposed default, not a separate client call.

## Elevation & Depth

Three depth states exist in this system now (previously two, before the tinted-sections change split "in-flow" into two distinct treatments). Shadow is still never decorative — if something casts one, it's because it's an overlay sitting on top of the flow, not because it looked nice.

- **Individual controls (white + border, no shadow, no tint):** input fields, dropdown/calendar triggers in their resting customer-facing state, the nav bar, and the footer. All use `{colors.border}` as a 1px hairline against `{colors.background}` white — these are single controls or unrelated page-level separators, not grouped containers, so they keep the original outline treatment.
- **Grouped in-flow containers (neutral tint, no border):** schedule rows (both open and booked), admin account rows/bars, and the booking-form card (`{components.form-section}`). These sit in the page flow like the controls above, but as row/card-level containers for grouped content they now use a `{colors.neutral}` fill with no border — the "Tinted sections" fix for feedback that non-Home pages lacked color. Hover swaps the fill to `{colors.border}` as a slightly darker tint, reusing the existing border color rather than introducing a new hex.
- **Floating (shadow, subtle):** confirm-action popups, the admin account-edit popup, and any open dropdown/select menu (calendar panel, barber-select menu, time-slot menu) while expanded. Unchanged by the tinted-sections update. The shadow should read as a soft, close lift — not a dramatic drop shadow; it exists to signal "this will go away if you click outside," not to add visual weight.
- **Named exception:** the admin-only barber-select control on My Schedule (`{components.select-dropdown-admin-barber}`) carries the floating shadow *at rest*, closed, not just when its menu is open. This is intentional, not an inconsistency: in the admin view it functions as a page-level filter sitting above the schedule list it governs, and the shadow signals that relationship even before it's interacted with. The equivalent barber-select on the customer-facing Schedule Appointment page lives inside the booking form and stays border-only at rest, consistent with the general rule — only the admin variant gets the exception. → `mockups/my-schedule.html`.

Motion is minimal and tied to the same floating/overlay set: a fast, subtle fade-and-slide (roughly 120–160ms, ease-out) on popups and dropdown menus opening/closing. Nothing else in the product animates — no hover transitions beyond the instant color swap, no page-transition choreography, no decorative motion. Exact easing/duration values above are a proposed default, not yet reviewed with the client.

## Shapes

Corners are now subtly rounded, in the 4–8px range depending on the element — `{rounded.DEFAULT}`/6px for most buttons, inputs, and rows, `{rounded.lg}`/8px for larger card-level containers like `{components.form-section}` and popups. This reverses the earlier all-square decision (`border-radius: 0` everywhere) after direct client feedback asking for something closer to a familiar, "Figma-style" softness — "like rounded buttons" — rather than the sharper, tool-like edge the square language was going for. That original decision was flagged at the time as the one exploratory, revisitable choice in the system, and this is that revision.

There are still no pill/capsule shapes anywhere in the product (no fully-rounded status badges, no circular chips) — this stays a deliberate choice, not an oversight: when offered the option, the client specifically asked for "subtle" rounding, not pill-shaped buttons. The one circular element remains the profile-icon avatar in the signed-in nav, now consistently expressible via the standard `{rounded.full}` scale step rather than a one-off exception outside the scale.

## Components

- **Button — Primary (`{components.button-primary}`).** `{colors.primary}` fill, `{colors.primary-foreground}` label, no border, `{rounded.DEFAULT}` corners, `{typography.label}` sizing. Used for "Schedule Appointment" (nav CTA and Home hero CTA), form "Submit" actions, and non-destructive "Confirm" inside popups. Hover/active swaps fill to `{colors.primary-hover}` on pointer devices only.
- **Button — Destructive (`{components.button-destructive}`).** `{colors.destructive}` fill, `{colors.destructive-foreground}` label, otherwise identical shape/sizing to primary. Used *only* for "Cancel" on a booked schedule row, "Delete" in the admin account-edit popup, and destructive "Confirm" inside a confirm-popup. Passes AA contrast at ≈5.06:1 — see the Colors section above.
- **Button — Secondary/neutral (`{components.button-secondary}`).** White fill, `{colors.primary}` label, `{colors.border}` outline. Used for "Go Back" in every confirm-popup (always white/neutral, regardless of what it's cancelling out of) and, as a proposed default not explicitly locked, for "Sign In" in the nav — a lower-emphasis companion to a more prominent "Register."
- **Footer (`{components.footer}`).** Present on every page, below all content. `{colors.neutral}` fill with a `{colors.border}` top border — the same "subtle section separation" use of neutral defined in Colors above, not a new pattern. Carries, in `{colors.text-muted}` at `{typography.body-sm}`: the "Fake Barbershop" wordmark (in `{colors.text}`, smaller and quieter than the nav logo — it's a footer credit, not a second brand moment), address and phone (same fake contact info as the About page), hours ("Mon–Fri, 9:00 AM – 4:30 PM," matching the shop's actual booking window and the weekends-closed rule), and a copyright line ("© 2026 Fake Barbershop"). No links, no social icons — kept as simple as every other surface in this product.
- **Nav bar (`{components.nav-bar}`).** Present on every page. Left: wordmark logo (`{colors.text}`, no graphic mark). Center/left-of-center: Home, Schedule Appointment, About, My Schedule, Admin Panel — My Schedule hidden unless the signed-in user is a barber or admin, Admin Panel hidden unless admin, matching the product's role-based visibility rule. The active link is `{colors.primary}` with a `{colors.primary}` underline; inactive links are `{colors.text-muted}`. Right side: signed-out shows Sign In (secondary/neutral button) + Register (primary button); signed-in replaces both with a profile icon that opens a dropdown (Radix) listing account actions and sign-out — the icon's own visual treatment (avatar vs. generic glyph) is a default left open for implementation, not specified here.
- **Form inputs (`{components.input}`), including double-entry password fields.** Single-line text style: white fill, `{colors.border}` outline, `{rounded.DEFAULT}` corners, `{colors.text}` value color, `{colors.text-muted}` placeholder. Focus state swaps the border to `{colors.primary}`. Register, Account, and Admin account-edit all use the identical double-entry pattern for passwords: two stacked `{components.input}` fields ("Password," "Confirm Password") with no visual distinction between them beyond the label — a mismatch surfaces as a message in `{typography.caption}` using `{colors.error}` (see Colors above), not the plain-text treatment used in earlier drafts.
- **Form-section card (`{components.form-section}`).** The tinted container wrapping the booking form on Schedule Appointment, and reused by other single-form pages (Account, Login, Register) that were previously bare white pages relying only on their individual bordered inputs for definition. `{colors.neutral}` fill, no border, `{rounded.lg}` corners, `{spacing.6}` padding — part of the same "Tinted sections" fix as the schedule and admin rows above.
- **Calendar / date-picker (`{components.calendar}`).** Radix Popover-driven date picker, fully restyled: closed trigger is border-only in the booking form; the open panel is a floating surface with `{colors.background}` fill and the standard floating shadow. Selected day is a solid `{colors.primary}` fill with `{colors.primary-foreground}` text; today is indicated by `{colors.primary}` text only (no fill) so it doesn't compete visually with the actual selection.
- **Barber-select dropdown (`{components.select-dropdown}` / `{components.select-dropdown-admin-barber}`).** Customer-facing (Schedule Appointment form): standard border-only trigger, floating-shadow menu when open. Admin-facing (My Schedule, next to the date header): the trigger itself carries the floating shadow at rest — the one deliberate exception in the elevation model, explained in Elevation & Depth above.
- **Time-slot dropdown (`{components.select-dropdown}`).** Same visual family as the barber-select: border-only trigger inside the booking form, floating-shadow menu when expanded, `{colors.neutral}` hover wash on individual options.
- **Schedule row — open slot (`{components.schedule-row-open}`).** Tinted in-flow container: `{colors.neutral}` fill, no border. Hover deepens the fill to `{colors.border}`. Time label in `{colors.text-muted}` (nothing to act on yet).
- **Schedule row — booked slot (`{components.schedule-row-booked}`).** Same tinted surface as schedule-row-open (`{colors.neutral}` fill, no border, `{colors.border}` on hover), but shows the customer's name in `{colors.text}` plus a `{components.button-destructive}` "Cancel" button, right-aligned. My Appointments rows reuse this same treatment.
- **Admin account row/bar (`{components.admin-account-row}`).** Tinted in-flow container, clickable — `{colors.neutral}` resting fill, no border, with `{colors.border}` as a distinct hover state signaling interactivity. Clicking opens the account-edit modal.
- **Date-nav arrows (`{components.date-nav-arrow}`).** The two day-step controls flanking My Schedule's date header. Resting state uses `{colors.text-muted}`; hover (pointer devices only) swaps to `{colors.primary}`; a disabled edge case (should one ever exist) would use `{colors.border}`. No background, no border — icon-only, sized at 20px.
- **Admin account-edit / account-create popup (`{components.admin-account-popup}`).** The field layout inside `{components.modal}` for both variants: stacked `{components.input}` fields (and `{components.select-dropdown}` for the permission-level field, edit-only) separated by `{spacing.4}`, grouped into logical sections (identity fields, then password fields) separated by `{spacing.6}`, with `{spacing.3}` above the footer row of Cancel/Save or Cancel/Delete buttons. Same shell for edit and create — create simply omits the permission-select field per its behavioral spec in EXPERIENCE.md.
- **Confirm-action popup (`{components.confirm-popup}`).** Radix Dialog, floating surface, `{rounded.DEFAULT}` corners. Always exactly two buttons: **"Go Back"** in `{components.button-secondary}` (white/neutral) on every instance, and **"Confirm"** whose color is context-dependent — this is a real, non-obvious rule, not a stylistic flourish: `{components.button-primary}` (blue) when confirming a non-destructive action like saving an account edit, `{components.button-destructive}` (red) when confirming a destructive action like cancelling an appointment or deleting an account. The same popup shell is reused for both the account-edit and delete flows in the Admin Panel — only the Confirm button's color and destination action differ. → `mockups/confirm-popup.html` (destructive and non-destructive variants side by side).
- **Confirmation screen (`{components.confirmation-screen}`).** Post-booking, full page (not a popup) — plain `{colors.background}`, a `{colors.primary}`-accented confirmation line in `{typography.h2}` or similar weight, and the booking detail in `{typography.body}`/`{colors.text}`. Copy in this register: plain and specific, e.g. "Appointment booked with Manny at 11:00 AM on July 24." No celebratory iconography or color beyond the single primary accent. → `mockups/schedule-appointment.html` (second state in that file).
- **Home hero.** A curved/angled diagonal splits the page into a white half (left) and a `{colors.primary}`-filled half (right). The white half carries a short headline plus the `{components.button-primary}` "Schedule Appointment" CTA — exact headline copy is a content-pass decision owned by EXPERIENCE.md/copywriting, not fixed here. The blue half carries a scissor-and-comb graphic, the two crossed like an X; this is the only illustrative graphic element in the entire product. → `mockups/home.html`.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Use `{colors.destructive}` only for Cancel/Delete actions and their destructive-context Confirm button; use `{colors.error}` only for validation-message text | Use red for emphasis or decoration, use `{colors.destructive}` on validation text, or use `{colors.error}` on a button/fill |
| Keep every corner at its assigned `{rounded.*}` step — don't introduce one-off radius values outside the defined scale | Reintroduce sharp 0px corners without a documented decision, or go pill-shaped when the client asked for subtle rounding |
| Use a shadow only on floating/overlay surfaces (popups, open dropdown/select menus, modal) | Add shadow to anything sitting in normal page flow (rows, bars, form sections, in-flow input) |
| Trigger hover/active color states only on pointer/desktop input | Fire hover states from touch input, or make an action only discoverable via hover |
| Keep the confirm-popup's "Go Back" button white/neutral in every instance | Recolor "Go Back" to match the action being confirmed |
| Color the popup's "Confirm" button by consequence — blue for non-destructive, red for destructive | Default "Confirm" to red because it "feels like a confirm button," or to blue for a destructive action |
| Use one type family (Manrope) for every role, headings through captions | Introduce a second family for a "display" moment the way some brands do |
| Cap content width and keep schedule/admin views list-shaped | Stretch schedule rows or admin bars into a wide multi-column table layout |
