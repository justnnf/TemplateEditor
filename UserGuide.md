# Template Editor Add-in User Guide

Last updated: 2026-05-17

This guide documents the Template Editor ArcGIS Pro add-in in `C:\Code\TemplateEditor`. It explains what the add-in does, how to configure it, how each workflow behaves, and what to check when something does not work as expected.

## 1. Purpose

Template Editor is an ArcGIS Pro 3.3 add-in for placing configured utility templates from a JSON file. A template can create one feature, one non-spatial record, or a group of related features and records. The add-in can also create configured Utility Network associations, prompt for containment or structural attachment candidates, split underlying lines, and place parallel copies from selected lines.

The add-in is designed around two kinds of template records:

- `SimpleTemplates`: individual feature or table templates.
- `GroupTemplates`: larger templates made from multiple simple template references and optional configured associations.

## 2. ArcGIS Pro 3.3 Compatibility

The project is currently configured for ArcGIS Pro 3.3.x.

Important compatibility details:

- `Config.daml` declares `desktopVersion="3.3.52636"`.
- `TemplateEditor.csproj` targets `net8.0-windows7.0`, which matches ArcGIS Pro 3.3 add-in expectations.
- ArcGIS references point to `esri.arcgispro.extensions30\3.3.0.52636`.
- The build output is a `.esriAddinX` package registered by `RegisterAddIn.exe`.
- The current build has been verified with `dotnet build TemplateEditor.csproj`.

Do not assume compatibility with ArcGIS Pro 3.4 or later without rebuilding against the matching Esri SDK references and testing. ArcGIS Pro add-ins are sensitive to SDK/runtime version alignment.

## 3. Main Ribbon Commands

The add-in adds a `Templates` tab/group with these buttons:

- `Settings`: opens the settings window.
- `Open Editor`: opens the Template Editor dockpane.
- `Reload Config`: reloads the selected JSON template configuration.

The DAML also registers internal map tools:

- `SketchPointTool`: point placement tool.
- `SketchPolylineTool`: line placement tool.
- `SketchPolygonTool`: polygon placement tool.
- `AddRowTool`: point-click tool used for non-spatial table rows.

Users normally do not click these tools directly. Selecting a template in the dockpane activates the correct tool automatically.

## 4. First-Time Setup

1. Build the add-in:

   ```powershell
   cd C:\Code\TemplateEditor
   dotnet build TemplateEditor.csproj
   ```

2. Confirm the build succeeds and registers the add-in:

   ```text
   Build succeeded.
   Installed ... TemplateEditor.esriAddinX ...
   ```

3. Open ArcGIS Pro 3.3.x.

4. Open the `Templates` tab.

5. Click `Settings`.

6. Choose the template configuration JSON file.

7. Confirm the feature-layer group names and workflow settings.

8. Click `Open Editor`.

If no valid template config path is set, `Open Editor` prompts for a JSON file.

## 5. User Settings

Settings are stored per user under:

```text
%LOCALAPPDATA%\FortisAlberta\TemplateEditor\user-settings.json
```

Legacy settings may be read from:

```text
%LOCALAPPDATA%\FortisAlberta\FramingEditor\user-settings.json
```

The packaged default config path and default feature-layer group names are read from:

```text
TemplateEditor.dll.config
```

Current packaged app settings include:

- `TemplateConfigFilePath`: default JSON config path.
- `FeatureLayerGroupNames`: comma-delimited map group names treated as spatial feature layer groups.
- `ValidateConfig`: whether the add-in validates the JSON before opening the editor.

## 6. Settings Window

The settings window has three tabs.

### General

- `Template config file`: the JSON file that defines all templates.
- `Validate template configuration before opening the editor`: if enabled, the add-in validates layers, tables, fields, subtype values, group references, geometry definitions, feature IDs, and association references.

### Line Split

Controls automatic prompts to split existing lines after placing features.

- `Enable line split prompts`: master switch.
- `Prompt when eligible point features are placed on lines`: point placement split prompts.
- `Prompt when eligible line feature endpoints land on lines`: line endpoint split prompts.
- `Prompt to create a parallel copy from a selected line`: parallel-copy workflow.
- `Allow split prompts at line start points`: enable start-point split candidates.
- `Allow split prompts at line end points`: enable end-point split candidates.
- `Split search distance`: map-unit search distance around the point or endpoint.
- `Eligible point placement groups`: newly placed point feature groups that can trigger splitting.
- `Eligible line placement groups`: newly placed line feature groups that can trigger endpoint splitting.
- `Underlying target line groups`: existing line groups that can be split.

### Associations

Controls post-placement association prompts.

