# Template Editor UI design review

Date: 2026-09-15  
Scope: ArcGIS Pro ribbon entry points, Template Editor dockpane, Template Settings, placement/association prompts, and placement override editor.  
Method: source review plus successful build and unit test run. ArcGIS Pro was not restarted for an interactive walkthrough, so rendered layout, keyboard behavior, screen-reader output, DPI scaling, and theme appearance remain verification gaps.

## Executive summary

Template Editor already has the right basic product shape: an operator-oriented, searchable dockpane close to the map; compact tabular scanning; persistent status; light/dark theme awareness; and small, contextual prompts that avoid covering the map. The UI is specific to utility-template placement rather than a generic dashboard, and it preserves valuable operator density.

Its main modernization opportunity is not a fashionable reskin. It is to make the existing dense workflow feel intentional, discoverable, and native to ArcGIS Pro. The highest-priority issue is keyboard visibility: custom control styles repeatedly remove WPF focus visuals without consistently replacing them. The next largest gains are exposing primary row actions without relying on a context menu, simplifying the settings information architecture, and consolidating the many hand-built visual styles into a small semantic component system.

**Verdict: Block** for accessibility because confirmed visible-focus failures remain in essential controls. Visual modernization otherwise **needs changes**, not a wholesale redesign.

## Product and task model

- **Product identity:** ArcGIS Pro operator add-in for locating, configuring, and placing utility templates.
- **Primary user pressure:** find the correct template quickly, place it accurately, understand what enhancement will run, and recover from configuration or association problems without losing map context.
- **Dominant composition:** **Operate**, with secondary **Explore** (search/filter templates) and **Configure** (settings and overrides) surfaces.
- **Domain artifact:** template rows and their placement behavior, not abstract cards or metrics.
- **Constraints worth preserving:** ArcGIS Pro visual compatibility, dense docked layouts, light/dark themes, map-visible prompts, fast repeat placement, and no new UI dependency.

## Scores

| Dimension | Score | Assessment |
|---|---:|---|
| Intentionality | 7/10 | Strong operator-oriented structure, but many local styles and glyph choices feel assembled rather than systematized. |
| Readability | 6/10 | Table scanning is good; several 8.5–10 px labels and dense forms are too small for sustained use. |
| Usability | 6/10 | Search, filters, recent/favourites, previews, and status are useful; advanced placement actions are hidden in a context menu. |
| Responsiveness / adaptation | 5/10 | Dockpane has compact mode and dynamic columns; settings and dialogs rely heavily on fixed dimensions and horizontal layouts. |
| Accessibility | 3/10 | Native WPF controls are used, but visible focus is explicitly removed across essential controls and bespoke widgets lack complete keyboard/state treatment. |
| Performance feel | 8/10 | Virtualized template rows and compact in-context prompts support a responsive operator workflow. |
| Product credibility | 8/10 | Copy and controls reflect real ArcGIS/Utility Network work; status, validation, and preview features build trust. |

## First impression and task walkthrough

The dockpane communicates “find and place a template” quickly. Search, type filters, favourites/recent views, a sortable table, and status are all present in the first work surface. The table is the correct choice for this domain and should remain the visual anchor.

The weak point appears after selecting a template. Selection itself activates placement, while continuous placement, mirroring, one-time overrides, and offset placement live in a right-click menu. That creates a gap between selection and understanding: users cannot readily see the selected template's mode or available next actions. During placement, the small prompts correctly stay near but away from the map center, yet their styling and validation behavior vary by dialog.

Settings are complete but cognitively heavy. Six wide, button-built tabs lead to long groups of checkboxes and technical fields. The arrangement mirrors feature ownership more than the user's setup sequence. The strongest modern direction is a left-side settings navigation with a concise summary/health header and progressively disclosed advanced controls—not larger cards, gradients, or ornamental dashboard chrome.

## Findings

