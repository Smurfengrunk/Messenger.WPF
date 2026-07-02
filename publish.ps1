param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$OutputPath = (Join-Path $PSScriptRoot "publish")
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "Messenger\Messenger.csproj"

if (-not (Test-Path -LiteralPath $project)) {
    throw "Project not found: $project"
}

if (Test-Path -LiteralPath $OutputPath) {
    Remove-Item -LiteralPath $OutputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $OutputPath | Out-Null

dotnet publish $project `
    -c $Configuration `
    -p:Platform=x64 `
    -o $OutputPath `
    --sc

Write-Host "Published $Configuration build of project $project to $OutputPath"
