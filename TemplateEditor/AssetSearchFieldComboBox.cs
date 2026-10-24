using ArcGIS.Desktop.Framework.Contracts;

namespace TemplateEditor;

internal sealed class AssetSearchFieldComboBox : ComboBox
{
	internal static string FieldName { get; private set; } = "facilityid";

	public AssetSearchFieldComboBox()
	{
		Add(new ComboBoxItem("Facility ID"));
		Add(new ComboBoxItem("Switch Number"));
		Add(new ComboBoxItem("Site ID"));
		Add(new ComboBoxItem("Global ID"));
		SelectedItem = ItemCollection[0];
	}

	protected override void OnSelectionChange(ComboBoxItem item)
	{
		FieldName = item?.Text switch
		{
			"Switch Number" => "assetid",
			"Site ID" => "SITE_ID",
			"Global ID" => "GLOBALID",
			_ => "facilityid"
		};
	}
}
