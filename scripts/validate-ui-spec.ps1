# UISpec / Binding pipeline (batchmode)
# Usage (adjust Unity/Tuanjie exe path):
#   .\scripts\validate-ui-spec.ps1 -EditorPath "C:\Path\To\Tuanjie.exe"

param(
    [Parameter(Mandatory = $true)]
    [string]$EditorPath,
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$LogFile = (Join-Path $env:TEMP "AttackSkill-UISpec.log")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $EditorPath)) {
    Write-Error "Editor not found: $EditorPath"
    exit 2
}

$argsList = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $ProjectPath,
    "-executeMethod", "AttackSkill.Editor.UISpecChecker.UISpecPipeline.ValidateAllBatch",
    "-logFile", $LogFile
)

Write-Host "Running UISpec pipeline..."
Write-Host "  Editor : $EditorPath"
Write-Host "  Project: $ProjectPath"
Write-Host "  Log    : $LogFile"

$p = Start-Process -FilePath $EditorPath -ArgumentList $argsList -Wait -PassThru
if ($p.ExitCode -ne 0) {
    Write-Error "UISpec pipeline FAILED (exit $($p.ExitCode)). See $LogFile"
    exit $p.ExitCode
}

Write-Host "UISpec pipeline OK."
exit 0
