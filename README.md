# Template Editor 2.0.7

Template Editor is an ArcGIS Pro 3.3 add-in for placing configured utility templates from a JSON configuration file. It supports simple feature templates, grouped template layouts, non-spatial table records, placement previews, line splitting, parallel copy workflows, and Utility Network association prompts.

Version 2 opens in a narrow left sidebar tabbed with ArcGIS Pro's Contents pane by default. It also adds line-end terminal choices during sketching and reduces work during post-placement split and association checks.

The add-in does not ship with a customer-specific template configuration. Each user or team selects their own template JSON and can generate their own optional association-rule JSON from the active map.

## Requirements

- ArcGIS Pro 3.3.x
- .NET 8 SDK for building from source
- A template configuration JSON file for the templates you want to place
- Optional: an ArcGIS Utility Network map if you use automatic associations, containment, structural attachment, or rule generation

## Add-In Commands

The ArcGIS Pro ribbon adds a `Templates` tab with three buttons:

- `Settings`: choose configuration files and workflow options.
- `Open Editor`: open the Template Editor dockpane.
- `Reload Config`: reload the selected template configuration JSON.

The ribbon has a separate **Find Asset** group to the right of Templates. Choose Facility ID (`facilityid`), Switch Number (`assetid`), Site ID (`SITE_ID`), or Global ID (`GLOBALID`), enter an exact value, and press Enter. The add-in searches matching fields in the active map's feature layers and standalone tables, then adds up to 100 matching rows to the existing selection. Non-spatial matches are selected in their standalone tables. Search errors are logged to `%LOCALAPPDATA%\TemplateEditor\template-editor.log`.

## First-Time Setup

1. Build or install the add-in.
2. Open ArcGIS Pro and the map where templates should be placed.
3. Open the `Templates` tab.
4. Click `Settings`.
5. On the `General` tab, select your template configuration JSON file.
6. Click `OK`.
7. Click `Open Editor`.

## Building, Packaging, and Deployment

Build a versioned package from the repository root:

```powershell
dotnet build TemplateEditor.csproj -c Release
```

Every normal build also copies the package to `Release` with the version from `TemplateEditor.csproj`. For version 2.0.7, the file is `Release/TemplateEditor.ArcPro.3.3.v.2.0.7.esriAddinX`. A Debug build also installs the add-in locally by default; pass `-p:AutoDeployArcGISAddIn=false` to build Debug without installing it.

Use an explicit MSBuild target when needed:

- `dotnet msbuild TemplateEditor.csproj -target:DeployArcGISAddIn` builds and installs the current package for ArcGIS Pro. Restart ArcGIS Pro to load it.
- `dotnet msbuild TemplateEditor.csproj -target:PublishArcGISAddIn` copies the current package to `Release` with its versioned filename.

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

### Placement Attribute Overrides

Override fields are configured directly under `Settings > Attribute Overrides`.
Enter one field per line using `FieldName | Label | Description | DomainName`;
only the field name is required. Definitions and session values are saved with
the add-in's normal user settings—there is no separate override JSON catalog.

Template preview fill/line and outline colors are configurable under
`Settings > Interface > Template Preview Colors` using six-digit HEX values.

## Editor Dockpane

The dockpane includes:

- Search box with highlighted matches
- `Groups`, `Simple`, and `All` views
- Favorites and recent-template tracking
- Sortable template columns
- Placement status and configuration health indicators
- Optional compact layout

The editor uses the narrow sidebar layout by default and opens in the Contents pane's left-side dock group as another tab. You can switch the sidebar layout in `Settings > Interface`; ArcGIS Pro controls the saved position after you move the pane.

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

Right-click a template to place it continuously, mirror its configured geometry, or set one-time attribute overrides. Continuous placement keeps the selected tool active after a successful placement until you stop it.

For Utility Network line sketches, the add-in uses the device each endpoint snaps to. It prompts for that device's terminal after the first vertex and at the final vertex, using the junction-edge rules for the selected line asset type. An endpoint without a valid device snap has no terminal prompt. The From and To choices follow the first and last sketch vertices and are written to the line's terminal fields when it is created. Canceling a choice leaves that endpoint's terminal at the template default. These prompts apply to line sketches; parallel copy uses its existing placement workflow.

`Settings > General > Line End Terminals` filters these choices by terminal name. `Ignored terminal names` defaults to `Single Terminal`, so those endpoints are placed with terminal `None` without a prompt. Add other comma-separated names to ignore, or enter `Allowed terminal names` as a whitelist; a blank whitelist allows all names not ignored. If filtering removes every terminal at an endpoint, the add-in writes `None` (code `0`) for that endpoint, even when the line template specifies a terminal default. The terminal prompt also offers an explicit `None` choice. Matching is case-insensitive. These settings do not affect parallel copy.

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

Settings control candidate distance, eligible placement groups, eligible target groups, duplicate prompt suppression, interior-only splitting, and whether the add-in always asks or auto-splits when only one candidate exists. The Line Split settings also include an included target asset groups list. It defaults to `ElectricLine: Overhead Primary` and `ElectricLine: Underground Primary`, so only those lines are offered as split candidates. Entries are comma-separated `FeatureClass: Asset Group` pairs; an asset group name without a feature class applies to all target line classes. Clear the list to allow every asset group.

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

Candidate prompts can highlight the selected feature on the map, controlled by `Settings > Interface`. Association prompts zoom only when the placed feature and candidates fall outside the current view. Line split prompts keep the current map extent so a long candidate line does not pull the view away from the placement point. Post-placement searches reuse layer and rule information where possible to reduce repeated work.

Prompt modes:

- `Always ask`
- `Auto-create when one candidate`
- `Review multiple only`
- `Never create`

Configured group associations can run in:

- `Fast`: batch configured group associations into one edit operation.
- `Debug`: create associations one at a time to isolate exact failures.

Automatic prompts that create associations with multiple targets run the target edits together on the ArcGIS main thread while keeping each association as a separate operation.

## Placement Attribute Overrides

Template Editor supports two override scopes:

- Session overrides: configured in Settings and applied across placements.
- One-time placement overrides: opened from the template context workflow and applied only to the next placement.

Override definitions and session selections are saved in `user-settings.json`. Favorites for one-time overrides are saved in:

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
- `Interface`: sidebar and compact layouts, recent template count, map hint and preview colors, and diagnostics.

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

### Terminal Choices Are Not Appearing

Check that the line is being drawn with the line sketch tool, the endpoint snaps to a device feature in the active map, the line belongs to a Utility Network, and a junction-edge rule permits a terminal on that device for the line's asset group and type. The line layer must have `FROMDEVICETERMINAL` and `TODEVICETERMINAL` fields.

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

## Developer notes

Build the ArcGIS Pro 3.3 add-in from the repository root:

```powershell
dotnet build TemplateEditor.csproj --no-restore
dotnet test TemplateEditor.Tests/TemplateEditor.Tests.csproj --no-restore
```

ArcGIS Core and Mapping objects must be read or changed on the Main CIM Thread
through `QueuedTask.Run`. WPF windows and controls must be accessed through the
application dispatcher. Keep disposable ArcGIS rows, cursors, definitions,
feature classes, and overlay handles inside the scope that owns them.

A successful build verifies compilation and packaging, but not DAML registration
or interactive behavior. Before release, exercise point, line, polygon, and table
placement; terminal choices at both line ends; preview rotation; offset/parallel
copy; line splitting; Utility Network associations; settings persistence; and
light/dark UI resources in ArcGIS Pro 3.3.