- `Enable automatic association prompts`: master switch.
- `Allow structural attachment prompts`: prompts for structural attachment targets.
- `Allow containment prompts for structure points`: prompts for containment in point structures.
- `Allow containment prompts for structure boundaries`: prompts for containment in boundary structures.
- `Highlight association candidates on the map`: flashes candidates before prompting.
- `Association search distance`: map-unit search distance around the placed feature.
- `Eligible placement groups`: newly placed feature groups that can trigger association prompts.
- `Structural attachment target groups`: target groups for structural attachment prompts.
- `Containment target point groups`: target point groups for containment prompts.
- `Containment target boundary groups`: target boundary groups for containment prompts.

## 7. Template Configuration JSON

The JSON root has two arrays:

```json
{
  "SimpleTemplates": [],
  "GroupTemplates": []
}
```

### Simple Template

A simple template creates one feature or one table row.

```json
{
  "Name": "Template name",
  "Description": "Optional description",
  "TemplateType": "Display category",
  "GroupLayer": "ELECTRICDEVICE",
  "SubtypeLayer": "Switch",
  "Geometry": null,
  "DefaultFieldValues": {
    "ASSETGROUP": "Switch",
    "ASSETTYPE": "Switch Unit"
  }
}
```

Fields:

- `Name`: unique template name.
- `Description`: text shown in the viewer.
- `TemplateType`: text shown in the viewer Type column.
- `GroupLayer`: map layer group or standalone table group.
- `SubtypeLayer`: subtype layer/table name under the group. Can be null for non-subtype layers or tables.
- `Geometry`: optional polygon geometry for simple templates. Used as offsets from the placement point.
- `DefaultFieldValues`: field values applied during create.

How spatial versus non-spatial is determined:

- If `GroupLayer` is listed in `FeatureLayerGroupNames`, the template is treated as a feature-layer template.
- Otherwise, it is treated as a standalone table/non-spatial record template.

### Group Template

A group template creates multiple simple templates and optional associations between them.

```json
{
  "Name": "Group template name",
  "Description": "Optional description",
  "TemplateType": "Display category",
  "SimpleTemplates": [],
  "Associations": []
}
```

### Group Simple Template Reference

Each group template references simple templates with feature IDs and optional placement geometry.

```json
{
  "Name": "Simple template name",
  "FeatureId": 1,
  "Location": [0.0, 0.0],
  "Line": null,
  "Polygon": null,
  "SketchType": null
}
```

Fields:

- `Name`: must match a simple template name.
- `FeatureId`: unique within the group. Associations use these IDs.
- `Location`: `[x, y]` offset from the placement anchor.
- `Line`: list of `[x, y]` offsets for a line.
- `Polygon`: list of `[x, y]` offsets for a polygon.
- `SketchType`: optional placement sketch override. Supported values are `LINE` and `POLYGON`.

Group placement behavior:

- Full group placement uses `Location`, `Line`, and `Polygon` offsets.
- Selected individual group parts use special behavior:
  - Point parts ignore group `Location` and place at the cursor/sketch point.
  - Line parts ignore group `Line` geometry and use the user-sketched line.
  - Polygon parts keep configured group `Polygon` geometry.

### Association Object

Configured group associations are created after group feature creation.

```json
{
  "Type": "ATTACHMENT",
  "FromFeatureId": 1,
  "ToFeatureId": 2,
  "FromTerminal": 0,
  "ToTerminal": 0
}
```

Supported `Type` values:

- `CONTAINMENT`
- `ATTACHMENT`
- `JUNCTIONJUNCTIONCONNECTIVITY`
- `JUNCTIONEDGEOBJECTCONNECTIVITYFROMSIDE`
- `JUNCTIONEDGEOBJECTCONNECTIVITYTOSIDE`
- `JUNCTIONEDGEOBJECTCONNECTIVITYMIDSPAN`

## 8. Opening and Using the Dockpane

Click `Open Editor` to show the Template Editor dockpane.

The viewer includes:

- Search box.
- Clear search button.
- Template mode radio buttons: `Groups`, `Simple`, `All`.
- Template count.
- Table columns: `Name`, `Type`, `Description`.

### Search

Search terms are split by whitespace. A template matches when all search terms are found somewhere in the template name, type, or description. Group templates also search their child part names and details.

Search highlights matched text in the table. Highlight text follows the current theme foreground color.

### Sorting

Click a column header to sort by:

- Name
- Type
- Description

Click the same header again to reverse the sort order.

### Groups View

`Groups` is the default view.

Each group row has an expand/collapse button. Expanded group parts appear as selectable rows under the group, with names formatted like:

```text
1. Template part name
2. Template part name
```

Selecting the group row activates the full group template.

Selecting a child part activates only that individual part.

### Simple View

`Simple` shows standalone simple templates that are not part of any group.

### All View

`All` shows a flat list of all simple templates and all group templates. Group rows are not expandable here. Use `Groups` when you need expandable group parts.

## 9. Placement Tools

Selecting a template automatically activates a placement tool:

