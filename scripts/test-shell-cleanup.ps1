$ErrorActionPreference = 'Stop'
$fixture = "HKCU:\Software\QuickConvert.Tests\$([guid]::NewGuid().ToString('N'))"
$classes = Join-Path $fixture 'Classes'
$store = Join-Path $fixture 'CommandStore\shell'

try {
    New-Item "$classes\SystemFileAssociations\.png\shell\QuickConvert\command" -Force | Out-Null
    New-Item "$classes\SystemFileAssociations\.png\shell\KeepMe" -Force | Out-Null
    New-Item "$classes\*\shell\QCTest" -Force | Out-Null
    New-Item "$store\QCTest.A1\command" -Force | Out-Null
    New-Item "$store\QuickConvert.ToJPG\command" -Force | Out-Null
    New-Item "$store\KeepMe" -Force | Out-Null

    Import-Module "$PSScriptRoot\ShellCleanup.psm1" -Force
    Remove-QuickConvertLegacyShellEntries -ClassesRoot $classes -CommandStoreRoot $store

    @(
        "$classes\SystemFileAssociations\.png\shell\QuickConvert",
        "$classes\*\shell\QCTest",
        "$store\QCTest.A1",
        "$store\QuickConvert.ToJPG"
    ) | ForEach-Object {
        if (Test-Path -LiteralPath $_) { throw "Not removed: $_" }
    }

    @(
        "$classes\SystemFileAssociations\.png\shell\KeepMe",
        "$store\KeepMe"
    ) | ForEach-Object {
        if (-not (Test-Path -LiteralPath $_)) { throw "Wrongly removed: $_" }
    }

    Write-Host '[PASS] Exact legacy shell cleanup'
}
finally {
    Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
