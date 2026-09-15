# Template Editor 1.8 — complete Ponytail audit

Scope: every maintained file on `Version-1.8`; generated `bin`, `obj`, `Release`, and `temp_archive` output is excluded. Target compatibility remains .NET 8 and ArcGIS Pro SDK 3.3. “Lean” means no useful reduction was found.

## Implementation status

Implemented: compiler warnings, shared map lookup, comparer-backed lookup sets,
decompiler boolean/switch artifacts, configuration initialization, converters,
Utility Network rule extraction, sketch-tool bridge methods, meaningful names in
touched flows, repository formatting rules, MCT/overlay comments, and developer
build/test guidance. Test discovery is explicit and the test project is included
in the solution; all three current tests execute and pass.

Retained deliberately: JSON round-trip settings cloning is shorter and less
error-prone than approximately 70 manual assignments. Large code-built WPF views
remain until each can be visually regression-tested in ArcGIS Pro; converting all
dialogs blindly would risk layout, focus, accessibility, and command behavior.

## Ranked findings

1. `native:` replace code-built WPF with XAML resources/templates. Potential reduction: 700–1,100 lines. [`EditorDockpaneView.cs`](TemplateEditor/EditorDockpaneView.cs), [`TemplateSettingsWindow.cs`](TemplateEditor/TemplateSettingsWindow.cs), [`PlacementAttributeOverrideWindow.cs`](TemplateEditor/PlacementAttributeOverrideWindow.cs), dialogs.
2. `shrink:` split the 2,439-line placement grab-bag into placement, association, preview, and validation static services; no interfaces or DI. Potential reduction: 250–400 lines. [`CommonFunctions.cs`](TemplateEditor/CommonFunctions.cs).
3. `shrink:` separate attribute persistence, effective-value calculation, and ArcGIS metadata inspection. Potential reduction: 100–180 lines. [`PlacementAttributeOverrideService.cs`](TemplateEditor/PlacementAttributeOverrideService.cs).
4. `stdlib:` replace uppercase-normalized lookup collections with `HashSet<string>(StringComparer.OrdinalIgnoreCase)` and immutable empty results with `Array.Empty<T>()`. Potential reduction: 80–140 lines and fewer allocations.
5. `delete:` remove verified decompiler bridge methods, false branches, delegate casts, and exception aliases. Potential reduction: 30–60 lines.

## Root, build, and tests

| File | Verdict and action |
|---|---|
| `Config.daml` | Lean. Keep IDs synchronized with classes and validate every image/class during packaging. |
| `Esri.ArcGISPro.Extensions.targets` | Vendor-like build logic; prefer Esri's matching 3.3 targets and avoid local customization. |
| `LICENSE.txt` | Lean; no change. |
| `README.md` | Add a developer section covering MCT/UI-thread rules, build command, configuration paths, and interactive checks. |
| `TemplateEditor.csproj` | Mostly lean. Tests already have a project, so verify whether `Compile Remove="TemplateEditor.Tests/**"` is redundant. Keep the explicit 3.3 references. |
| `TemplateEditor.slnx` | Lean; ensure the test project is included/discovered. |
| `TemplateEditor.Tests/TemplateEditor.Tests.csproj` | Lean; use as the sole test boundary. |
| `TemplateEditor.Tests/TemplateValidationTests.cs` | Too narrow. Add tests for settings normalization, search ranges, association normalization, and JSON value conversion. |
| `Properties/AssemblyInfo.cs` | Keep only attributes not emitted by the SDK/version target. |
| `Properties/launchSettings.json` | Keep only if Visual Studio uses this ArcGIS launch profile. |
| `Tools/Set-AddInManifestVersion.ps1` | Focused. Add a temp-ZIP test only if packaging regressions recur. |
| `.github/copilot-instructions.md` | Currently deleted in the worktree. Restore it only if repository-specific AI guidance is still maintained; otherwise commit the deletion deliberately. |

## Configuration and lifecycle

| File | Verdict and action |
|---|---|
| `AddinConfiguration.cs` | Split file persistence from in-memory state only; reuse `AtomicFileService`, simplify remaining exception/delegate artifacts. |
| `AddinModule.cs` | Lean. Keep project-open cache invalidation and symmetric event lifetime documented. |
| `TemplateEditorSettings.cs` | Replace JSON round-trip cloning with explicit copying; keep normalization centralized. |
| `TemplateConfig.cs` | Lean DTO. |
| `TemplateCache.cs` | Useful cache. Document complete-snapshot replacement and invalidation triggers. |
| `AtomicFileService.cs` | Lean; rename the temporary variable and retain same-directory atomic replacement. |
| `LogService.cs` | Lean logging boundary; add correlation only when diagnostics require it. |
| `TaskObservationService.cs` | Useful fire-and-forget boundary. Keep exception logging centralized and use it for every deliberately unawaited `Task`. |

## Models

