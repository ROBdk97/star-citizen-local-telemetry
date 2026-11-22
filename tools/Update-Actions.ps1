#Requires -Version 7
<#
.SYNOPSIS
    Regenerates src\StarCitizen.LocalTelemetry\GameActions.cs from the running game.

.DESCRIPTION
    Runs tools\GameActionsGenerator, which only calls ListTriggerableActions (it never
    triggers an action), prints added/removed/changed actions and rewrites GameActions.cs.
    The game version written in the file header is read from the library's proto file, so
    run Update-Proto.ps1 first after a patch. The game must be running with telemetry enabled.

.EXAMPLE
    .\tools\Update-Actions.ps1

.EXAMPLE
    .\tools\Update-Actions.ps1 -Check
#>
[CmdletBinding()]
param(
    [string] $Address = 'http://127.0.0.1:2942',
    [switch] $Check
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$generator = Join-Path $PSScriptRoot 'GameActionsGenerator\GameActionsGenerator.csproj'
$output = Join-Path $repoRoot 'src\StarCitizen.LocalTelemetry\GameActions.cs'
$proto = Join-Path $repoRoot 'src\StarCitizen.LocalTelemetry\Protos\sc_telemetry_types.proto'

$arguments = @('--output', $output, '--proto', $proto, '--address', $Address)
if ($Check) { $arguments += '--check' }

dotnet run -c Release --project $generator -- @arguments
$exitCode = $LASTEXITCODE
if ($exitCode -eq 0 -and -not $Check) {
    # GameActions constants are public API: record added (RS0016) and removed (RS0017) ones.
    $library = Join-Path $repoRoot 'src\StarCitizen.LocalTelemetry\StarCitizen.LocalTelemetry.csproj'
    dotnet format analyzers $library --diagnostics RS0016 RS0017 --severity info
    $exitCode = $LASTEXITCODE
    git -C $repoRoot --no-pager diff --stat -- $output (Join-Path $repoRoot 'src\StarCitizen.LocalTelemetry\PublicAPI.Unshipped.txt')
}
exit $exitCode