- Point templates use the point sketch tool.
- Line templates use the polyline sketch tool.
- Polygon templates use the polygon sketch tool.
- Table/non-spatial templates use the add-row tool.

After sketch completion, the add-in creates the feature(s) or row(s), then returns to the select tool.

### Preview Behavior

Preview overlays are drawn while moving the mouse for configured templates.

Full group preview:

- Shows all configured point, line, and polygon parts.
- Uses group offsets and configured geometries.
- Supports rotation.

Selected individual group part preview:

- Point part: no configured offset preview; placement is at the cursor.
- Line part: no configured line preview; the sketch tool provides line feedback.
- Polygon part: configured polygon preview remains visible.

Simple template preview:

- Simple templates with configured polygon `Geometry` show that polygon.
- Simple point templates show a point marker.

### Rotation

While a preview sketch tool is active:

- Hold `R` to rotate the preview around the anchor point.
- Release `R` to end rotate mode.
- Press `E` to reset rotation.

Rotation applies to configured preview geometry and configured placement geometry.

## 10. Full Group Placement

When a full group template is selected:

1. The sketch tool is chosen from the group sketch feature.
2. The group creates each referenced simple template.
3. Each feature uses its configured geometry:
   - `Location`
   - `Line`
   - `Polygon`
   - or the sketch geometry if no configured geometry exists.
4. The group's configured `Associations` are created using `FeatureId`.
5. Post-placement enhancements may run:
   - line split prompts
   - association prompts

If configured associations fail, the add-in warns that the template was placed but is incomplete and lists failed associations.

Fallback options may be offered:

- retry without configured associations
- retry with minimal subtype/required attributes only

## 11. Individual Group Part Placement

When an expanded child row is selected:

1. Only that part is created.
2. It uses the simple template referenced by the group part.
3. It does not create the rest of the group.
4. It does not create the group's configured associations.
5. It still uses existing simple-template special behavior, including:
   - non-spatial auto association
   - SJO selected-pole structural attachment flow
   - post-placement enhancements for spatial features

Geometry rules for individual group parts:

- Point: create at clicked/sketched point.
- Line: create from user-sketched line.
- Polygon: create from configured group polygon.

## 12. Simple Template Placement

When a simple feature template is selected:

1. The matching feature layer is found by `GroupLayer` and `SubtypeLayer`.
2. The add-in builds attributes from `DefaultFieldValues`.
3. Coded domains are resolved through ArcGIS domain values.
4. The feature is created.
5. Post-placement enhancements can run.

For simple templates with configured polygon `Geometry`, the clicked point is treated as the anchor and the configured polygon is built around it.

## 13. Non-Spatial Record Placement

If a simple template's `GroupLayer` is not listed in `FeatureLayerGroupNames`, the add-in treats it as a standalone table record.

Possible behavior:

- If association prompts are disabled, the add-in asks whether to create the record without associations.
- If features are selected and matching association rules exist, the add-in prompts to create the association.
- If no candidates or rules are found, it asks whether to create the row without associations.

This is used for contained unit style workflows.

## 14. SJO Auto Attachment Workflow

SJO templates are detected when the template name, group layer, subtype layer, or default values indicate an SJO/framing/pole-link template.

When placing an SJO template:

1. The add-in looks for selected Pole features.
2. It prompts:

   ```text
   The SJO can be created as attachments for N selected Pole(s).
   Would you like to do that?
   ```

3. If you choose `Yes`, it creates one SJO per selected pole.
4. It creates a structural attachment association with `AssociationType.Attachment`.

If no selected poles are found, the add-in warns and creates the SJO without an attachment.

## 15. Parallel Copy Workflow

When selecting a line template, the add-in may prompt for parallel copy if:

- `EnableParallelCopyPrompt` is on.
- An existing line feature is selected.

If accepted:

1. Choose offset distance.
2. Choose left/right side.
3. The selected line is offset.
4. The new template is created from the offset geometry.

If not accepted, normal line sketching continues.

## 16. Line Split Workflow

After creating eligible point or line features, the add-in can prompt to split an underlying line.

Point split:

- Triggered by newly placed point feature groups in `SplitPointPlacementGroups`.
- Searches target line groups within `SplitSearchDistance`.

Line endpoint split:

- Triggered by newly placed line feature groups in `SplitLinePlacementGroups`.
- Can search start point, end point, or both.

If multiple candidate lines are found, the add-in flashes candidates and asks the user to choose.

## 17. Association Prompt Workflow

After creating eligible spatial features, the add-in can search nearby candidates and prompt for:

- structural attachment
- containment in a structure point
- containment in a structure boundary

The feature must belong to `AssociationPlacementGroups`.

Candidate targets come from:

- `StructuralAttachmentTargetGroups`
- `ContainmentPointTargetGroups`
- `ContainmentBoundaryTargetGroups`

