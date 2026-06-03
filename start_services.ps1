# start_services.ps1
# Starts the PoolPredict API and frontend dev servers together.
# Press Ctrl-C in this terminal to stop both services.
#
# Usage:
#   .\start_services.ps1
#   .\start_services.ps1 -ApiPort 5000 -WebPort 3000
#   .\start_services.ps1 -WithTestProvider

param(
    [int]$ApiPort = 5000,
    [int]$WebPort = 3000,
    [int]$TestProviderPort = 5090,
    [switch]$WithTestProvider
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Start-ServiceProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileName,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [hashtable]$EnvironmentVariables = @{}
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.Arguments = ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_ -replace '"', '\"') + '"'
        } else {
            $_
        }
    }) -join " "
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    foreach ($key in $EnvironmentVariables.Keys) {
        $startInfo.Environment[$key] = $EnvironmentVariables[$key]
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()
    return $process
}

function Resolve-CommandPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command
    )

    $resolvedCommand = Get-Command $Command -ErrorAction Stop
    if (-not $resolvedCommand.Source) {
        throw "Could not resolve command path for $Command."
    }

    return $resolvedCommand.Source
}

function Get-ChildProcessIds {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId
    )

    $children = Get-CimInstance Win32_Process -Filter "ParentProcessId = $ProcessId" -ErrorAction SilentlyContinue
    foreach ($child in $children) {
        Get-ChildProcessIds -ProcessId $child.ProcessId
        $child.ProcessId
    }
}

function Stop-ProcessTree {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($Process.HasExited) {
        return
    }

    Write-Host "Stopping $Name ..."

    $processIds = @(Get-ChildProcessIds -ProcessId $Process.Id)
    $processIds += $Process.Id
    foreach ($processId in $processIds) {
        try {
            Stop-Process -Id $processId -Force -ErrorAction Stop
        } catch {
            Write-Warning "Failed to stop PID ${processId}: $($_.Exception.Message)"
        }
    }
}

$apiProcess = $null
$webProcess = $null
$testProviderProcess = $null
$dotnetCommand = Resolve-CommandPath "dotnet"
$npmCommand = if ($env:OS -eq "Windows_NT") {
    Resolve-CommandPath "npm.cmd"
} else {
    Resolve-CommandPath "npm"
}

try {
    if ($WithTestProvider) {
        Write-Host "Starting virtual provider on http://localhost:$TestProviderPort ..."
        $testProviderProcess = Start-ServiceProcess `
            -FileName $dotnetCommand `
            -Arguments @("run", "--project", "apps/test-provider/PoolPredict.TestProvider.csproj") `
            -WorkingDirectory $repoRoot `
            -EnvironmentVariables @{
                "ASPNETCORE_ENVIRONMENT" = "Development"
                "ASPNETCORE_URLS" = "http://localhost:$TestProviderPort"
            }
    }

    Write-Host "Starting API on http://localhost:$ApiPort ..."
    $apiProcess = Start-ServiceProcess `
        -FileName $dotnetCommand `
        -Arguments @("run", "--project", "apps/api/PoolPredict.Api.csproj") `
        -WorkingDirectory $repoRoot `
        -EnvironmentVariables @{
            "ASPNETCORE_ENVIRONMENT" = "Development"
            "ASPNETCORE_URLS" = "http://localhost:$ApiPort"
        }

    Write-Host "Starting frontend on http://localhost:$WebPort ..."
    $webProcess = Start-ServiceProcess `
        -FileName $npmCommand `
        -Arguments @("run", "dev", "--", "--hostname", "localhost", "--port", "$WebPort") `
        -WorkingDirectory (Join-Path $repoRoot "apps/web")

    Write-Host ""
    Write-Host "Services are running. Press Ctrl-C to stop both."
    Write-Host ""

    while ($true) {
        Start-Sleep -Seconds 1

        if ($apiProcess.HasExited) {
            throw "API process exited with code $($apiProcess.ExitCode)."
        }

        if ($webProcess.HasExited) {
            throw "Frontend process exited with code $($webProcess.ExitCode)."
        }

        if ($testProviderProcess -and $testProviderProcess.HasExited) {
            throw "Virtual provider process exited with code $($testProviderProcess.ExitCode)."
        }
    }
} finally {
    if ($webProcess) {
        Stop-ProcessTree -Process $webProcess -Name "frontend"
    }

    if ($apiProcess) {
        Stop-ProcessTree -Process $apiProcess -Name "API"
    }

    if ($testProviderProcess) {
        Stop-ProcessTree -Process $testProviderProcess -Name "virtual provider"
    }
}
