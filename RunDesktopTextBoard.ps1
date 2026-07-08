$ErrorActionPreference = 'Stop'

$app = Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\win-x64\publish\AkashaNotes.exe'
if (-not (Test-Path -LiteralPath $app)) {
    $app = Join-Path $PSScriptRoot 'bin\Debug\net8.0-windows\AkashaNotes.exe'
}

if (-not (Test-Path -LiteralPath $app)) {
    throw 'AkashaNotes.exe was not found. Build or publish the project first.'
}

Start-Process -FilePath $app
