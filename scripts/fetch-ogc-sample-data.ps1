[CmdletBinding()]
param(
    [string]$Database = "data/sqlite/ogc-sample.db",
    [string]$Metadata = "samples/ogc/metadata.json",
    [string]$ServerUrl = "http://localhost:5000",
    [string]$SourceUrl,
    [switch]$SkipApply
)

$root = Split-Path -Path $PSScriptRoot -Parent

function Resolve-PathWithRoot {
    param([string]$PathValue)
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return $PathValue }
    if ([System.IO.Path]::IsPathRooted($PathValue)) { return [System.IO.Path]::GetFullPath($PathValue) }
    else { return [System.IO.Path]::GetFullPath((Join-Path -Path $root -ChildPath $PathValue)) }
}

$databasePath = Resolve-PathWithRoot -PathValue $Database
$metadataTemplate = Resolve-PathWithRoot -PathValue $Metadata
$samplesDb = Join-Path -Path $root -ChildPath 'samples/ogc/ogc-sample.db'

$targetDirectory = Split-Path -Path $databasePath -Parent
if (-not (Test-Path -LiteralPath $targetDirectory)) {
    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
}

function Prepare-FromArchive {
    param([string]$Url, [string]$Destination)
    $tempDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("honua-ogc-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    try {
        $archivePath = Join-Path -Path $tempDir -ChildPath 'dataset'
        Write-Host "Downloading dataset from $Url"
        Invoke-WebRequest -Uri $Url -OutFile $archivePath -UseBasicParsing

        $extractDir = Join-Path -Path $tempDir -ChildPath 'extracted'
        New-Item -ItemType Directory -Path $extractDir -Force | Out-Null

        if ($archivePath -match '\\.(zip)$') {
            Expand-Archive -LiteralPath $archivePath -DestinationPath $extractDir -Force
        } elseif ($archivePath -match '\\.(tar.gz|tgz|tar.bz2|tbz|tbz2)$') {
            & tar -xf $archivePath -C $extractDir
        } else {
            throw "Unsupported archive format: $archivePath"
        }

        $sourceFile = Get-ChildItem -LiteralPath $extractDir -Recurse -File -Include *.gpkg, *.geojson, *.json, *.shp | Select-Object -First 1
        if (-not $sourceFile) {
            throw "Unable to locate a geospatial dataset inside archive."
        }

        $ogr = Get-Command ogr2ogr -ErrorAction SilentlyContinue
        if (-not $ogr) {
            throw "ogr2ogr is required to transform the dataset but was not found on PATH."
        }

        Write-Host "Transforming dataset using ogr2ogr from $($sourceFile.FullName)"
        & $ogr.Source -overwrite -f SQLite $Destination $sourceFile.FullName
    }
    finally {
        if (Test-Path -LiteralPath $tempDir) {
            Remove-Item -LiteralPath $tempDir -Recurse -Force
        }
    }
}

if ($PSBoundParameters.ContainsKey('SourceUrl') -and -not [string]::IsNullOrWhiteSpace($SourceUrl)) {
    Prepare-FromArchive -Url $SourceUrl -Destination $databasePath
} else {
    Copy-Item -LiteralPath $samplesDb -Destination $databasePath -Force
    Write-Host "Copied bundled sample dataset to $databasePath"
}

$connectionString = "Data Source=$databasePath;Version=3;Pooling=false;"
$generatedMetadata = Join-Path -Path $targetDirectory -ChildPath 'ogc-sample-metadata.json'

Write-Host "Generating metadata at $generatedMetadata"

$json = Get-Content -LiteralPath $metadataTemplate -Raw | ConvertFrom-Json
foreach ($ds in $json.dataSources) {
    $ds.connectionString = $connectionString
}
$json | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $generatedMetadata -Encoding UTF8

if (-not $SkipApply) {
    try {
        $uri = "{0}/admin/metadata/apply" -f $ServerUrl.TrimEnd('/')
        $body = Get-Content -LiteralPath $generatedMetadata -Raw
        Write-Host "POST $uri"
        $response = Invoke-WebRequest -Uri $uri -Method Post -ContentType 'application/json' -Body $body -UseBasicParsing
        Write-Host "Metadata apply status: $($response.StatusCode)"
    }
    catch {
        Write-Warning "Metadata apply failed: $($_.Exception.Message)"
    }
}

Write-Host "OGC sample dataset ready at $databasePath"
