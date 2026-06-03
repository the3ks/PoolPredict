# kill_ports.ps1
# This script finds and terminates processes listening on specified ports (default 5000 for the API and 3000 for the frontend).
# Usage: .\kill_ports.ps1   # uses default ports 5000 and 3000
#        .\kill_ports.ps1 -Ports 5000,3000   # custom ports

param(
    [int[]]$Ports = @(5000, 3000)
)

foreach ($port in $Ports) {
    Write-Host "Checking port $port ..."
    # Get listening TCP connections on the port
    $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    foreach ($conn in $connections) {
        $processId = $conn.OwningProcess
        if ($processId) {
            try {
                $proc = Get-Process -Id $processId -ErrorAction Stop
                Write-Host "Killing process $($proc.Name) (PID $processId) listening on port $port"
                Stop-Process -Id $processId -Force
            } catch {
                Write-Warning "Failed to kill PID ${processId}: $($_)"
            }
        }
    }
}
