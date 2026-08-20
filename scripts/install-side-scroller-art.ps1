[CmdletBinding()]
param(
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '..\unity-side-scroller')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Get-ItchFreeUpload {
    param(
        [Parameter(Mandatory)][string]$Slug,
        [Parameter(Mandatory)][long]$UploadId,
        [Parameter(Mandatory)][string]$OutputFile
    )

    $baseUrl = "https://gandalfhardcore.itch.io/$Slug"
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page = Invoke-WebRequest -Uri $baseUrl -WebSession $session -UseBasicParsing
    $pageToken = [regex]::Match($page.Content, 'name="csrf_token" value="([^"]+)"').Groups[1].Value
    if (-not $pageToken) {
        throw "Unable to read the itch.io download token for $Slug."
    }

    $generated = Invoke-WebRequest -Uri "$baseUrl/download_url" -Method Post -Body @{ csrf_token = $pageToken } -WebSession $session -Headers @{ 'X-Requested-With' = 'XMLHttpRequest' } -UseBasicParsing
    $downloadPageUrl = ($generated.Content | ConvertFrom-Json).url
    $downloadPage = Invoke-WebRequest -Uri $downloadPageUrl -WebSession $session -UseBasicParsing
    $downloadToken = [regex]::Match($downloadPage.Content, 'meta name="csrf_token" value="([^"]+)"').Groups[1].Value
    if (-not $downloadToken) {
        throw "Unable to authorize the itch.io file download for $Slug."
    }

    $fileInfo = Invoke-WebRequest -Uri "$baseUrl/file/$UploadId`?source=game_download" -Method Post -Body @{ csrf_token = $downloadToken } -WebSession $session -Headers @{ 'X-Requested-With' = 'XMLHttpRequest' } -UseBasicParsing
    $fileUrl = ($fileInfo.Content | ConvertFrom-Json).url
    Invoke-WebRequest -Uri $fileUrl -OutFile $OutputFile -UseBasicParsing
}