| File | Verdict and action |
|---|---|
| `AssociationObject.cs` | Lean DTO. |
| `AssociationPromptResult.cs` | Lean named-result factories; record conversion is optional churn. |
| `AssociationRule.cs` | Lean DTO. |
| `AssociationRuleEnvelope.cs` | Lean DTO. |
| `AssociationRuleGenerationResult.cs` | Lean DTO. |
| `CandidateChoiceResult.cs` | Lean enum. |
| `DialogButtonChoice.cs` | Lean value object; do not add a dialog framework. |
| `FeedbackSeverity.cs` | Lean enum. |
| `DisplayTemplate.cs` | Lean view model; make collections read-only only after callers stop mutating them. |
| `DisplayTemplateChild.cs` | Replace manual temporary-list search-text construction with `string.Join` over non-empty values. |
| `ExistingAssociationPair.cs` | Keep one direction-insensitive `Matches` implementation; document ordering semantics. |
| `FeatureCandidate.cs` | Lean. |
| `FeatureInfo.cs` | Lean. |
| `FeatureLayerInfo.cs` | Lean. |
| `GroupTemplate.cs` | Lean DTO. |
| `LayerMetadata.cs` | Clarify disposal ownership; preferably cache plain metadata rather than disposable SDK definitions. |
| `LayerSearchContext.cs` | Lean. |
| `PlacedFeatureContext.cs` | Lean. |
| `SimpleTemplate.cs` | Lean DTO. |
| `SimpleTemplateReference.cs` | Lean DTO. |

## Association and map services

| File | Verdict and action |
|---|---|
| `AssociationRuleCatalog.cs` | Share association-name normalization with the regenerator/enhancement code; use immutable empty results. |
| `AssociationRuleJsonRegenerator.cs` | Simplify `QueuedTask.Run` delegates and SDK casts; keep discovery and projection together. |
| `FeatureInfoCache.cs` | Now lean, but rename if it remains stateless—it is not a cache. Keep calls on the MCT. |
| `MapMemberLookupService.cs` | Prefer async APIs; retain blocking `QueuedTask` wrappers only for callbacks that cannot await. |

## Placement engine

| File | Verdict and action |
|---|---|
| `CommonFunctions.cs` | Highest-priority split: placement/edit construction, configured associations, preview geometry, validation. Preserve ordering and avoid abstractions. |
| `PlacementEnhancementService.cs` | Extract candidate search and overlay-symbol creation; keep prompt serialization in the coordinator. Cache only plain summaries. |
| `ParallelCopyService.cs` | Separate pure polyline stitching/offset calculations from ArcGIS selection/edit calls for cheap tests. |
| `OffsetPlacementSession.cs` | Focused state machine; document overlay/event ownership and repeated completion/cancellation. |
| `PreviewSketchTool.cs` | Split pure rotation/throttling from lifecycle. Keep generation counters, cancellation, and overlay disposal. |
| `PreviewOverlayGraphic.cs` | Lean holder. |
| `OverlayGroup.cs` | Lean disposable aggregate; ensure idempotent disposal. |
| `GeometryTypeHelper.cs` | Prefer named SDK enum members; if 3.3 lacks them, document the numeric constants. |
| `PlacementMirrorMode.cs` | Lean enum. |

## Attribute overrides

| File | Verdict and action |
|---|---|
| `PlacementAttributeOverrideService.cs` | Split JSON/favourites, ArcGIS domain inspection, and effective-value calculation. Remove duplicate nested summary shapes where identical. |
| `PlacementAttributeOverrideWindow.cs` | Convert layout/styles/templates to XAML; keep event behavior in code-behind. |
| `PlacementAttributeEditorFieldState.cs` | Lean UI state. |
| `PlacementAttributeEditorModel.cs` | Lean UI model. |
| `PlacementAttributeEditorPartState.cs` | Lean UI state. |
| `PlacementAttributeOverrideCatalog.cs` | Lean DTO. |
| `PlacementAttributeOverrideDefinition.cs` | Lean DTO. |
| `PlacementAttributeOverrideEditorState.cs` | Lean UI state. |
| `PlacementAttributeOverrideFavourite.cs` | Lean DTO. |
| `PlacementAttributeOverrideFavouriteCatalog.cs` | Lean DTO. |
| `PlacementAttributeOverrideFavouriteSummary.cs` | Lean projection. |
| `PlacementAttributeOverrideValue.cs` | Lean DTO. |
| `PlacementAttributeOverrides.json` | Lean config; add version/schema only when migration is needed and test duplicates. |

## Dockpane, commands, and tools

