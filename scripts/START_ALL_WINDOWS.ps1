# Starts three independent .NET 8 services in separate PowerShell windows.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'Cannot find dotnet. Install .NET 8 SDK and restart PowerShell.'
    exit 1
}
$services = @(
    @{ Name = 'PRODUCT API'; Project = 'Demo.ProductService'; Port = 5181 },
    @{ Name = 'ORDER API'; Project = 'Demo.OrderService'; Port = 5182 },
    @{ Name = 'REPORTING API'; Project = 'ReportingService'; Port = 5183 }
)
foreach ($service in $services) {
    $folder = Join-Path $root "src\$($service.Project)"
    $project = Join-Path $folder "$($service.Project).csproj"
    $command = "dotnet run --project `"$project`" --no-launch-profile"
    Start-Process -FilePath 'powershell.exe' -WorkingDirectory $folder -ArgumentList @(
        '-NoExit', '-Command', $command
    ) | Out-Null
    Write-Host "Started $($service.Name): http://localhost:$($service.Port)/swagger"
}
Write-Host 'Wait until all 3 windows show Now listening. Then run scripts\TEST_API_WINDOWS.ps1.'
