---
name: "BLUEVERSE Product Design System"
colors:
  coast-paper: "#F7F6F0"
  coast-sand: "#E9EFF0"
  coast-pearl: "#FBFDFD"
  coast-ink: "#18394C"
  coast-deep: "#205C79"
  coast-blue: "#347B9B"
  coast-teal: "#43858A"
  coast-glass: "#C9E0E6"
  coast-sage: "#E5EFF0"
  coast-muted: "#5C7481"
  coast-line: "#D6E2E7"
---

# Design System: BLUEVERSE Coastal

## Scope and source of truth

This is the canonical visual system for React Web and Flutter Mobile. It
defines their shared palette, typography, imagery, component character,
spacing, motion and accessibility expectations. The named web tokens are
declared in the [web Tailwind entry stylesheet](apps/web/src/styles/index.css),
and Flutter maps the same values in the
[mobile theme](apps/mobile/lib/ui/blueverse_theme.dart). Keep both
implementations aligned with this guide when the shared visual language changes.

Use Tailwind utilities and shared React components on the web. Use Flutter's
native Material widgets and `ThemeData` on mobile. Screen layouts and
navigation adapt to each platform, while color roles, type hierarchy, image
treatment, control shapes and interaction states remain part of one product
system. Cross-platform product and accessibility requirements live in the
[BLUEVERSE UI Experience Principles](docs/project/ui-experience-principles.md).

## 1. Visual Theme & Atmosphere

BLUEVERSE is a calm, coastal editorial experience. A soft paper canvas and
cool sea-glass surfaces frame deep ocean-blue text and actions. Daylight coastal
photography, restrained teal details, generous spacing and confident display
headings connect the interface to shorelines, marine life and coastal
livelihoods. The overall feel is open and considered, with enough structure for
account work without making the product feel like an operations console.

Use composition and real coastal context to create interest: pair photography
with explanatory copy, vary section proportions and give important actions
clear space. Keep dense information secondary to the user's task. The visual
system should feel welcoming and credible across the public home page,
authentication, profile and dashboard surfaces.

## 2. Color Palette & Roles

The values below are the canonical shared color tokens. Their web names are
Tailwind theme tokens; Flutter exposes matching `BlueversePalette` constants.
Use these roles rather than introducing near-duplicate colors in component
styles or widget themes.

| Token | Hex | Role |
|---|---|---|
| `coast-paper` — Coast Paper | `#F7F6F0` | Main page canvas and light shell background. |
| `coast-sand` — Quiet Sand | `#E9EFF0` | Cool, pale section and authentication-page background. |
| `coast-pearl` — Salt Pearl | `#FBFDFD` | Raised panels, cards and form surfaces. |
| `coast-ink` — Coast Ink | `#18394C` | Primary text and the deep footer surface. |
| `coast-deep` — Ocean Deep | `#205C79` | Main brand color, primary actions and prominent links. |
| `coast-blue` — Clear Blue | `#347B9B` | Interactive hover/focus color and secondary blue emphasis. |
| `coast-teal` — Sea Teal | `#43858A` | Small natural accent and selected positive feedback. |
| `coast-glass` — Sea Glass | `#C9E0E6` | Soft highlight, focus-ring companion and light-on-dark accent. |
| `coast-sage` — Shore Sage | `#E5EFF0` | Quiet feature panels, chips and icon backgrounds. |
| `coast-muted` — Tide Muted | `#5C7481` | Supporting copy, helper text and metadata. |
| `coast-line` — Tide Line | `#D6E2E7` | Subtle borders and dividers. |

### Color use

- Keep Coast Paper as the default page canvas. Use Quiet Sand to distinguish a
  section or page shell and Salt Pearl to lift the content that needs focus.
- Use Ocean Deep for the main action and Clear Blue for interaction feedback.
  Keep Sea Teal as a secondary accent instead of a competing primary.
- Use Coast Ink for readable body text; use Tide Muted for secondary copy only
  when the background keeps it legible.
- On dark Coast Ink or Ocean Deep surfaces, use white text and Sea Glass for
  restrained highlights.
- Use clear text, iconography or shape in addition to color to communicate
  status. Validation and error states use functional red colors; these are not
  additional brand tokens.
- Check contrast for each foreground/background pairing when adding a new
  combination. The palette declaration alone does not guarantee accessible
  contrast at every text size.

## 3. Typography Rules

### Font Stacks

The shared web stylesheet declares Manrope first for body copy and Sora first
for display text, with Aptos, Segoe UI and system sans-serif fallbacks. Flutter
uses those same family names in `BlueverseTheme`, with the operating system's
available sans-serif as its fallback. Neither client currently bundles these
fonts, so the names express the intended hierarchy; actual glyphs can use a
platform fallback. Do not describe Manrope or Sora as guaranteed loaded fonts
until the project bundles or explicitly loads them.

### Hierarchy, Weights and Spacing

- **Display headings:** Use the display stack, sentence case, strong weight,
  close leading and modest negative tracking. The home hero scales fluidly;
  section and page headings grow from mobile to desktop.
