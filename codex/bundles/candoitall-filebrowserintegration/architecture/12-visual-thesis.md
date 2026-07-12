# FileTools Sandbox Visual Thesis

## Visual thesis

A calm, dense file workbench: warm neutral surfaces, near-black type, and one restrained cobalt accent, with hierarchy coming from spacing and typography rather than nested cards or heavy borders.

## Content plan

This is operational UI, not a marketing page. The first viewport begins with the working surface:

1. A slim product/context bar with scenario, density, view, and theme controls.
2. A source rail plus the dominant FileBrowser workspace.
3. A host-owned FileInteraction surface when a file is invoked.
4. A compact evidence/status strip for events, freshness, save state, and errors.

Each region has one job. Labels describe scope and behavior; no aspirational hero copy, dashboard-card mosaic, or ornamental panel is allowed.

## Interaction thesis

- Density and list/card changes use a short layout/opacity transition so floating-window adaptation is legible without feeling animated.
- Rows/cards gain a restrained accent edge and action reveal on hover/focus; keyboard focus remains stronger than hover.
- Context menus and the interaction surface enter quickly with scale/opacity, remain inside a dedicated overlay layer, and honor reduced-motion preferences.

## Responsive intent

- Standard: full filter/sort/source/status context for the project-browser tab.
- Compact: same capabilities with compressed chrome for dialogs and ordinary floating windows.
- Minimal: location, search, essential navigation/actions, and the file list for low-height/narrow canvas windows; advanced controls remain discoverable rather than clipped.
- The results region is the primary scroll owner. The page must not gain horizontal overflow at 720x520, 560x360, 480x360, or 390px mobile width.