| ID | Severity | Area | Location / evidence | Current behavior | Proposed correction | User impact |
|---|---|---|---|---|---|---|
| UI-01 | **HIGH** | Keyboard focus | `DialogAppearance.cs:74-83`; `EditorDockpaneView.cs:1070-1079, 1148-1162, 1217-1224`; `TemplateSettingsWindow.cs:1582-1587, 1622-1652, 1655-1665, 1800-1808, 1871-1879`; similar suppression in override and prompt windows | Custom styles set `FocusVisualStyle` to `null`. Some text fields change border on keyboard focus, but buttons, tabs, checkboxes, list rows, segmented controls, and chrome buttons do not receive an equivalent consistent focus-visible treatment. | Introduce one shared keyboard-focus token/style: a 2 px high-contrast outer ring or inner border with sufficient offset, plus forced-colors support. Apply it to every interactive component and keep selection visually distinct from focus. Do not suppress native focus until the replacement is attached. | Keyboard users can lose their position in essential template selection, settings, and confirmation flows. |
| UI-02 | **MEDIUM** | Primary dockpane interaction | `EditorDockpaneView.cs:397-470`; selection activation at `EditorDockpaneView.cs:520-523` | Important placement modes and actions (continuous placement, mirror, overrides, offset, favourite) are available only through the row context menu. The selected mode is not persistently summarized beside the chosen template. | Add a compact selected-template action strip below the filters or above the status footer: **Place** as the primary action, a visible continuous-mode toggle, and an overflow menu for Mirror / Overrides / Offset / Favourite. Show active mode as text plus icon. Keep the context menu as a shortcut. | Improves discoverability and reduces accidental placement or forgotten mode state without consuming much dockpane space. |
| UI-03 | **MEDIUM** | Settings navigation semantics and adaptation | `TemplateSettingsWindow.cs:355-445, 448-461`; six buttons have minimum widths of 124 px inside a horizontal `StackPanel` | The settings header looks like tabs but is implemented as unrelated buttons with manually swapped content. It has no tab/list semantics, arrow-key behavior, or narrow-width recomposition; the six minimum-width items approach or exceed the window's usable width at its 760 px minimum. | Replace the button row with a native `TabControl`, or preferably a 176–200 px left navigation list and a single scrollable content pane. Preserve tab state, use keyboard selection patterns, and collapse to a combo/navigation menu only if the window becomes genuinely narrow. | Makes settings easier to scan, improves keyboard predictability, and prevents long/localized labels from crowding or clipping. |
| UI-04 | **MEDIUM** | Information architecture | `TemplateSettingsWindow.cs:245-306, 464-485`; README lists six technical categories | The settings taxonomy follows implementation features. First-time configuration, placement behavior, advanced Utility Network targeting, appearance, and diagnostics receive similar visual weight. Long checkbox groups expose dependent options even when their parent feature is off. | Reframe around user intent: **Setup**, **Placement**, **Network associations**, **Overrides**, **Appearance & feedback**, **Advanced**. Add a compact setup health summary at the top. Disable and visually nest dependent controls; collapse target-group/layer details under “Advanced matching”. Keep safe defaults and show concise consequence text for risky switches. | Lowers setup effort and makes rare technical tuning less intimidating without removing capability. |
| UI-05 | **MEDIUM** | Typography and density | `PlacementAttributeOverrideWindow.cs:114-127, 158-161, 234-238, 289-304, 359-370`; base font is 11 px and secondary metadata reaches 8.5–10 px | The override editor compresses hierarchy with very small text. Field name, configured default, part detail, and section labels depend on size differences that become difficult to read at common desktop scaling. | Establish semantic type roles using ArcGIS/Segoe UI conventions: body 12–13 px, labels 12 px, supporting text 11 px minimum, section title 13–14 px, title 16 px. Recover space through tighter grouping and progressive disclosure rather than sub-10 px text. Use tabular numerals for numeric values. | Reduces eye strain and improves comprehension in the densest, highest-decision surface. |
| UI-06 | **MEDIUM** | Component system and visual consistency | `DialogAppearance.cs:12-83`; repeated `CreateButtonStyle`, `CreateTextBoxStyle`, `CreateComboBoxStyle`, and theme brush helpers across settings, override, parallel-copy, split, confirmation, and prompt classes | Every window rebuilds similar controls with slightly different heights, padding, hover behavior, and primary emphasis. Raw colors and square templates are duplicated, making theme and accessibility fixes expensive and inconsistent. | Create a small shared WPF resource/component layer with semantic tokens (`Canvas`, `Surface`, `Text`, `TextMuted`, `Border`, `Action`, `Danger`, `Focus`) and variants (`Button.Primary`, `.Secondary`, `.Quiet`; `Field`; `Segmented`; `DialogFooter`; `Status`). Prefer ArcGIS Pro framework resources where available. | Produces a more native, coherent UI and makes later contrast, DPI, and state fixes much safer. |
| UI-07 | **MEDIUM** | Form feedback and recovery | `ParallelCopyPromptDialog.cs:290-296`; `TextEntryPromptWindow.cs:96-101`; validation uses modal `DialogService.Show` after submit | Invalid input is reported in a separate popup, detaching cause and recovery from the field. Preview failure is logged but leaves no local state. | Add persistent inline validation under the affected field, mark invalid state, retain input, and focus the field on submit. Keep the primary action enabled unless processing. For live preview, show a subtle “Preview unavailable” status with retry guidance when failure affects confidence. | Faster recovery, less modal interruption, and clearer confidence during precision placement. |
| UI-08 | **LOW** | Dockpane filtering and controls | `EditorDockpaneView.cs:203-282, 283-310`; clear button is 18×18 px and `Focusable = false`; search icon uses the text glyph `⌕`; filters use text symbols `★` and `◷` | The search and segmented filters are compact, but Unicode glyphs vary by font/rendering, and the clear action is pointer-only with a small target. The two filter groups read as five equal tabs rather than type filters plus saved views. | Use ArcGIS-compatible vector icons, make clear-search keyboard reachable with a 24 px minimum hit target, add an accessible name, and visually separate **Type: All / Groups / Simple** from **Saved: Favourites / Recent**. Consider `Ctrl+F` focus and `Esc` clear shortcuts. | Improves polish, input flexibility, and the mental model of filtering. |
| UI-09 | **LOW** | Status communication | `EditorDockpaneView.cs:318-329, 350-383`; status uses a colored dot and most detail is placed in a tooltip | The persistent footer is useful, but a small color-coded dot plus terse text can hide actionable configuration details behind hover. | Use icon + explicit state label (Ready, Config issue, Placement active) and an inline action such as **Review settings** or **View details** for actionable states. Reserve the tooltip for extra detail. | Makes readiness and recovery visible without relying on color or hover. |
| UI-10 | **LOW** | Dialog hierarchy and modern native feel | `DialogAppearance.cs:12-71`; many dialogs use custom 44 px accent title bars and uniformly square controls | Custom chrome gives consistency but competes with ArcGIS Pro and makes every dialog equally loud. Prompt windows often repeat the title in chrome and body while primary/secondary actions share near-identical styling. | Reduce accent to a narrow semantic highlight or primary button, use the normal neutral Pro dialog surface, standardize footer alignment, and reserve destructive/status color for meaning. Keep modest radii (2–4 px) only if consistent with the host theme; avoid a cosmetic “web app” reskin. | Feels more current and better integrated with ArcGIS Pro while retaining a professional GIS-tool character. |

