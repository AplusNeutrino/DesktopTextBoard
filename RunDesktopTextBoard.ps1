$ErrorActionPreference = 'Stop'

$app = Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\win-x64\publish\DesktopTextBoard.exe'
if (-not (Test-Path -LiteralPath $app)) {
    $app = Join-Path $PSScriptRoot 'bin\Debug\net8.0-windows\DesktopTextBoard.exe'
}

if (-not (Test-Path -LiteralPath $app)) {
    throw 'DesktopTextBoard.exe was not found. Build or publish the project first.'
}

Start-Process -FilePath $app