function Export-SpriteFrame {
    param(
        [Parameter(Mandatory)][System.Drawing.Bitmap]$Sheet,
        [Parameter(Mandatory)][int]$Column,
        [Parameter(Mandatory)][int]$Row,
        [Parameter(Mandatory)][string]$OutputFile
    )

    $rectangle = New-Object System.Drawing.Rectangle ($Column * 80), ($Row * 64), 80, 64
    $frame = $Sheet.Clone($rectangle, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $frame.Save($OutputFile, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $frame.Dispose()
    }
}

function Export-CharacterSequence {
    param(
        [Parameter(Mandatory)][System.Drawing.Bitmap]$Sheet,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$Row,
        [Parameter(Mandatory)][int]$FrameCount,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    for ($index = 0; $index -lt $FrameCount; $index++) {
        Export-SpriteFrame -Sheet $Sheet -Column $index -Row $Row -OutputFile (Join-Path $OutputDirectory "warrior_$Name`_$index.png")
    }
}

function Export-LayeredCharacterSequence {
    param(
        [Parameter(Mandatory)][System.Drawing.Bitmap[]]$Layers,
        [Parameter(Mandatory)][string]$Prefix,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$Row,
        [Parameter(Mandatory)][int]$FrameCount,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    for ($index = 0; $index -lt $FrameCount; $index++) {
        $frame = [System.Drawing.Bitmap]::new(80, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($frame)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $source = New-Object System.Drawing.Rectangle ($index * 80), ($Row * 64), 80, 64
            $destination = New-Object System.Drawing.Rectangle 0, 0, 80, 64
            foreach ($layer in $Layers) {
                $graphics.DrawImage($layer, $destination, $source, [System.Drawing.GraphicsUnit]::Pixel)
            }
            $frame.Save((Join-Path $OutputDirectory "$Prefix`_$Name`_$index.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $graphics.Dispose()
            $frame.Dispose()
        }
    }
}

function Export-Tile {
    param(
        [Parameter(Mandatory)][System.Drawing.Bitmap]$Sheet,
        [Parameter(Mandatory)][int]$Column,
        [Parameter(Mandatory)][int]$Row,
        [Parameter(Mandatory)][string]$OutputFile
    )

    $rectangle = New-Object System.Drawing.Rectangle ($Column * 32), ($Row * 32), 32, 32
    $tile = $Sheet.Clone($rectangle, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $tile.Save($OutputFile, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $tile.Dispose()
    }
}

function Export-SlimeFrame {
    param(
        [Parameter(Mandatory)][System.Drawing.Bitmap]$Sheet,
        [Parameter(Mandatory)][int]$Column,
        [Parameter(Mandatory)][int]$Row,
        [Parameter(Mandatory)][string]$OutputFile
    )

    $rectangle = New-Object System.Drawing.Rectangle ($Column * 32), ($Row * 32), 32, 32
    $frame = $Sheet.Clone($rectangle, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $frame.Save($OutputFile, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $frame.Dispose()
    }
}

function Export-SlimeSequence {
    param(
        [Parameter(Mandatory)][System.Drawing.Bitmap]$Sheet,
        [Parameter(Mandatory)][string]$Color,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$Row,
        [Parameter(Mandatory)][int]$FrameCount,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    for ($index = 0; $index -lt $FrameCount; $index++) {
        Export-SlimeFrame -Sheet $Sheet -Column $index -Row $Row -OutputFile (Join-Path $OutputDirectory "slime-$Color`_$Name`_$index.png")
    }
}

$resolvedProject = [IO.Path]::GetFullPath($ProjectRoot)
$targetRoot = Join-Path $resolvedProject 'Assets\Game\Resources\ThirdParty\GandalfHardcore'
$characterTarget = Join-Path $targetRoot 'Characters\Warrior'
$femaleTarget = Join-Path $targetRoot 'Characters\Female'
$slimeTarget = Join-Path $targetRoot 'Enemies\Slime'
$worldTarget = Join-Path $targetRoot 'World'
$backgroundTarget = Join-Path $worldTarget 'Backgrounds'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("easygame-side-art-" + [guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Force -Path $characterTarget, $femaleTarget, $slimeTarget, $worldTarget, $backgroundTarget, $tempRoot | Out-Null

try {
    $warriorZip = Join-Path $tempRoot 'warrior.zip'
    $characterPackZip = Join-Path $tempRoot 'character-pack.zip'
    $worldZip = Join-Path $tempRoot 'world.zip'
    $hudZip = Join-Path $tempRoot 'hud.zip'
    $slimeZip = Join-Path $tempRoot 'slime.zip'
    Get-ItchFreeUpload -Slug '2d-pixel-art-male-and-female-character' -UploadId 11012135 -OutputFile $warriorZip
    Get-ItchFreeUpload -Slug '2d-pixel-art-male-and-female-character' -UploadId 9831286 -OutputFile $characterPackZip
    Get-ItchFreeUpload -Slug '2d-pixel-art-male-and-female-character' -UploadId 10819522 -OutputFile $slimeZip
    Get-ItchFreeUpload -Slug 'free-pixel-art-sidescroller-asset-pack-32x32-overworld' -UploadId 18452546 -OutputFile $worldZip
    Get-ItchFreeUpload -Slug 'free-pixel-art-sidescroller-asset-pack-32x32-overworld' -UploadId 10272614 -OutputFile $hudZip

    $warriorExtract = Join-Path $tempRoot 'warrior'
    $characterPackExtract = Join-Path $tempRoot 'character-pack'
    $worldExtract = Join-Path $tempRoot 'world'
    $hudExtract = Join-Path $tempRoot 'hud'
    $slimeExtract = Join-Path $tempRoot 'slime'
    Expand-Archive -LiteralPath $warriorZip -DestinationPath $warriorExtract -Force
    Expand-Archive -LiteralPath $characterPackZip -DestinationPath $characterPackExtract -Force
    Expand-Archive -LiteralPath $worldZip -DestinationPath $worldExtract -Force
    Expand-Archive -LiteralPath $hudZip -DestinationPath $hudExtract -Force
    Expand-Archive -LiteralPath $slimeZip -DestinationPath $slimeExtract -Force

    $warriorSheetPath = Join-Path $warriorExtract 'GandalfHardcore Warrior.png'
    $warriorSheet = New-Object System.Drawing.Bitmap $warriorSheetPath
    try {
        Export-CharacterSequence -Sheet $warriorSheet -Name 'idle' -Row 7 -FrameCount 5 -OutputDirectory $characterTarget
        Export-CharacterSequence -Sheet $warriorSheet -Name 'walk' -Row 8 -FrameCount 8 -OutputDirectory $characterTarget
        Export-CharacterSequence -Sheet $warriorSheet -Name 'run' -Row 9 -FrameCount 8 -OutputDirectory $characterTarget
        Export-CharacterSequence -Sheet $warriorSheet -Name 'jump' -Row 11 -FrameCount 4 -OutputDirectory $characterTarget
        Export-CharacterSequence -Sheet $warriorSheet -Name 'fall' -Row 12 -FrameCount 4 -OutputDirectory $characterTarget
        Export-CharacterSequence -Sheet $warriorSheet -Name 'attack' -Row 14 -FrameCount 6 -OutputDirectory $characterTarget
        Export-CharacterSequence -Sheet $warriorSheet -Name 'death' -Row 16 -FrameCount 10 -OutputDirectory $characterTarget
    }
    finally {
        $warriorSheet.Dispose()
    }

    $femaleSource = Join-Path $characterPackExtract 'GandalfHardcore Character Asset Pack'
    $femaleLayerPaths = @(
        'Character skin colors\Female Skin3.png'
        'Female Clothing\Boots.png'
        'Female Clothing\Blue Corset.png'
        'Female Clothing\Skirt.png'
        'Female Hair\Female Hair2.png'
        'Female Hand\Female Sword.png'
    )
    [System.Drawing.Bitmap[]]$femaleLayers = @($femaleLayerPaths | ForEach-Object {
        New-Object System.Drawing.Bitmap (Join-Path $femaleSource $_)
    })
    try {
        Export-LayeredCharacterSequence -Layers $femaleLayers -Prefix 'female' -Name 'idle' -Row 0 -FrameCount 5 -OutputDirectory $femaleTarget
        Export-LayeredCharacterSequence -Layers $femaleLayers -Prefix 'female' -Name 'walk' -Row 1 -FrameCount 8 -OutputDirectory $femaleTarget
        Export-LayeredCharacterSequence -Layers $femaleLayers -Prefix 'female' -Name 'run' -Row 2 -FrameCount 8 -OutputDirectory $femaleTarget
        Export-LayeredCharacterSequence -Layers $femaleLayers -Prefix 'female' -Name 'jump' -Row 3 -FrameCount 4 -OutputDirectory $femaleTarget
        Export-LayeredCharacterSequence -Layers $femaleLayers -Prefix 'female' -Name 'fall' -Row 4 -FrameCount 4 -OutputDirectory $femaleTarget
        Export-LayeredCharacterSequence -Layers $femaleLayers -Prefix 'female' -Name 'attack' -Row 5 -FrameCount 6 -OutputDirectory $femaleTarget
        Export-LayeredCharacterSequence -Layers $femaleLayers -Prefix 'female' -Name 'death' -Row 6 -FrameCount 10 -OutputDirectory $femaleTarget
    }
    finally {
        foreach ($layer in $femaleLayers) {
            $layer.Dispose()
        }
    }

    $slimeSource = Join-Path $slimeExtract 'GandalfHardcore Slime Enemy'
    foreach ($color in 'green', 'blue', 'red') {
        $slimeSheet = New-Object System.Drawing.Bitmap (Join-Path $slimeSource "Slime $color.png")
        try {
            Export-SlimeSequence -Sheet $slimeSheet -Color $color -Name 'idle' -Row 0 -FrameCount 4 -OutputDirectory $slimeTarget
            Export-SlimeSequence -Sheet $slimeSheet -Color $color -Name 'jump' -Row 1 -FrameCount 8 -OutputDirectory $slimeTarget
            Export-SlimeSequence -Sheet $slimeSheet -Color $color -Name 'death' -Row 2 -FrameCount 4 -OutputDirectory $slimeTarget
        }
        finally {
            $slimeSheet.Dispose()
        }
    }

    $worldSource = Join-Path $worldExtract 'GandalfHardcore FREE Platformer Assets'
    $tileSheet = New-Object System.Drawing.Bitmap (Join-Path $worldSource 'Floor Tiles1.png')
    try {
        Export-Tile -Sheet $tileSheet -Column 0 -Row 0 -OutputFile (Join-Path $worldTarget 'ground-left.png')
        Export-Tile -Sheet $tileSheet -Column 1 -Row 0 -OutputFile (Join-Path $worldTarget 'ground-top.png')
        Export-Tile -Sheet $tileSheet -Column 2 -Row 0 -OutputFile (Join-Path $worldTarget 'ground-right.png')
        Export-Tile -Sheet $tileSheet -Column 1 -Row 1 -OutputFile (Join-Path $worldTarget 'ground-fill.png')
    }
    finally {
        $tileSheet.Dispose()
    }

    $normalBackground = Join-Path $worldSource 'GandalfHardcore Background layers\Normal BG'
    for ($index = 1; $index -le 5; $index++) {
        Copy-Item -LiteralPath (Join-Path $normalBackground "GandalfHardcore Background layers_layer $index.png") -Destination (Join-Path $backgroundTarget "background-$index.png") -Force
    }

    $propFiles = @{
        'Large Pine Tree.png' = 'large-pine-tree.png'
        'Small Tent.png' = 'small-tent.png'
        'Angel Statue.png' = 'angel-statue.png'
        'Tall Grass.png' = 'tall-grass.png'
        'sun.png' = 'sun.png'
        'cloud5.png' = 'cloud.png'
    }
    foreach ($entry in $propFiles.GetEnumerator()) {
        Copy-Item -LiteralPath (Join-Path $worldSource $entry.Key) -Destination (Join-Path $worldTarget $entry.Value) -Force
    }

    $hudSource = Join-Path $hudExtract 'GandalfHardcore Hp bar'
    Copy-Item -LiteralPath (Join-Path $hudSource 'Hp bar.png') -Destination (Join-Path $targetRoot 'hud-frame.png') -Force
    Copy-Item -LiteralPath (Join-Path $hudSource 'red bar.png') -Destination (Join-Path $targetRoot 'hud-health.png') -Force
    Copy-Item -LiteralPath (Join-Path $hudSource 'yellow bar.png') -Destination (Join-Path $targetRoot 'hud-exp.png') -Force
    Copy-Item -LiteralPath (Join-Path $hudSource 'Blue bar.png') -Destination (Join-Path $targetRoot 'hud-energy.png') -Force

    @"
GandalfHardcore FREE Warrior, Slime Enemy, and FREE Platformer Assets
Source: https://gandalfhardcore.itch.io/
License permits use and modification in commercial/non-commercial games.
Raw or modified asset redistribution is prohibited. This directory is gitignored.
"@ | Set-Content -LiteralPath (Join-Path $targetRoot 'SOURCE.txt') -Encoding UTF8
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host "Installed licensed side-scroller pixel art into: $targetRoot" -ForegroundColor Green
