param(
    [string]$CapabilitiesUrl,
    [string]$HonuaBaseUrl,
    [string]$EtsVersion = "latest",
    [string]$ReportRoot = "qa-report"
)

function Show-Usage {
    @"
Usage: run-wfs-conformance.ps1 [-CapabilitiesUrl <url>] [-HonuaBaseUrl <url>] [-EtsVersion <tag>] [-ReportRoot <path>]

Runs the OGC WFS 2.0 executable test suite (ETS) via the ogccite/ets-wfs20 Docker image.

Arguments:
  -CapabilitiesUrl  Fully-qualified WFS GetCapabilities URL.
  -HonuaBaseUrl     Base Honua URL; used to construct the capabilities URL when
                    -CapabilitiesUrl is not supplied.
  -EtsVersion       Docker tag for ogccite/ets-wfs20 (default: latest).
  -ReportRoot       Directory to store ETS output (default: qa-report).

Examples:
  ./scripts/run-wfs-conformance.ps1 -CapabilitiesUrl "https://localhost:5001/wfs?service=WFS&request=GetCapabilities&version=2.0.0"
  ./scripts/run-wfs-conformance.ps1 -HonuaBaseUrl "https://localhost:5001"
"@
}

if ($PSBoundParameters.ContainsKey('Help') -or $args -contains '-h' -or $args -contains '--help') {
    Show-Usage
    exit 0
}

if (-not $CapabilitiesUrl -and $env:WFS_CAPABILITIES_URL) {
    $CapabilitiesUrl = $env:WFS_CAPABILITIES_URL
}

if (-not $CapabilitiesUrl -and $HonuaBaseUrl) {
    $CapabilitiesUrl = "$($HonuaBaseUrl.TrimEnd('/'))/wfs?service=WFS&request=GetCapabilities&version=2.0.0"
}

if (-not $CapabilitiesUrl -and $env:HONUA_BASE_URL) {
    $CapabilitiesUrl = "$($env:HONUA_BASE_URL.TrimEnd('/'))/wfs?service=WFS&request=GetCapabilities&version=2.0.0"
}

if (-not $CapabilitiesUrl) {
    Write-Error "Capabilities URL not provided. Set -CapabilitiesUrl, WFS_CAPABILITIES_URL, or HONUA_BASE_URL."
    exit 1
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Error "Docker is required to run the WFS conformance tests."
    exit 1
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputDir = Join-Path -Path $ReportRoot -ChildPath "wfs-$timestamp"
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# Reserve a free TCP port for the temporary container
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ($listener.LocalEndpoint).Port
$listener.Stop()

$containerName = "wfs-ets-$timestamp"
$dockerImage = "ogccite/ets-wfs20:$EtsVersion"

function Cleanup {
    docker rm -f $containerName *> $null 2>&1 | Out-Null
}

Register-EngineEvent PowerShell.Exiting -Action { Cleanup } | Out-Null

Write-Host "Starting $dockerImage as $containerName (port $port)"
docker run -d --rm --name $containerName -p "${port}:8080" $dockerImage | Out-Null

$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    try {
        Invoke-WebRequest -Uri "http://localhost:$port/teamengine/rest/" -Headers @{ Authorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('ogctest:ogctest')) } -UseBasicParsing -TimeoutSec 5 | Out-Null
        $ready = $true
        break
    } catch {
        Start-Sleep -Seconds 2
    }
}

if (-not $ready) {
    Write-Error "TEAM Engine did not become ready on port $port."
    Cleanup
    exit 1
}

$encoded = [System.Uri]::EscapeDataString($CapabilitiesUrl)
$runUrl = "http://localhost:$port/teamengine/rest/suites/wfs20/run?wfs=$encoded&format=xml"
$resultFile = Join-Path $outputDir 'wfs-conformance-response.xml'

Write-Host "Executing WFS 2.0 ETS against $CapabilitiesUrl"

$basicAuth = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('ogctest:ogctest'))

try {
    $response = Invoke-WebRequest -Uri $runUrl -Headers @{ Authorization = $basicAuth } -OutFile $resultFile -UseBasicParsing -TimeoutSec 600
    $statusCode = $response.StatusCode.value__
} catch [System.Net.WebException] {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $content = $reader.ReadToEnd()
    $reader.Dispose()
    Set-Content -LiteralPath $resultFile -Value $content
}

Cleanup

if (-not $statusCode) {
    Write-Error "Failed to invoke the ETS run endpoint."
    exit 1
}

if ($statusCode -ge 400) {
    Write-Error "WFS conformance suite reported an error (HTTP $statusCode). See $resultFile for details."
    exit 1
}

$content = Get-Content -LiteralPath $resultFile -Raw
if ($content -match 'FAIL' -or $content -match 'Failed') {
    Write-Error "WFS conformance suite reported failures. See $resultFile for details."
    exit 1
}

Write-Host "Conformance results stored in $outputDir"