- **Section headings:** Use a prominent display face and sentence case, scaling
  from compact mobile headings to larger web section headings. Use weight and
  scale, not all-caps, to establish hierarchy.
- **Body copy:** Use the sans stack, typically 14–16px with relaxed 24–28px
  line-height. Keep explanatory paragraphs at a comfortable reading width.
- **Labels and eyebrows:** Small, bold, sometimes letter-spaced text is used
  sparingly for section metadata. Keep field labels plain and visible.
- **Monospace:** Reserve for code and technical values, not routine site labels.

Treat the observed scale as a shared hierarchy rather than a platform-specific
pixel prescription. Preserve readable line lengths, wrapping and hierarchy on
narrow screens and at larger text scales.

## 4. Component Stylings

### Buttons and Links

Primary buttons use Ocean Deep with white text, a full pill shape and a quiet
shadow. On hover they may lift slightly and shift toward Clear Blue. Secondary
actions use a white or transparent fill with a Tide Line border, or a soft
background when grouped in a panel. Main calls to action are generally 48–52px
tall; compact account actions may be smaller.

Keep visible keyboard focus, a clear disabled state and a subtle pressed
response. Hover effects may reinforce an action but cannot be its only cue.
Respect reduced-motion preferences.

### Cards and Panels

Cards use Salt Pearl, Coast Paper, Quiet Sand or Shore Sage depending on their
place in the page. A Tide Line border and soft Coast Ink shadow provide
separation without heavy elevation. Larger marketing panels often use
generous padding and 1.75rem–3rem corners; small status chips and avatars use
pills or circles.

Vary panel sizes and placement to support the content. Use a photo when it
adds coastal context, not as a decorative requirement for every panel. Avoid
repeating identical three-card grids when a more natural image-and-copy
composition better serves the section.

### Navigation and Shared Shell

The web header is sticky with a stable height, a light translucent Coast Paper
background, subtle lower border and compact wave mark. It is 76px tall; the
compact authentication header is 64px on narrow screens and 76px from the
small breakpoint upward. Desktop navigation and account controls collapse
into a mobile menu on narrow screens. Flutter uses native app navigation and
touch-sized controls instead of reproducing this web shell.

Signed-in navigation uses one account disclosure for account switching,
profile, dashboard, account addition and sign-out actions. Keep it keyboard
operable and preserve a stable shared shell around route content.

The full footer uses Coast Ink with grouped exploration, coastal-focus and
account links. A floating Back to top control appears after the user has
scrolled through 12% of the available page distance. Compact layouts may use
the small footer treatment.

### Forms and Inputs

Place visible labels above fields. Inputs are white, usually at least 48px
high, with a Tide Line border, rounded corners and comfortable horizontal
padding. Focus changes the border to Clear Blue and adds a Sea Glass ring.
Keep validation and recovery copy next to the relevant field or action, and
announce important errors and status changes accessibly.

### Photography and Coastal Illustration

Use the checked-in coastal images with descriptive alternative text and
intentional crops. The web home and authentication surfaces use large
image-and-copy compositions. Flutter's signed-out launch experience uses a
full-screen, swipeable coastal image carousel with a Coast Ink gradient overlay,
side arrows, Skip/Back/Next controls and a final sign-in/create-account slide.
Keep text on a quiet part of each image or provide enough overlay for
legibility. Use simple wave marks and restrained line illustrations as
supporting brand details, not as decoration that competes with the content.

### Loading and Recovery

Use the shared branded loading screen for asynchronous work. Its coastal
gradient, wave and marine shapes are reserved for meaningful waits; account
transitions use specific, calm messages. Longer loads reveal the completed
page with the wave treatment. Empty, error and recovery states keep the shared
site structure and offer plain next steps rather than exposing internal
service details.

### Interaction, Accessibility and Motion

- Give web controls visible keyboard focus. Make controls on both platforms
  usable by keyboard or touch, with native semantics and comfortable hit areas.
- Preserve readable text contrast, labels, semantic status/error announcements
  and descriptive image alternatives.
- Use hover and focus states on the web and clear pressed/focused states on
  mobile to explain affordances; never hide important actions behind hover.
- Keep transitions brief and purposeful: menu entrances, small button lifts,
  image crops and the shared loading transition.
- Honor reduced-motion settings on both platforms. Disable nonessential
  movement and replace smooth scrolling and page transitions with reduced or
  immediate behavior where appropriate.
- Keep disabled and in-progress states distinct, and avoid using motion or
  bright color to imply urgency without a real user action.

## 5. Layout Principles

### Grid and width

The web client uses Tailwind's mobile-first responsive utilities. Marketing
sections generally share a centered max-w-7xl container; the profile workspace
uses a wider max-w-[90rem] container, and authentication panels use max-w-6xl.
Flutter uses adaptive native constraints and safe areas rather than web grid
classes. On both platforms, keep paragraph measures narrower than full section
width.

Compose sections according to their content: single-column flow on narrow
screens, editorial multi-column grids at larger breakpoints, and occasional
asymmetric spans for photography or feature panels. The profile area adds a
navigation rail at the large breakpoint while retaining a stacked mobile
layout.

