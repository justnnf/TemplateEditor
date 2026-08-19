param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$Version
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive = [System.IO.Compression.ZipFile]::Open($PackagePath, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    $entry = $archive.GetEntry('Config.daml')
    if ($null -eq $entry) {
        throw "Config.daml was not found in '$PackagePath'."
    }

    $reader = [System.IO.StreamReader]::new($entry.Open())
    try {
        [xml]$manifest = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $manifest.ArcGIS.AddInInfo.version = $Version
    $entry.Delete()

    $updatedEntry = $archive.CreateEntry('Config.daml')
    $writer = [System.IO.StreamWriter]::new($updatedEntry.Open(), [System.Text.UTF8Encoding]::new($false))
    try {
        $manifest.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}
finally {
    $archive.Dispose()
}
