param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$OutputPath,
    [int]$WarmupSeconds = 20,
    [int]$SampleSeconds = 20
)
$ErrorActionPreference = 'Stop'
if ($WarmupSeconds -lt 5 -or $SampleSeconds -lt 10) { throw 'Use at least 5 seconds warmup and 10 seconds sampling.' }
$path = (Resolve-Path -LiteralPath $Executable).Path
$process = Start-Process -FilePath $path -ArgumentList '--test-calendar' -PassThru -WindowStyle Normal
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw 'The test app exited before opening its calendar.' }
        if ($process.MainWindowTitle -like '*startup failed*') { throw 'The app displayed a startup error. This is not an idle-calendar measurement.' }
    } until ($process.MainWindowTitle -eq 'Daymark — Test calendar' -or [DateTime]::UtcNow -gt $deadline)
    if ($process.MainWindowTitle -ne 'Daymark — Test calendar') { throw 'The test-calendar window was not observed. Refusing to benchmark a dialog or failed launch.' }
    Start-Sleep -Seconds $WarmupSeconds
    $samples = @()
    foreach ($index in 1..2) {
        $process.Refresh(); $cpu = $process.TotalProcessorTime.TotalSeconds; $watch = [Diagnostics.Stopwatch]::StartNew()
        Start-Sleep -Seconds $SampleSeconds
        $process.Refresh()
        if ($process.HasExited -or $process.MainWindowTitle -ne 'Daymark — Test calendar') { throw 'Calendar exited or changed to an error dialog during measurement.' }
        $elapsed = $watch.Elapsed.TotalSeconds; $used = $process.TotalProcessorTime.TotalSeconds - $cpu
        $samples += @{ sample = $index; seconds = $elapsed; cpuSeconds = $used; oneCoreCpuPercent = 100 * $used / $elapsed;
            workingSetMiB = $process.WorkingSet64 / 1MB; privateBytesMiB = $process.PrivateMemorySize64 / 1MB }
    }
    @{ version = $process.MainModule.FileVersionInfo.ProductVersion; warmupSeconds = $WarmupSeconds; samples = $samples;
        note = 'No UI automation. Empty isolated QA calendar unless test data already exists. CPU percentages refer to one core; not a battery benchmark.' } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
    Get-Content -LiteralPath $OutputPath
} finally {
    # Only the process this script created; never close a separately running personal calendar.
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