### Spacing and page edges

The web implementation mostly follows Tailwind's default 4px-based spacing
scale; Flutter uses matching increments in logical pixels. Common page gutters
grow from about 20px on narrow screens to 32px at tablet widths and 48px at
large widths. Major web sections use generous vertical spacing; mobile screens
keep the same breathing room without copying desktop dimensions.

Use whitespace to separate ideas and keep the layout calm. Avoid compressing
forms, navigation or action groups simply to fit more content above the fold.

### Responsive behavior

The web layout is mobile-first and uses Tailwind's small, medium and large
breakpoints, plus a few short-viewport adjustments. Flutter adapts to portrait,
landscape, safe areas and compact heights using native constraints. Stack grids
and preserve comfortable page gutters on small screens; introduce wider
columns only when the content can use them.

Web authentication pages place the coastal photo before the form on mobile. On
desktop, sign-in places the form left and photo right; registration places the
photo left and form right. Flutter uses its own focused auth screens. Crop
images responsively rather than shrinking the text until it becomes difficult
to read.

### Alignment and visual balance

Use left-aligned headings and body copy for reading. Center alignment is
appropriate for a focused single action or compact recovery state. Balance
photography with meaningful copy and clear actions; do not put essential text
over a busy image.

### React Web App — `apps/web`

React applies the shared system with Tailwind CSS utilities. Its token and base
style source is the [Tailwind entry stylesheet](apps/web/src/styles/index.css);
its coastal photos are in [the web assets folder](apps/web/src/assets/coastal).
Keep the shared site header, footer and page shells reusable. Use the desktop
editorial compositions and responsive breakpoints described above while
preserving clear single-column reading and comfortable gutters on narrow
screens.

### Flutter Mobile App — `apps/mobile`

Flutter applies the same palette and visual roles through native Material 3
widgets and the [Flutter theme](apps/mobile/lib/ui/blueverse_theme.dart). Its
signed-out launch carousel is implemented in the
[onboarding screen](apps/mobile/lib/ui/onboarding_screen.dart), with bundled
coastal photography in the [mobile assets folder](apps/mobile/assets/coastal/onboarding).
Session restoration and launch routing are in [the app entry point](apps/mobile/lib/main.dart).
Keep native safe-area,
touch-target and compact-height behavior while carrying through the shared
colors, type hierarchy, image treatment, rounded controls and calm motion.

## 6. Design System Notes for Stitch Generation

Use this document as the visual brief when creating or revising a React Web or
Flutter Mobile screen, or generating a design in Stitch. Preserve the coastal
editorial atmosphere, palette roles, responsive/adaptive composition and
accessible interaction states. Treat generated output as a proposal: reconcile
it with both clients' implemented tokens, the workflow contract and the public
product scope before adopting it.

### Language to Use

Describe the interface as calm, daylight, coastal, editorial, spacious,
human-centered and grounded in real shoreline context. Ask for ocean-blue
actions, cool sea-glass surfaces, natural coastal photography, readable
typography and varied image-and-copy layouts.

### Color References

Use the named color roles and hex values from the palette above. Keep Coast
Paper and Quiet Sand as light foundations, Ocean Deep for primary actions,
Clear Blue for interaction feedback, and Sea Teal as a restrained accent.
Preserve functional red states for errors and validation instead of treating
them as brand colors.

### Product Language and Visual Boundaries

Use imagery, labels and examples that fit coastal discovery, marine awareness,
safety, environmental resilience and coastal livelihoods. Match claims and
status to implemented behavior. Identify demo content when it is used, and do
not invent live coastal readings or capabilities to fill space.

Avoid mission-control, military, sonar and tactical styling; dark dashboard
canvases; neon or unrelated orange/yellow accents; unexplained metric grids;
and generic filler cards. Keep the experience calm and editorial while making
real account and workflow actions easy to find.

### Component Prompts

- “Design a welcoming BLUEVERSE sign-in page with a soft paper background,
  ocean-blue pill action, clearly labeled fields and a daylight coastal photo.
  Keep the form readable on mobile and place the photo before it on narrow
  screens. Use a visible keyboard focus state and restrained motion.”
- “Create a coastal overview page with an editorial heading, a broad image
  paired with useful account context, spacious light surfaces and a compact
  ocean-blue action. Keep operational data only where the current workflow
  provides it, and adapt the layout cleanly to mobile.”
- “Create a full-screen BLUEVERSE mobile onboarding carousel using coastal
  photography, a readable ocean-ink gradient, compact page indicators and
  accessible previous/next arrows. Show Back after the first slide, Next before
  the final slide and Skip on every introduction slide. End with clear Sign in
  and Create account actions. Use the shared palette, type hierarchy and
  reduced-motion behavior.”

### Incremental Iteration

Start from the real user workflow and content. Check mobile composition,
keyboard focus, contrast, reduced motion, loading and error states, then
compare the generated design with the source tokens in the [web stylesheet](apps/web/src/styles/index.css)
and [Flutter theme](apps/mobile/lib/ui/blueverse_theme.dart). Reject new colors
or components that imply unimplemented capabilities.