| File | Verdict and action |
|---|---|
| `EditorDockpaneView.cs` | Move layout/templates/styles/triggers/bindings to XAML; retain only event behavior and column measurement. |
| `EditorDockpaneViewModel.cs` | Extract template projection/filtering as pure tested functions; keep ArcGIS activation outside them. |
| `EditorDockpane_ShowButton.cs` | Focused; simplify exception aliases and retain async observation. |
| `AddRowTool.cs` | Verify and remove `_003C_003En__0` decompiler bridge; retain only geometry-specific behavior. |
| `SketchPointTool.cs` | Consolidate shared shell in `PreviewSketchTool`; verify/remove bridge method. |
| `SketchPolylineTool.cs` | Same; keep only polyline settings. |
| `SketchPolygonTool.cs` | Same; keep only polygon settings. |
| `RelayCommand.cs` | Lean standard implementation; a command dependency would be worse. |
| `TemplateReloadConfigButton.cs` | Lean command. |
| `TemplateSettingsButton.cs` | Lean command. |
| `ToolReactivationService.cs` | Keep request-version cancellation and dispatcher fallback; simplify syntax only. |
| `ToolCursorLoader.cs` | Confirm WPF cursor stream lifetime; retain backing stream if cursors fail after GC. |

## Dialogs and visual helpers

| File | Verdict and action |
|---|---|
| `DialogAppearance.cs` | Becomes mostly unnecessary after XAML resources; until then cache frozen resources where safe. |
| `DialogService.cs` | Useful boundary; consolidate overload internals and explicitly observe dispatcher work. |
| `CandidateChoiceDialog.cs` | Convert to XAML without changing multi-candidate callback/result behavior. |
| `EnhancementConfirmationDialog.cs` | Use XAML or `DialogService.Show` if custom layout adds no behavior. |
| `FeedbackPromptWindow.cs` | Convert to XAML and remove switch artifacts. |
| `FeedbackToastWindow.cs` | Prefer native ArcGIS notifications if they meet positioning/lifetime needs; otherwise retain and simplify. |
| `LineSplitChoiceDialog.cs` | Convert to small XAML window; preserve its two-choice result contract. |
| `ParallelCopyPromptDialog.cs` | Move layout/styles to XAML; retain preview versioning/disposal. |
| `TemplateSettingsWindow.cs` | Major XAML candidate. Split parsing from control construction; `async void` is valid only for event handlers. |
| `TextEntryPromptWindow.cs` | Convert to one reusable XAML prompt; do not create a general dialog framework. |
| `ChildRowIndentConverter.cs` | Lean; later replace with an XAML trigger. |
| `GroupExpansionGlyphConverter.cs` | Lean; later prefer vector glyph/data trigger. |
| `InverseBooleanToVisibilityConverter.cs` | Lean; native triggers can replace it during XAML conversion. |
| `SearchHighlightTextBlock.cs` | Keep merged-range algorithm; test overlapping/case-insensitive terms. |
| `WindowPlacementHelper.cs` | Keep centralized clamping; document multi-monitor work-area assumptions. |

## Data and assets

| File | Verdict and action |
|---|---|
| `AllowedAssociationRules.json` | Generated and large; never hand-edit. Store generator/source version and validate deterministic output/duplicates. |
| `Images/AddInDesktopMap.png` | Lean binary; verify dimensions and contrast. |
| `Images/lineCursor.cur` | Lean; keep while referenced. |
| `Images/pointCursor.cur` | Lean; keep while referenced. |
| `Images/polygonCursor.cur` | Lean; keep while referenced. |
| `Images/tableCursor.cur` | Lean; keep while referenced. |
| `Images/ReloadVectorIcon16.xaml` | Lean; keep synchronized with dark/32px variants. |
| `Images/ReloadVectorIcon32.xaml` | Lean; keep synchronized. |
| `Images/SettingsVectorIcon16.xaml` | Lean; keep synchronized. |
| `Images/SettingsVectorIcon32.xaml` | Lean; keep synchronized. |
| `Images/TemplatesVectorIcon16.xaml` | Lean; compare geometry with dark variant to prevent drift. |
| `Images/TemplatesVectorIcon32.xaml` | Lean; keep synchronized. |
| `DarkImages/ReloadVectorIcon16.xaml` | Lean; paired asset. |
| `DarkImages/ReloadVectorIcon32.xaml` | Lean; paired asset. |
| `DarkImages/SettingsVectorIcon16.xaml` | Lean; paired asset. |
| `DarkImages/SettingsVectorIcon32.xaml` | Lean; paired asset. |
| `DarkImages/TemplatesVectorIcon16.xaml` | Verify its geometry intentionally differs from the light version. |
| `DarkImages/TemplatesVectorIcon32.xaml` | Same verification as 16px. |

## Correctness and performance risks (outside pure Ponytail scope)

- MCT/UI deadlock risk: audit every synchronous `GetAwaiter().GetResult()` call and every dispatcher transition.
- SDK resource lifetime: dispose rows, cursors, feature classes, definitions, and overlays inside the owning MCT scope.
- Candidate-search cost: avoid repeated flattened-layer scans and string normalization; precompute target sets outside `QueuedTask.Run`.
- Cache invalidation: clear metadata/template caches on project, map, layer, and configuration changes as applicable.
- Fire-and-forget work: route `Task` operations through `TaskObservationService` so exceptions are never silent.
- Interactive validation remains mandatory: a build cannot validate DAML wiring, edit operations, Utility Network rules, sketch tools, modal prompts, or overlays inside ArcGIS Pro.

`net: -1,160 to -1,880 lines, -0 dependencies possible.`
