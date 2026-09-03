# Template Editor

Template Editor is an ArcGIS Pro add-in for placing configured utility templates from a JSON configuration file. It supports simple feature templates, grouped template layouts, non-spatial table records, placement previews, line splitting, parallel copy workflows, and Utility Network association prompts.

The add-in does not ship with a customer-specific template configuration. Each user or team selects their own template JSON and can generate their own optional association-rule JSON from the active map.

## Requirements

- ArcGIS Pro 3.3.x
- .NET 8 SDK for building from source
- A template configuration JSON file for the templates you want to place
- Optional: an ArcGIS Utility Network map if you use automatic associations, containment, structural attachment, or rule generation

## Add-In Commands

The ArcGIS Pro ribbon adds a `Templates` tab with three commands:

- `Settings`: choose configuration files and workflow options.
- `Open Editor`: open the Template Editor dockpane.
- `Reload Config`: reload the selected template configuration JSON.

## First-Time Setup

1. Build or install the add-in.
2. Open ArcGIS Pro and the map where templates should be placed.
3. Open the `Templates` tab.
4. Click `Settings`.
5. On the `General` tab, select your template configuration JSON file.
6. Click `OK`.
7. Click `Open Editor`.

User preferences are stored outside the add-in package at:

```text
%LOCALAPPDATA%\TemplateEditor\user-settings.json
```

Logs and generated support files use the same neutral app-data folder:

```text
%LOCALAPPDATA%\TemplateEditor
```

## Configuration Files

### Template Config JSON

The template config JSON is the primary input. It defines the simple templates, grouped templates, target layers/tables, default field values, configured geometry, and configured group associations that Template Editor can place.

The add-in stores only the selected file path in user settings. The template JSON itself is not embedded in the add-in.

### Association Rules JSON

Association rules are optional. When present, they help the add-in decide which Utility Network associations are valid for automatic prompts and configured association creation.

The default rule path is:

```text
%LOCALAPPDATA%\TemplateEditor\AllowedAssociationRules.json
```

Users can also choose another rule JSON path in `Settings > Associations > Rule Catalog`.

The source repository may contain a sample or working `TemplateEditor\AllowedAssociationRules.json`, but it is intentionally excluded from the `.esriAddinX` package. End users can generate their own rule JSON from their active map using the settings window.

### Placement Attribute Overrides JSON

Placement attribute override definitions are optional. They define which fields are available in the one-time placement override window and the session override settings.

The default override definition path is:

```text
%LOCALAPPDATA%\TemplateEditor\PlacementAttributeOverrides.json
```

The repository copy is not packaged with the add-in.

## Editor Dockpane

The dockpane includes:

- Search box with highlighted matches
- `Groups`, `Simple`, and `All` views
- Favorites and recent-template tracking
- Sortable template columns
- Placement status and configuration health indicators
- Optional compact layout

Use `Groups` when you need to expand a group and place one part. Use `All` when you want one flat list for quick searching.

## Placing Templates

1. Select a template in the dockpane.
2. The add-in activates the matching placement tool.
3. Click or sketch in the map.
4. Finish the sketch.
5. The add-in creates the feature, table row, or group.
6. Optional post-placement prompts run based on settings.

Template type behavior:

- Point templates use a point placement cursor.
- Line templates use a line sketch tool.
- Polygon templates use a polygon sketch tool.
- Non-spatial table templates run from the map click workflow and can prompt for association targets.
- Group templates create all configured parts together.
- Expanded group parts create only the selected part.

## Preview, Rotation, and Mirror

Configured group layouts can show a preview before placement. Point, line, and polygon placement tools keep preview state in sync with the selected template.

Keyboard controls:

- Hold `R` and move the mouse to rotate a configured preview.
- Release `R` to stop rotating.
- Press `E` to reset rotation.

The dockpane also supports placement mirror modes where configured geometry needs to be flipped before placement.

## Line Splitting

Template Editor can prompt to split underlying lines after placement.

Supported cases include:

- Point templates placed on eligible target lines
- Line endpoints placed on eligible target lines
- Configured group line parts with start/end split behavior

Settings control candidate distance, eligible placement groups, eligible target groups, duplicate prompt suppression, interior-only splitting, and whether the add-in always asks or auto-splits when only one candidate exists.

## Parallel Copy

When placing a line template, Template Editor can create a parallel copy from selected line features.

The workflow supports:

- Default offset distance
- Left/right side selection
- Remembering the last distance and side
- Multi-segment selected-line spans
- Optional connected-span enforcement
- Optional automatic creation when selected lines exist

The parallel-copy prompt opens near the lower-right of the ArcGIS Pro window and sizes itself to its content so controls are not clipped.

## Utility Network Associations

Template Editor can create or prompt for several association types:

- Configured group associations
- Structural attachment
- Containment in structure points
- Containment in structure boundaries or lines
- Junction-junction connectivity
- Non-spatial record associations

Association behavior is controlled in `Settings > Associations`.

Prompt modes:

- `Always ask`
- `Auto-create when one candidate`
- `Review multiple only`
- `Never create`

Configured group associations can run in:

- `Fast`: batch configured associations into one edit operation.
- `Debug`: create associations one at a time to isolate exact failures.

## Placement Attribute Overrides

Template Editor supports two override scopes:

- Session overrides: configured in Settings and applied across placements.
- One-time placement overrides: opened from the template context workflow and applied only to the next placement.

Override definitions are loaded from the optional app-data JSON file. Presets for one-time overrides are saved in:

```text
%LOCALAPPDATA%\TemplateEditor\placement-override-favourites.json
```

## Settings Tabs

The settings window is organized into:

- `General`: template config path and validation.
- `Line Split`: split prompting, candidate limits, target groups, and target names.
- `Parallel Copy`: offset defaults, multi-segment behavior, and selected-line automation.
- `Associations`: prompt modes, search distances, fallback groups, and rule catalog generation.
- `Attribute Overrides`: session-level placement attribute overrides.
- `Interface`: compact layout, recent template count, map hint colors, and diagnostics.

## Troubleshooting

### The Editor Does Not Open

Check that a template configuration JSON is selected in `Settings > General` and that the file still exists.

If validation is enabled, resolve validation errors before opening the editor.

### A Template Is Missing

Check that:

- The search box is clear.
- The correct view is selected.
- The template exists in the selected config JSON.
- `Reload Config` has been run after editing the JSON.

### Group Parts Are Not Visible

Switch to `Groups` view and expand the group row. Group parts are not expanded in the `All` view.

### Associations Are Not Prompting

Check:

- Association prompts are enabled.
- The target layer is selectable and present in the map.
- A valid association rule exists or fallback target settings match the map.
- Search distances are large enough for the placement.
- The selected prompt mode allows prompting.

### Split Prompts Are Not Appearing

Check:

- Line split prompts are enabled.
- The placed template group is listed as eligible.
- The target line group or subtype/layer name is listed as eligible.
- The split search distance is large enough.
- Interior-only splitting is not filtering out endpoint candidates.

### Parallel Copy Is Not Offered

Check:

- The selected template is a line template.
- At least one source line is selected.
- Parallel copy prompts are enabled.
- Multi-segment settings match the selected geometry.

### Placement Fails

Common causes:

- Missing target layer or table
- Invalid subtype or domain value
- Required field missing from defaults
- Utility Network rule does not allow the requested association
- Selected feature is not a valid target
- The active map is connected to `DEFAULT` and default-version placement prevention is enabled

Read the popup details, adjust the template/config/map selection, and try again.