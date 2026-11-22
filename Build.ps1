param([switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler was not found.' }
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sources = @('Policy.cs', 'ConfigSync.cs', 'AudioInterop.cs', 'Program.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$exe = Join-Path $output 'SystemSoundsKeeper.exe'
& $compiler /nologo /warnaserror+ /optimize+ /platform:x64 /target:exe /reference:System.Web.Extensions.dll "/out:$exe" $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Test) {
    $testExe = Join-Path $output 'SystemSoundsKeeper.Tests.exe'
    & $compiler /nologo /warnaserror+ /platform:x64 /target:exe /reference:System.Web.Extensions.dll /main:SystemSoundsKeeper.PolicyTests "/out:$testExe" $sources (Join-Path $PSScriptRoot 'tests\PolicyTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Policy tests failed.' }
}
Write-Output "Built: $exe"
