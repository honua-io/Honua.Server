#!/usr/bin/env pwsh
param(
    [string] $CapabilitiesUrl,
    [string] $HonuaBaseUrl,
    [string] $EtsVersion = "latest",
    [string] $ReportRoot = "qa-report"
)

function Show-Usage {
    @'
Usage: run-wms-conformance.ps1 [-CapabilitiesUrl <url>] [-HonuaBaseUrl <url>] [-EtsVersion <tag>] [-ReportRoot <path>]

Runs the OGC WMS 1.3 executable test suite (ETS) using the ogccite/ets-wms13
container. Provide a full GetCapabilities URL or allow the script to derive it
from Honua's base URL.

Examples:
  ./scripts/run-wms-conformance.ps1 -HonuaBaseUrl "https://localhost:5001"
  ./scripts/run-wms-conformance.ps1 -CapabilitiesUrl "https://honua.dev/wms?service=WMS&request=GetCapabilities&version=1.3.0"
'@
}

if ($args -contains '-h' -or $args -contains '--help') {
    Show-Usage
    exit 0
}

if (-not $CapabilitiesUrl -and $env:WMS_CAPABILITIES_URL) {
    $CapabilitiesUrl = $env:WMS_CAPABILITIES_URL
}

if (-not $CapabilitiesUrl -and $HonuaBaseUrl) {
    $base = $HonuaBaseUrl.TrimEnd('/')
    $CapabilitiesUrl = "$base/wms?service=WMS&request=GetCapabilities&version=1.3.0"
}

if (-not $CapabilitiesUrl) {
    Write-Error "WMS capabilities URL not provided. Use -CapabilitiesUrl or set WMS_CAPABILITIES_URL or HONUA_BASE_URL."
    exit 1
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Error "Docker is required to run the WMS conformance tests."
    exit 1
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputDir = Join-Path $ReportRoot "wms-$timestamp"
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$port = & {
    Add-Type -AssemblyName System.Net.Sockets
    $random = New-Object System.Random
    for ($i = 0; $i -lt 100; $i++) {
        $candidate = $random.Next(20000, 40000)
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $candidate)
        try {
            $listener.Start()
            $listener.Stop()
            return $candidate
        } catch {
            $listener.Stop()
        }
    }
    throw "Unable to locate an available local port"
}

$container = "wms-ets-$timestamp"
$image = "ogccite/ets-wms13:$EtsVersion"

try {
    Write-Host "Starting $image as $container (port $port)"
    docker run -d --rm --name $container -p "$port:8080" $image | Out-Null
} catch {
    Write-Error "Failed to start ETS container: $_"
    exit 1
}

try {
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        try {
            Invoke-WebRequest -Uri "http://localhost:$port/teamengine/rest/" -Credential (New-Object System.Management.Automation.PSCredential('ogctest', (ConvertTo-SecureString 'ogctest' -AsPlainText -Force))) -UseBasicParsing -TimeoutSec 5 | Out-Null
            $ready = $true
            break
        } catch {
            Start-Sleep -Seconds 2
        }
    }

    if (-not $ready) {
        throw "TEAM Engine did not become ready on port $port."
    }

    $encoded = [System.Uri]::EscapeDataString($CapabilitiesUrl)
    $runUrl = "http://localhost:$port/teamengine/rest/suites/wms13/run?wms=$encoded&format=xml"
    $resultFile = Join-Path $outputDir "wms-conformance-response.xml"

    Write-Host "Executing WMS 1.3 ETS against $CapabilitiesUrl"
    try {
        $response = Invoke-WebRequest -Uri $runUrl -Credential (New-Object System.Management.Automation.PSCredential('ogctest', (ConvertTo-SecureString 'ogctest' -AsPlainText -Force))) -UseBasicParsing -OutFile $resultFile -ErrorAction Stop
    } catch {
        throw "Failed to invoke the ETS run endpoint: $_"
    }

    if ($response.StatusCode -ge 400) {
        throw "WMS conformance suite reported an error (HTTP $($response.StatusCode)). See $resultFile for details."
    }

    if (Select-String -Path $resultFile -Pattern 'FAIL' -SimpleMatch) {
        throw "WMS conformance suite reported failures. See $resultFile for details."
    }

    Write-Host "Conformance results stored in $outputDir"
}
finally {
    docker rm -f $container | Out-Null 2>$null
}
