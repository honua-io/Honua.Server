param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$InputPath,
    [string]$EtsVersion = "latest",
    [string]$ReportRoot = "qa-report"
)

function Get-FreePort {
    for ($i = 0; $i -lt 20; $i++) {
        $port = Get-Random -Minimum 20000 -Maximum 40000
        $inUse = Test-NetConnection -ComputerName 127.0.0.1 -Port $port -InformationLevel Quiet
        if (-not $inUse) { return $port }
    }
    throw "Unable to find a free TCP port."
}

if (-not (Test-Path $InputPath)) {
    throw "File not found: $InputPath"
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required to run the KML conformance tests."
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDir = Join-Path $ReportRoot "kml-$timestamp"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$fullOutputDir = (Resolve-Path $outputDir).Path

$fullInput = (Resolve-Path $InputPath).Path
$mountDir = (Resolve-Path (Split-Path $fullInput -Parent)).Path
$inputFile = Split-Path $fullInput -Leaf

$containerName = "kml-ets-" + $timestamp
$dockerImage = "ogccite/ets-kml22:$EtsVersion"
$volumeArg = "{0}:/data:ro" -f $mountDir
$port = Get-FreePort
Write-Host "Starting $dockerImage as $containerName (port $port)"
docker run -d --rm --name $containerName -p ${port}:8080 -v $volumeArg $dockerImage | Out-Null

try {
    $ready = $false
    for ($i = 0; $i -lt 30 -and -not $ready; $i++) {
        Start-Sleep -Seconds 2
        try {
            Invoke-WebRequest -Uri "http://localhost:$port/teamengine/" -UseBasicParsing -TimeoutSec 5 | Out-Null
            $ready = $true
        } catch {
            $ready = $false
        }
    }
    if (-not $ready) {
        throw "TEAM Engine did not become ready on port $port."
    }

    $body = "<testRunRequest xmlns='http://teamengine.sourceforge.net/ctl'><entry><string>iut</string><string>file:/data/$inputFile</string></entry></testRunRequest>"
    $credentials = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes('ogctest:ogctest'))
    Write-Host "Submitting test run request..."
    $response = Invoke-WebRequest -Uri "http://localhost:$port/teamengine/rest/suites/kml22/run" -Method Post -Headers @{ Authorization = "Basic $credentials" } -ContentType 'application/xml' -Body $body -TimeoutSec 120 -UseBasicParsing
    $resultPath = Join-Path $fullOutputDir 'earl-results.rdf'
    $response.Content | Out-File $resultPath -Encoding UTF8

    if ($response.Content -match 'earl#failed') {
        throw "KML conformance suite reported failures. See $resultPath for details."
    }

    Write-Host "Conformance results stored in $outputDir"
}
finally {
    docker stop $containerName | Out-Null
}
