param(
    [string]$OutputDirectory = "samples/kml",
    [string]$KmlUrl = "https://developers.google.com/kml/documentation/KML_Samples.kml",
    [string]$FileName = "ogc-kml-conformance-sample.kml"
)

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$destination = Join-Path $OutputDirectory $FileName

try {
    Invoke-WebRequest -Uri $KmlUrl -OutFile $destination -UseBasicParsing
    Write-Host "Downloaded KML sample to $destination"
} catch {
    Remove-Item -ErrorAction SilentlyContinue $destination
    Write-Error "Failed to download KML sample from $KmlUrl. $_"
    exit 1
}