The search uses `AssociationSearchDistance`.

If candidate highlighting is enabled, candidates flash before prompting.

## 18. Validation

When `ValidateConfig` is enabled, the add-in validates before opening the editor.

Validation checks include:

- Simple template layer/table names exist.
- Referenced fields exist.
- Subtype field is present when needed.
- Default subtype/domain values are valid.
- Group templates reference existing simple templates.
- Feature IDs are unique within each group.
- Group geometry is valid for the target geometry type.
- Association feature IDs exist in the group.

Validation is recommended when changing the JSON file.

## 19. Troubleshooting

### The editor will not open

Check:

- The template JSON path exists.
- The user settings file points to the right JSON.
- `ValidateConfig` is not blocking the editor due to config errors.

Use `Settings` to choose the file again.

### A template does not appear

Check:

- The template is in `SimpleTemplates` or `GroupTemplates`.
- The JSON deserializes correctly.
- Search text is not filtering it out.
- You are in the correct view: `Groups`, `Simple`, or `All`.

### A simple template is missing from Simple view

Simple view intentionally excludes simple templates that are part of a group.

Use `All` to see every simple template in a flat list, or `Groups` to see group members under their group.

### Group parts are not expandable in All view

This is intentional. Expandable groups only appear in `Groups`.

### Cursor is wrong

The cursor files must be copied to the build output:

```text
bin\Debug\net8.0-windows7.0\Images\cursor_point.cur
bin\Debug\net8.0-windows7.0\Images\cursor_line.cur
bin\Debug\net8.0-windows7.0\Images\cursor_polygon.cur
bin\Debug\net8.0-windows7.0\Images\cursor_row.cur
```

If they are missing, the tool falls back to the default cross cursor.

### SJO attachment prompt does not appear

Check:

- A Pole feature is selected before placing.
- The selected pole layer is visible/selectable in the active map.
- The template is recognized as SJO/framing/pole-link.
- Association prompts are enabled.

If no pole is detected, the add-in displays a message before creating the SJO without attachment.

### Configured group association fails

Check:

- The group association uses valid `FromFeatureId` and `ToFeatureId`.
- The associated features were actually created.
- The Utility Network supports the requested association type.
- Terminal IDs are correct for terminal-based junction-junction connectivity.

### Placement fails due to attributes

Check:

- `DefaultFieldValues` keys match real database field names.
- Required fields are present.
- Subtype field value matches a subtype description.
- Domain-coded values match the configured coded domain descriptions.

## 20. Build and Release

Build command:

```powershell
cd C:\Code\TemplateEditor
dotnet build TemplateEditor.csproj
```

Successful build output:

```text
bin\Debug\net8.0-windows7.0\TemplateEditor.esriAddinX
```

The custom Esri target packages and registers the add-in.

## 21. Bug and Compatibility Audit Notes

The following checks were performed during this documentation pass:

- Confirmed target framework is `net8.0-windows7.0`.
- Confirmed ArcGIS SDK references are ArcGIS Pro 3.3.0.52636.
- Confirmed DAML `desktopVersion` is ArcGIS Pro 3.3.52636.
- Confirmed cursor files are included as content and copied to output.
- Confirmed `dotnet build TemplateEditor.csproj` succeeds.
- Fixed a viewer state issue where a selected group child could remain selected after collapsing the parent or switching away from Groups view.

Possible future fixes or hardening:

- Add unit-like tests around config validation logic using sample JSON.
- Add explicit validation for null or unsupported association `Type` values.
- Convert hardcoded settings-window light colors to theme-aware resources.
- Improve diagnostic detail for SJO selection detection if field deployment varies by map.
- Replace magic association enum casts with named enum members where available in the ArcGIS SDK.
- Consider preserving selected group expansion state across reloads if users rely on large group browsing.
- Consider logging placement diagnostics to a file for support cases where ArcGIS Pro swallows edit-operation details.

## 22. Important Files

- `Config.daml`: add-in metadata, ribbon controls, tools, and dockpane registration.
- `TemplateEditor.csproj`: target framework, ArcGIS references, content packaging.
- `TemplateEditor.dll.config`: default template path and feature-layer group names.
- `TemplateEditor\EditorDockpaneView.cs`: dockpane UI.
- `TemplateEditor\EditorDockpaneViewModel.cs`: viewer state, search, sorting, activation.
- `TemplateEditor\CommonFunctions.cs`: placement, validation, geometry, associations, preview.
- `TemplateEditor\PlacementEnhancementService.cs`: post-placement split and association prompts.
- `TemplateEditor\ParallelCopyService.cs`: parallel-copy line workflow.
- `TemplateEditor\TemplateSettingsWindow.cs`: settings UI.
- `TemplateEditor\AddinConfiguration.cs`: settings and config loading.
