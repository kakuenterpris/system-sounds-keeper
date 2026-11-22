param(
    [ValidateSet('Start', 'Stop', 'Status', 'Devices', 'SyncDevices', 'Apply', 'Check', 'InstallStartup', 'RemoveStartup')]
    [string]$Action = 'Status'
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'bin\SystemSoundsKeeper.exe'
$config = Join-Path $PSScriptRoot 'config.json'
if (-not (Test-Path -LiteralPath $exe)) { & (Join-Path $PSScriptRoot 'Build.ps1') }
$startupFolder = [Environment]::GetFolderPath('Startup')
$startupPath = Join-Path $startupFolder 'SystemSoundsKeeper.lnk'
$launcher = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$launcherArguments = '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + (Join-Path $PSScriptRoot 'Control.ps1') + '" -Action Start'

function Get-OwnedStartupLink {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($startupPath)
    if ((Test-Path -LiteralPath $startupPath) -and ($link.TargetPath -ne $launcher -or $link.Arguments -ne $launcherArguments)) {
        throw 'A startup entry owned by another installation already exists; it was not changed.'
    }
    return $link
}

function Initialize-Config {
    if (Test-Path -LiteralPath $config) { return }
    $json = & $exe list
    if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate audio devices.' }
    $devices = @($json | ConvertFrom-Json)
    $rules = @($devices | Where-Object { ($_.state -band 1) -ne 0 } | ForEach-Object {
        [ordered]@{ id = $_.id; name = $_.name; volumePercent = 50 }
    })
    $initial = [ordered]@{ defaultVolumePercent = 50; devices = $rules }
    [IO.File]::WriteAllText($config, ($initial | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
}

switch ($Action) {
    'Start' {
        Initialize-Config
        & $exe check
        if ($LASTEXITCODE -ne 0) { throw 'Fix config.json before starting.' }
        & $exe status
        if ($LASTEXITCODE -eq 0) { break }
        Start-Process -FilePath $exe -ArgumentList 'watch' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden
        $started = $false
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            Start-Sleep -Milliseconds 150
            & $exe status | Out-Null
            if ($LASTEXITCODE -eq 0) { $started = $true; break }
        }
        if (-not $started) { throw 'Watcher did not start. Check logs\watcher.log.' }
        Write-Output 'SystemSoundsKeeper started in the background.'
    }
    'Stop' { & $exe stop }
    'Status' {
        & $exe status
        $log = Join-Path $PSScriptRoot 'logs\watcher.log'
        if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 10 -Encoding UTF8 }
    }
    'Devices' {
        $json = & $exe list
        if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate audio devices.' }
        $json | ConvertFrom-Json | Select-Object name, stateName, @{Name='Default';Expression={$_.defaultRoles -join ','}}, id | Format-Table -AutoSize -Wrap
        Write-Output "Full inventory: $(Join-Path $PSScriptRoot 'devices.json')"
    }
    'Apply' { Initialize-Config; & $exe apply; if ($LASTEXITCODE -ne 0) { throw 'Some audio sessions could not be updated.' } }
    'SyncDevices' { Initialize-Config; & $exe sync; if ($LASTEXITCODE -ne 0) { throw 'Device config sync failed; existing configuration was retained.' } }
    'Check' { Initialize-Config; & $exe check; if ($LASTEXITCODE -ne 0) { throw 'Invalid configuration.' } }
    'InstallStartup' {
        Initialize-Config
        & $exe check
        if ($LASTEXITCODE -ne 0) { throw 'Fix config.json before installing startup.' }
        $link = Get-OwnedStartupLink
        $link.TargetPath = $launcher
        $link.Arguments = $launcherArguments
        $link.WorkingDirectory = $PSScriptRoot
        $link.WindowStyle = 7
        $link.Description = 'Apply configured System Sounds volumes after audio device changes.'
        $link.Save()
        Write-Output "Installed current-user logon startup: $startupPath"
    }
    'RemoveStartup' {
        if (Test-Path -LiteralPath $startupPath) {
            Get-OwnedStartupLink | Out-Null
            Remove-Item -LiteralPath $startupPath
        }
        Write-Output 'Current-user startup entry removed. Use Stop to stop this session.'
    }
}
