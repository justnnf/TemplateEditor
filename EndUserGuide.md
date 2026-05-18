# Template Editor User Guide

This guide explains how to use the Template Editor add-in in ArcGIS Pro.

## What Template Editor Does

Template Editor helps you place configured utility templates in a map. A template can create:

- a single point, line, polygon, or table record
- a group of related features and records
- configured Utility Network associations between group parts
- automatic structural attachment or containment associations when supported
- optional line splits after placing features
- optional parallel copies from selected lines

The add-in is driven by a template configuration JSON file. Users normally do not edit the add-in itself.

## Opening Template Editor

1. Open ArcGIS Pro.
2. Open the project and map where you want to place templates.
3. Go to the `Templates` tab.
4. Click `Open Editor`.

If no template configuration file is selected, the add-in asks you to choose one.

## Choosing or Changing the Template Config

1. Go to the `Templates` tab.
2. Click `Settings`.
3. On the `General` tab, choose the template configuration JSON file.
4. Optional: turn on `Validate template configuration before opening the editor`.
5. Click `OK`.
6. Click `Reload Config` or reopen the editor.

Use `Reload Config` whenever the template JSON file has changed.

## Main Editor Layout

The Template Editor dockpane has:

- a search box
- `Groups`, `Simple`, and `All` filters
- a template count
- a table with `Name`, `Type`, and `Description`

Click a column header to sort. Click it again to reverse the sort.

## Searching

Type in the search box to filter templates.

Search checks:

- template name
- type
- description
- group part names

Matching text is highlighted.

Use the red `X` button to clear the search.

## Template Views

### Groups

Shows group templates. These can be expanded.

Click the arrow beside a group to show its individual parts.

Selecting the group row places the entire group.

Selecting a part row places only that one part of the group.

### Simple

Shows simple templates that are not part of a group.

### All

Shows all templates in one flat list. Groups are not expandable in this view.

Use `Groups` when you need to see or select individual group parts.

## Placing a Template

1. Select a template in the editor.
2. The add-in activates the correct placement tool.
3. Click or sketch in the map.
4. Finish the sketch.
5. The add-in creates the feature, record, or group.
6. The add-in returns to the select tool.

The tool type depends on the template:

- point templates use a point cursor
- line templates use a line sketch tool
- polygon templates use a polygon sketch tool
- table/non-spatial templates use a click-to-place tool

## Placing a Full Group

Select the main group row, not one of its expanded parts.

When you place a full group, Template Editor creates all configured parts of the group and then attempts to create the configured associations between those parts.

The group preview shows the full layout before placement.

## Placing One Part of a Group

Expand a group and select one of the numbered part rows.

Only that selected part is created.

Behavior by part type:

- point part: places at the clicked point
- line part: uses the line you sketch
- polygon part: uses the configured polygon shape

This is useful when you want one component from a group without creating the entire group.

## Preview and Rotation

Some templates show a preview before placement.

For full groups, the preview shows the configured group layout.

For individual group parts:

- point and line parts rely on the normal placement/sketch feedback
- polygon parts show the configured polygon preview

Rotation controls:

- Hold `R` and move the mouse to rotate a configured preview.
- Release `R` to stop rotating.
- Press `E` to reset rotation.

## Automatic Associations

Template Editor can help create Utility Network associations after placement.

Depending on settings and the template, it may prompt for:

- structural attachment
- containment
- configured group associations
- non-spatial record association to a selected feature

When prompted, review the message carefully and choose `Yes` only if the highlighted or selected feature is the correct association target.

## SJO to Pole Attachments

For SJO/framing/pole-link templates:

1. Select one or more Pole features in the map.
2. Select the SJO template or group part.
3. Place it.
4. When prompted, choose whether to create structural attachments to the selected Pole features.

If you choose `Yes`, the add-in creates one SJO for each selected Pole and attempts to create structural attachment associations.

If no selected Pole is found, the add-in warns you and creates the SJO without attachment.

## Non-Spatial Records

Some templates create records in a table instead of map features.

For non-spatial records:

1. Select the feature that should contain or relate to the record, if applicable.
2. Select the record template.
3. Click in the map to place/run the template.
4. Follow the association prompt.

If no valid selected feature or association rule is found, the add-in may ask whether to create the record without associations.

## Line Splitting

After placing certain point or line templates, the add-in may ask whether to split an underlying line.

Examples:

- a point is placed on top of a line
- a new line endpoint lands on an existing line

If more than one possible line is found, the add-in asks you to choose the target.

## Parallel Copy

When placing a line template, the add-in may offer to create a parallel copy from a selected line.

To use it:

1. Select an existing line feature.
2. Select a line template in Template Editor.
3. If prompted, choose parallel copy.
4. Enter the offset distance.
5. Choose the side.

The add-in creates the new line template using the offset geometry.

## Settings Summary

Open `Settings` from the `Templates` tab.

Important settings:

- template configuration file
- configuration validation
- line split prompts
- parallel copy prompts
- automatic association prompts
- association search distance
- eligible placement and target groups
- candidate highlighting

If a workflow is not prompting, check settings first.

## Reloading Templates

Click `Reload Config` after the template configuration JSON changes.

If the editor is open, it refreshes the list.

If the editor is closed, it reloads the config and shows a confirmation.

## Troubleshooting

### The editor does not open

Check that the template configuration file exists and is selected in `Settings`.

If validation is enabled, fix any validation messages before opening the editor.

### A template is missing

Check:

- the search box is clear
- the correct view is selected: `Groups`, `Simple`, or `All`
- the template exists in the configuration file
- click `Reload Config`

### I cannot see group parts

Switch to `Groups` view and click the arrow beside a group.

Group parts are not shown in `All` view.

### A group part placed the wrong geometry

Expected behavior:

- selected point part places at the cursor
- selected line part uses your sketched line
- selected polygon part uses the configured polygon
- full group placement uses all configured offsets and shapes

If this does not match what you see, reload the config and reselect the template.

### SJO did not attach to a Pole

Check:

- the Pole feature was selected before placing
- you selected the correct SJO template or group part
- you answered `Yes` to the structural attachment prompt
- the Pole layer is selectable
- automatic association prompts are enabled

### A non-spatial record did not associate

Check:

- the target feature was selected before placement
- the template has a matching association rule
- automatic association prompts are enabled
- you answered `Yes` to the association prompt

### The cursor looks wrong

Close and reopen ArcGIS Pro after rebuilding or reinstalling the add-in.

If it still looks wrong, the custom cursor files may not be installed with the add-in package.

### Placement failed

Common causes:

- required field missing from the template
- invalid subtype or domain value
- target layer/table not present in the map
- Utility Network association rule does not allow the requested association
- selected feature is not a valid association target

Read the error message, correct the template or map selection, then try again.

## Best Practices

- Reload config after changing the JSON.
- Use `Groups` view when placing grouped equipment.
- Use expanded group parts only when you intentionally want one part.
- Select association targets before placing templates that depend on selection.
- Keep validation enabled when editing or testing template configurations.
- Watch prompts carefully; they control whether optional associations and line splits happen.
