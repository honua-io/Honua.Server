param(
    [Parameter(Mandatory = $true)]
    [string]$HonuaBaseUrl,
    [string]$EtsVersion = "1.0.0",
    [string]$ReportRoot = "qa-report",
    [string]$TestSelection = "confAll",
    [string]$EtsLogLevel
)

$dockerImage = "ghcr.io/opengeospatial/ets-ogcapi-features10:$EtsVersion"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDir = Join-Path $ReportRoot "ogcfeatures-$timestamp"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$fullOutputDir = (Resolve-Path $outputDir).Path

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Error "Docker is required to run the OGC conformance tests."
    exit 1
}

Write-Host "Pulling $dockerImage..."
docker pull $dockerImage | Out-Null

$env:ETS_SERVICE_ENDPOINT = "$HonuaBaseUrl/ogc"
$env:ETS_TEST_SELECTION = $TestSelection
if ($EtsLogLevel) {
    $env:ETS_LOG_LEVEL = $EtsLogLevel
}

$volume = "{0}:/tmp/ets-results" -f $fullOutputDir
Write-Host "Running OGC API Features ETS against $HonuaBaseUrl/ogc"
docker run --rm `
  -v $volume `
  $dockerImage `
  -o /tmp/ets-results/testng-results.xml | Out-Null

Write-Host "Conformance results stored in $outputDir"
