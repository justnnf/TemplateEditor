using TemplateEditor;
using Xunit;

namespace TemplateEditor.Tests;

public sealed class UserDataMigrationTests
{
    [Fact]
    public void CopyMissingFile_CopiesOnceWithoutReplacingRoamingSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), "TemplateEditorTests", Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "local", "user-settings.json");
        string destination = Path.Combine(root, "roaming", "user-settings.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, "local");

            Assert.True(AddinConfiguration.CopyMissingFile(source, destination));
            Assert.Equal("local", File.ReadAllText(destination));

            File.WriteAllText(destination, "roaming");
            Assert.False(AddinConfiguration.CopyMissingFile(source, destination));
            Assert.Equal("roaming", File.ReadAllText(destination));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