## Recommended modern direction

Call the direction **Native operator workspace**:

1. Keep the dockpane table, but turn selection into a clear command state: selected template summary, visible Place/continuous action, active mirror/override indicators, and overflow for secondary actions.
2. Use ArcGIS Pro-neutral surfaces and a single blue action/focus accent. Let geometry-type icons, status icons, and map-preview colors carry domain meaning; avoid decorative gradients, cards, and pills.
3. Convert Settings to left navigation with a top health summary (“Template config valid”, “Association rules current”, “3 session overrides active”). Group dependent options and hide advanced matching fields until requested.
4. Consolidate WPF resources before broad visual restyling. A modern interface here comes from coherent states, keyboard behavior, density, and hierarchy more than from novelty.
5. Standardize all prompts around: concise task title, one-sentence consequence, fields/options, inline feedback, and a footer with one primary verb plus a clearly named alternative.

## What already works and should be preserved

- The dockpane uses a sortable table rather than forcing templates into oversized cards.
- Search, result count, favourites, recent templates, and virtualization support fast scanning (`EditorDockpaneView.cs:203-310, 397-415`).
- Selected rows use both fill and a leading border, so selection is not communicated by hue alone (`EditorDockpaneView.cs:1125-1134`).
- Dialog placement intentionally avoids obscuring the map, which is appropriate for spatial decisions.
- Light/dark theme branches and centralized dialog brushes provide a useful starting point.
- Copy is generally concrete and domain-specific: “Create Parallel Copy”, “Split Underlying Line”, and “Place With Overrides” are stronger than generic “Continue” labels.

## Rejected or deferred candidates

- **Replace the table with template cards:** rejected. Cards would reduce scan density and column comparison without adding useful evidence.
- **Add animated transitions and glass/gradient styling:** rejected. Motion and ornament would compete with the map and would not improve placement confidence.
- **Move every context action into the main toolbar:** rejected. Mirror variants, favourites, and offset placement are secondary; a compact overflow is more appropriate.
- **Declare fixed dialog widths a confirmed reflow defect:** deferred. Source suggests DPI/localization risk, but an interactive DPI and text-expansion test is required before assigning defect severity.

## Verification performed

- `dotnet build TemplateEditor.csproj --no-restore` — passed with 0 warnings and 0 errors; add-in packaged successfully.
- `dotnet test TemplateEditor.Tests/TemplateEditor.Tests.csproj --no-restore` — passed, 4/4 tests.
- Static review of programmatic WPF construction, custom templates, input controls, layout dimensions, theme resources, state styles, and DAML ribbon definitions.

## Verification gaps

- ArcGIS Pro was not restarted after build, so no rendered screenshots or interactive before-state were captured.
- Keyboard-only traversal, visible focus, context-menu access, default/cancel behavior, and focus restoration were not executed in the host.
- No Narrator/NVDA screen-reader pass or UI Automation tree inspection was run.
- Light/dark theme contrast pairs were not measured from rendered pixels.
- 100/150/200% Windows scaling, localized/long labels, minimum settings-window size, and dockpane widths were not exercised.
- Empty, loading, invalid-config, missing-layer, preview-failure, and permission/version-limited states were reviewed only where represented in source.

## Prioritized implementation sequence

1. **Accessibility and component foundation:** fix UI-01 and establish shared semantic resources from UI-06.
2. **Dockpane interaction:** implement UI-02, UI-08, and UI-09 while preserving the table and compact layout.
3. **Settings relayout:** address UI-03 and UI-04, then test localization and DPI scaling.
4. **Dense editors and prompts:** resolve UI-05 and UI-07; align remaining dialogs with UI-10.

Suggested follow-ups: run an `a11y` implementation pass first, then a focused `relayout` pass for the dockpane and settings window.
