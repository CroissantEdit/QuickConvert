$script:QuickConvertExtensions = @(
    '.png', '.jpg', '.jpeg', '.webp', '.avif', '.bmp', '.gif',
    '.mp3', '.ogg', '.opus', '.wav', '.flac', '.m4a', '.aac',
    '.mp4', '.mov', '.webm', '.mkv', '.avi', '.wmv'
)

function Get-QuickConvertLegacyShellEntries {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ClassesRoot,
        [Parameter(Mandatory)][string]$CommandStoreRoot
    )

    $paths = @(
        (Join-Path $ClassesRoot '*\shell\QCTest'),
        (Join-Path $ClassesRoot '*\shell\QuickConvert'),
        (Join-Path $ClassesRoot 'SystemFileAssociations\.png\shell\QCE'),
        (Join-Path $ClassesRoot 'SystemFileAssociations\.png\shell\QCTestB'),
        (Join-Path $ClassesRoot 'SystemFileAssociations\.png\shell\QCTestC'),
        (Join-Path $CommandStoreRoot 'QCTest.A1'),
        (Join-Path $CommandStoreRoot 'QCTest.A2'),
        (Join-Path $CommandStoreRoot 'QCStore.A'),
        (Join-Path $CommandStoreRoot 'QCStore.B')
    )

    foreach ($extension in $script:QuickConvertExtensions) {
        $paths += Join-Path $ClassesRoot "SystemFileAssociations\$extension\shell\QuickConvert"
    }

    if (Test-Path -LiteralPath $CommandStoreRoot) {
        $paths += Get-ChildItem -LiteralPath $CommandStoreRoot -ErrorAction SilentlyContinue |
            Where-Object {
                $_.PSChildName -like 'QuickConvert.*' -or
                $_.PSChildName -like 'QCTest.*' -or
                $_.PSChildName -like 'QCStore.*'
            } |
            ForEach-Object { $_.PSPath }
    }

    $paths | Sort-Object -Unique | Where-Object { Test-Path -LiteralPath $_ }
}

function Remove-QuickConvertLegacyShellEntries {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ClassesRoot,
        [Parameter(Mandatory)][string]$CommandStoreRoot
    )

    Get-QuickConvertLegacyShellEntries @PSBoundParameters | ForEach-Object {
        Remove-Item -LiteralPath $_ -Recurse -Force
    }
}

Export-ModuleMember -Function Get-QuickConvertLegacyShellEntries, Remove-QuickConvertLegacyShellEntries
