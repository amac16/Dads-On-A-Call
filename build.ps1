param([switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x is required.' }
if (-not (Test-Path -LiteralPath $PSScriptRoot)) { throw 'Project directory not found.' }
$output = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sources = @('AppSettings.cs', 'IndicatorContent.cs', 'IndicatorForm.cs', 'AlertSettingsPanel.cs', 'PositionPicker.cs', 'SettingsForm.cs', 'StartupRegistration.cs', 'UpdateService.cs', 'Program.cs') | ForEach-Object {
    Join-Path $PSScriptRoot "src\$_"
}
$references = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xml.dll')
$manifest = Join-Path $PSScriptRoot 'src\app.manifest'
$executable = Join-Path $output 'DadsOnACall.exe'
& $compiler /nologo /target:winexe /optimize+ /warn:4 "/win32manifest:$manifest" "/out:$executable" $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Write-Output "Built $executable"
if ($Test) {
    $testExecutable = Join-Path $output 'DadsOnACall.Tests.exe'
    $testSource = Join-Path $PSScriptRoot 'tests\Tests.cs'
    & $compiler /nologo /target:exe /optimize+ /warn:4 /main:DadsOnCall.Tests "/win32manifest:$manifest" "/out:$testExecutable" $references $sources $testSource
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
