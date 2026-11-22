#Requires -Version 7
<#
.SYNOPSIS
    Updates the library's Star Citizen telemetry protobuf schema.

.DESCRIPTION
    Replaces src\StarCitizen.LocalTelemetry\Protos\sc_telemetry_types.proto with a schema
    extracted from StarCitizen.exe by sc_make_proto (https://github.com/Purzifal/sc_make_proto),
    validates it, shows the diff and rebuilds the library so incompatible changes surface as
    compile errors in TelemetryMapper.cs.

    Without parameters the script downloads sc_make_proto (pinned to -MakeProtoRef) into
    -CachePath (default: .tools\sc_make_proto in the repository, git-ignored), creates a Python
    virtual environment there with the pinned protobuf package, and runs make_proto.py. This
    needs Python 3 on PATH and the game running (make_proto.py waits for StarCitizen.exe).

    Alternatively pass an already extracted file with -Source, or your own sc_make_proto
    checkout with -MakeProtoPath.

.EXAMPLE
    .\tools\Update-Proto.ps1

.EXAMPLE
    .\tools\Update-Proto.ps1 -Source D:\SC-Telemetry\sc_telemetry_5.0.202.10025.proto

.EXAMPLE
    .\tools\Update-Proto.ps1 -MakeProtoPath D:\SC-Telemetry\sc_make_proto -Python D:\SC-Telemetry\.venv\Scripts\python.exe
#>
[CmdletBinding(DefaultParameterSetName = 'Download', SupportsShouldProcess)]
param(
    [Parameter(ParameterSetName = 'Download')]
    [string] $CachePath,

    [Parameter(ParameterSetName = 'Download')]
    [string] $MakeProtoRef = '90a491d362c2c76fef086080e00b1f0ff0b79917',

    [Parameter(ParameterSetName = 'Download')]
    [string] $ProtobufVersion = '7.36.2',

    [Parameter(Mandatory, ParameterSetName = 'Source')]
    [string] $Source,

    [Parameter(Mandatory, ParameterSetName = 'MakeProto')]
    [string] $MakeProtoPath,

    [Parameter(ParameterSetName = 'MakeProto')]
    [Parameter(ParameterSetName = 'Download')]
    [string] $Python = 'python',

    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$target = Join-Path $repoRoot 'src\StarCitizen.LocalTelemetry\Protos\sc_telemetry_types.proto'
$project = Join-Path $repoRoot 'src\StarCitizen.LocalTelemetry\StarCitizen.LocalTelemetry.csproj'

function Get-SchemaVersion([string] $Text, [string] $FileName) {
    if ($Text -match 'file version\s+([\d.]+\d)') { return $Matches[1] }
    if ($FileName -match '_(\d+(?:\.\d+)+)\.proto$') { return $Matches[1] }
    return 'unknown'
}

if ($PSCmdlet.ParameterSetName -eq 'Download') {
    if (-not $CachePath) { $CachePath = Join-Path $repoRoot '.tools\sc_make_proto' }
    $CachePath = [IO.Path]::GetFullPath($CachePath)
    $refFile = Join-Path $CachePath '.ref'
    $venvPython = Join-Path $CachePath '.venv\Scripts\python.exe'
    $ready = (Test-Path $venvPython) -and (Test-Path $refFile) -and
        ((Get-Content $refFile -Raw).Trim() -eq "$MakeProtoRef protobuf==$ProtobufVersion")

    if (-not $ready) {
        if (-not $PSCmdlet.ShouldProcess($CachePath, "Download sc_make_proto $MakeProtoRef and install protobuf==$ProtobufVersion")) { return }
        if (-not (Get-Command $Python -ErrorAction SilentlyContinue)) { throw "Python '$Python' was not found. Install Python 3 or pass -Python <path to python.exe>." }

        $zip = Join-Path ([IO.Path]::GetTempPath()) "sc_make_proto-$MakeProtoRef.zip"
        $extract = Join-Path ([IO.Path]::GetTempPath()) "sc_make_proto-$MakeProtoRef"
        try {
            Write-Host "Downloading sc_make_proto $MakeProtoRef..."
            Invoke-WebRequest "https://github.com/Purzifal/sc_make_proto/archive/$MakeProtoRef.zip" -OutFile $zip
            Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue
            Expand-Archive $zip $extract
            $content = Get-ChildItem $extract -Directory | Select-Object -First 1
            if (-not $content -or -not (Test-Path (Join-Path $content.FullName 'make_proto.py'))) { throw 'The downloaded archive does not contain make_proto.py.' }

            Remove-Item $CachePath -Recurse -Force -ErrorAction SilentlyContinue
            New-Item (Split-Path $CachePath -Parent) -ItemType Directory -Force | Out-Null
            Move-Item $content.FullName $CachePath
        }
        finally {
            Remove-Item $zip, $extract -Recurse -Force -ErrorAction SilentlyContinue
        }

        Write-Host 'Creating the Python environment...'
        & $Python -m venv (Join-Path $CachePath '.venv')
        if ($LASTEXITCODE -ne 0) { throw "Creating the virtual environment failed with exit code $LASTEXITCODE." }
        & $venvPython -m pip install --disable-pip-version-check --quiet "protobuf==$ProtobufVersion"
        if ($LASTEXITCODE -ne 0) { throw "Installing protobuf failed with exit code $LASTEXITCODE." }
        Set-Content $refFile "$MakeProtoRef protobuf==$ProtobufVersion"
    }

    $MakeProtoPath = $CachePath
    $Python = $venvPython
}

if ($PSCmdlet.ParameterSetName -in 'MakeProto', 'Download') {
    if (-not (Get-Process StarCitizen -ErrorAction SilentlyContinue)) { Write-Host 'Waiting for StarCitizen.exe to start (make_proto.py reads the running game)...' }
    $script = Join-Path $MakeProtoPath 'make_proto.py'
    if (-not (Test-Path $script)) { throw "make_proto.py not found in '$MakeProtoPath'." }
    $started = Get-Date
    Push-Location $MakeProtoPath
    try {
        & $Python $script
        if ($LASTEXITCODE -ne 0) { throw "make_proto.py failed with exit code $LASTEXITCODE (is StarCitizen.exe running?)." }
    }
    finally { Pop-Location }
    $Source = Get-ChildItem $MakeProtoPath -Filter 'sc_telemetry_*.proto' -File |
        Where-Object LastWriteTime -ge $started.AddSeconds(-5) |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $Source) { throw "make_proto.py did not write a new sc_telemetry_*.proto in '$MakeProtoPath'." }
}

$Source = (Resolve-Path $Source).Path
$text = Get-Content $Source -Raw
foreach ($required in 'syntax\s*=\s*"proto3"', 'package\s+sc\.game\.telemetry\.v1\s*;', 'service\s+GameTelemetryService\b',
                      'rpc\s+TelemetryStream\b', 'rpc\s+TriggerGameAction\b', 'rpc\s+ListTriggerableActions\b') {
    if ($text -notmatch $required) { throw "'$Source' does not look like the telemetry schema (missing /$required/)." }
}

$oldText = Get-Content $target -Raw
$oldVersion = Get-SchemaVersion $oldText ''
$newVersion = Get-SchemaVersion $text (Split-Path $Source -Leaf)
Write-Host "Current schema: $oldVersion"
Write-Host "New schema:     $newVersion ($Source)"

if ($oldText -eq $text) {
    Write-Host 'The schema is unchanged.'
    return
}
elseif ($PSCmdlet.ShouldProcess($target, 'Replace telemetry schema')) {
    Set-Content $target $text -NoNewline
    $schemaFile = Join-Path $repoRoot 'src\StarCitizen.LocalTelemetry\TelemetrySchema.cs'
    if ($newVersion -match '^\d+\.\d+\.\d+\.\d+$') {
        $schemaText = Get-Content $schemaFile -Raw
        $schemaText = $schemaText -replace 'BundledGameVersion \{ get; \} = new\([\d, ]+\);', "BundledGameVersion { get; } = new($($newVersion -replace '\.', ', '));"
        Set-Content $schemaFile $schemaText -NoNewline
    }
    git -C $repoRoot --no-pager diff --stat -- $target $schemaFile
}

if (-not $SkipBuild -and -not $WhatIfPreference) {
    dotnet build $project
    if ($LASTEXITCODE -ne 0) {
        throw 'Build failed. Update TelemetryModels.cs/TelemetryMapper.cs for the schema changes shown above.'
    }
}

Write-Host @"

Next steps:
  - Review the diff: git diff -- src/StarCitizen.LocalTelemetry/Protos
  - Map new or changed fields in TelemetryModels.cs and TelemetryMapper.cs.
  - Update the version '$oldVersion' -> '$newVersion' in README.md and GameActions.cs (TelemetrySchema.cs is updated automatically).
  - New public API needs entries in PublicAPI.Unshipped.txt (see CONTRIBUTING.md).
  - Add a CHANGELOG.md entry.
  - Run: dotnet test --solution .\StarCitizen.LocalTelemetry.slnx -c Release
"@
