param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$DestinationPath,

    [ValidateRange(1, 4096)]
    [int]$Width = 32,

    [ValidateRange(1, 4096)]
    [int]$Height = 32
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

function Convert-XamlVectorToPng {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputFile,

        [Parameter(Mandatory = $true)]
        [string]$OutputFile,

        [Parameter(Mandatory = $true)]
        [int]$PixelWidth,

        [Parameter(Mandatory = $true)]
        [int]$PixelHeight
    )

    # Render the loose WPF XAML at the exact bitmap dimensions required by
    # ArcGIS Pro's AddInInfo thumbnail, which does not consume vector XAML.
    $xaml = [System.IO.File]::ReadAllText($InputFile)
    $visual = [System.Windows.Markup.XamlReader]::Parse($xaml)

    if ($visual -isnot [System.Windows.FrameworkElement]) {
        throw "The root element in '$InputFile' must be a WPF FrameworkElement."
    }

    $renderSize = [System.Windows.Size]::new($PixelWidth, $PixelHeight)
    $visual.Width = $PixelWidth
    $visual.Height = $PixelHeight
    $visual.Measure($renderSize)
    $visual.Arrange([System.Windows.Rect]::new($renderSize))
    $visual.UpdateLayout()

    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
        $PixelWidth,
        $PixelHeight,
        96,
        96,
        [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)

    $outputDirectory = [System.IO.Path]::GetDirectoryName($OutputFile)
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
    }

    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))

    $stream = [System.IO.File]::Open($OutputFile, [System.IO.FileMode]::Create)
    try {
        $encoder.Save($stream)
    }
    finally {
        $stream.Dispose()
    }
}

$conversion = @{
    InputFile = [System.IO.Path]::GetFullPath($SourcePath)
    OutputFile = [System.IO.Path]::GetFullPath($DestinationPath)
    PixelWidth = $Width
    PixelHeight = $Height
}

Convert-XamlVectorToPng @conversion
