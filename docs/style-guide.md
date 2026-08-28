# Famick Style Guide

How Famick Home Management should look, across every surface it renders on. Written after the
sign-up pages shipped in a palette that did not match the sign-in page beside them — the point
of this document is that the next screen does not have to rediscover any of it.

**If code and this document disagree, the code is the bug.**

---

## 1. Surfaces

Three of them, with different styling mechanisms and one shared palette:

| Surface | Where | Styled by |
|---|---|---|
| Blazor web app | `Famick.HomeManagement.UI` (Razor Class Library) | MudBlazor via `Theme/FamickTheme.cs` |
| MAUI mobile app | `Famick.HomeManagement.Mobile` | `Resources/Styles/Colors.xaml` + `Styles.xaml`, plus per-page values |
| Server-rendered pages | Razor views outside the SPA | hand-written CSS in the page layout |

`FamickTheme.cs` is the **source of truth for colour**. The other two duplicate its values
because they cannot reference it — MAUI resource dictionaries and plain CSS have no access to a
C# theme object. Duplication is accepted; divergence is not.

---

## 2. Brand palette

From the logo. Defined in `FamickTheme.Colors`.

### Greens — the primary colour

| Name | Hex | Use |
|---|---|---|
| `ForestGreen` | `#518751` | **Primary.** Buttons, links, active states, app bar |
| `DarkGreen` | `#3D6B3D` | Hover and pressed states; primary in dark surfaces |
| `SageGreen` | `#7BA17C` | Primary on dark backgrounds, where ForestGreen lacks contrast |
| `DarkModeGreen` | `#2D4A2D` | App bar in dark mode only |

### Greys — secondary

| Name | Hex | Use |
|---|---|---|
| `WarmGray` | `#9E9E9E` | Secondary; input borders at rest |
| `LightGray` | `#BDBDBD` | Disabled text, subtle dividers |
| `SmokeGray` | `#A8A8A8` | From the logo's chimney smoke; decorative |
| `Charcoal` | `#424242` | Dark-mode surfaces |

### Neutrals

| Hex | Use |
|---|---|
| `#FFFFFF` | Light surface (cards, inputs) |
| `#F5F5F5` | Light page background — cards must sit *on* something |
| `#E0E0E0` | Borders and dividers |
| `#616161` | Muted / helper text |
| `#212121` | Body text |
| `#1E1E1E` | Dark surface |
| `#121212` | Dark page background |

### Semantic colours

Reserved for meaning, never for decoration. Using them as accents is what makes a UI look like
it belongs to no one.

| Meaning | Hex |
|---|---|
| Success / positive | `#4CAF50` |
| Warning | `#FF9800` |
| Error / destructive | `#D32F2F` |
| Error (web/MudBlazor default) | `#B00020` |
| Contact type: household | `#4CAF50` |
| Contact type: business | `#2196F3` |

> `#2196F3` is the **only** sanctioned blue, and only as a contact-type marker. Blue is not an
> accent colour in this product.

---

## 3. Rules

1. **Green is the primary colour, everywhere.** Mobile spent a long period on Material Blue
   (`#1976D2`) while the web used green; they now match. Do not reintroduce a second accent.
2. **Never paste a hex value that is not in this document.** If a screen seems to need a new
   colour, it usually needs an existing one — and if it genuinely doesn't, add it here first.
3. **Semantic colours mean something.** Green for success, amber for warning, red for
   destructive. Never for emphasis.
4. **Every surface reads the same preference.** It lives in `localStorage` under
   `theme_preference`, with an explicit choice beating the operating system and
   `prefers-color-scheme` used only when no choice has been made. Server-rendered pages read the
   same key, so a visitor never crosses a light/dark boundary partway through a journey. Do not
   let a new page follow the OS on its own — that is what made the sign-up pages disagree with
   sign-in.
5. **Touch targets are at least 44pt.** Already the mobile default via `MinimumHeightRequest`.
   Only deliberately secondary affordances go smaller, and they sit next to something larger.

---

## 4. Typography

Roboto, falling back to `Helvetica, Arial, sans-serif`. Set once in `FamickTheme.Typography`
and matched by the other surfaces.

| Role | Size | Weight |
|---|---|---|
| Page title | 20–22px | 500 |
| Body | 16px | 400 |
| Secondary / helper | 14px | 400 |
| Caption, field hints, validation | 12–13px | 400 |
| Button label | 14px | 500 |

Inputs must be **16px on mobile web** — anything smaller makes iOS Safari zoom the page when the
field takes focus, which reads as the layout breaking.

---

## 5. Shape, spacing, elevation

| | Web (MudBlazor) | Mobile (MAUI) |
|---|---|---|
| Corner radius | 4px (MudBlazor default) | 8px (`Styles.xaml`) |
| Card padding | 32px (`pa-8`) | 16–24px |
| Field spacing | 16–20px | 12–16px |
| Card elevation | `Elevation="3"` | flat, with a border |

The two radii differ because each follows its platform's convention. That is deliberate and not
worth unifying — a MAUI app that looks like a web page looks wrong on a phone.

Elevation 3 as CSS, for hand-written pages:

```css
box-shadow: 0 3px 3px -2px rgba(0,0,0,.2),
            0 3px 4px 0 rgba(0,0,0,.14),
            0 1px 8px 0 rgba(0,0,0,.12);
```

---

## 6. Components

**Buttons.** Filled green with white text for the primary action, one per screen. Uppercase
labels on web (MudBlazor convention); sentence case on mobile. Hover and pressed go to
`DarkGreen`. Secondary actions are text-only in `ForestGreen`, never a second filled button.

**Inputs.** Outlined, `WarmGray` border at rest, `#212121` on hover, 2px `ForestGreen` on focus.
When the focus border thickens, reduce padding by 1px so nothing shifts. Labels sit above the
field; hints below in 12px muted; validation below in 12px error.

**Cards.** White on `#F5F5F5`. Full-bleed with a small margin under about 480px wide.

**Logo.** `_content/Famick.HomeManagement.UI/images/logo-lockup.svg`, around 100px tall, centred
above the title on entry screens. The lockup already says "Famick Home Management", so a heading
underneath should not repeat it. `logo.svg` is the mark alone; `icon.svg` is for favicons.

**Product name.** "Famick Home Management" in full on first use and in page titles. "Famick"
alone is acceptable only in running prose after the full name has appeared.

---

## 7. Known drift

Honest list, so nobody treats these as intentional:

- **Mobile hardcodes colours per page** rather than referencing `Colors.xaml`. The palette is now
  consistent, but changing a brand colour still means editing many files. Worth centralising.
- **`Colors.xaml` carried the MAUI template purple** (`#512BD4`) as `Primary` long after the
  brand was green, because most pages set colours inline and never consulted it. Now corrected.
- **Two error reds** are in use: `#D32F2F` on mobile, `#B00020` from the MudBlazor default. Pick
  one when someone next touches error styling.
- **Dark mode is uneven.** Mobile supports it through `AppThemeBinding`; the Blazor app pins
  light. Until that is resolved, new server-rendered pages pin light to match the app.

---

## 8. Adding a screen

1. Take colours from §2. Add nothing new without updating this file.
2. Match the surrounding surface's light/dark decision — do not follow the OS unilaterally.
3. Logo lockup, then title, then lede, on any entry screen.
4. One primary button.
5. Check it beside the screen the user arrives from. Most mismatches are only visible in that
   comparison, which is exactly how the sign-up pages shipped wrong.
