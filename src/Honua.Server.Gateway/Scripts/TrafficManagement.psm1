# Copyright (c) 2025 HonuaIO
# Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    PowerShell module for YARP traffic management operations.

.DESCRIPTION
    Provides cmdlets for managing traffic distribution in blue-green and canary deployments
    using the Honua YARP gateway API.

.EXAMPLE
    Import-Module ./TrafficManagement.psm1
    Connect-TrafficGateway -Url "https://gateway.honua.io" -Token $env:GATEWAY_TOKEN
    Switch-Traffic -ServiceName "honua-api" -GreenPercentage 50

.NOTES
    Author: Honua.io
    Version: 1.0.0
    Requires: PowerShell 7.0+
#>

# Module-level variables
$script:GatewayUrl = $null
$script:GatewayToken = $null
$script:HttpClient = $null

<#
.SYNOPSIS
    Connects to the YARP traffic management gateway.

.DESCRIPTION
    Initializes connection to the gateway API with authentication credentials.
    Must be called before any other cmdlets in this module.

.PARAMETER Url
    Base URL of the YARP gateway (e.g., https://gateway.honua.io)

.PARAMETER Token
    Bearer token for API authentication

.EXAMPLE
    Connect-TrafficGateway -Url "https://gateway.honua.io" -Token $env:GATEWAY_TOKEN
#>
function Connect-TrafficGateway {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Url,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Token
    )

    $script:GatewayUrl = $Url.TrimEnd('/')
    $script:GatewayToken = $Token

    # Initialize HTTP client
    $script:HttpClient = [System.Net.Http.HttpClient]::new()
    $script:HttpClient.BaseAddress = [Uri]::new($script:GatewayUrl)
    $script:HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer $Token")
    $script:HttpClient.DefaultRequestHeaders.Add("User-Agent", "Honua-PowerShell-Client/1.0")
    $script:HttpClient.Timeout = [TimeSpan]::FromMinutes(10)

    Write-Verbose "Connected to gateway at $script:GatewayUrl"
}

<#
.SYNOPSIS
    Switches traffic between blue and green environments.

.DESCRIPTION
    Updates traffic distribution for a service cluster. Supports gradual rollout
    by specifying percentage of traffic to route to green environment.

.PARAMETER ServiceName
    Name of the service cluster (e.g., "honua-api")

.PARAMETER BlueEndpoint
    URL of the blue (current) environment

.PARAMETER GreenEndpoint
    URL of the green (new) environment

.PARAMETER GreenPercentage
    Percentage of traffic to route to green (0-100)

.EXAMPLE
    Switch-Traffic -ServiceName "honua-api" -GreenPercentage 50

.EXAMPLE
    Switch-Traffic -ServiceName "honua-api" `
        -BlueEndpoint "http://api-blue:8080" `
        -GreenEndpoint "http://api-green:8080" `
        -GreenPercentage 25
#>
function Switch-Traffic {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$ServiceName,

        [Parameter(Mandatory = $false)]
        [string]$BlueEndpoint = "http://$ServiceName-blue:8080",

        [Parameter(Mandatory = $false)]
        [string]$GreenEndpoint = "http://$ServiceName-green:8080",

        [Parameter(Mandatory = $true)]
        [ValidateRange(0, 100)]
        [int]$GreenPercentage
    )

    Test-GatewayConnection

    Write-Host "Switching traffic for $ServiceName`: $GreenPercentage% green, $($100 - $GreenPercentage)% blue" -ForegroundColor Cyan

    $requestBody = @{
        serviceName    = $ServiceName
        blueEndpoint   = $BlueEndpoint
        greenEndpoint  = $GreenEndpoint
        greenPercentage = $GreenPercentage
    } | ConvertTo-Json

    $content = [System.Net.Http.StringContent]::new($requestBody, [System.Text.Encoding]::UTF8, "application/json")

    try {
        $response = $script:HttpClient.PostAsync("/admin/traffic/switch", $content).GetAwaiter().GetResult()
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        if ($response.IsSuccessStatusCode) {
            $result = $responseBody | ConvertFrom-Json

            Write-Host "Traffic switched successfully!" -ForegroundColor Green
            Write-Host "  Blue: $($result.blueTrafficPercentage)%" -ForegroundColor Blue
            Write-Host "  Green: $($result.greenTrafficPercentage)%" -ForegroundColor Green

            return $result
        }
        else {
            Write-Error "Traffic switch failed: HTTP $([int]$response.StatusCode) - $responseBody"
            return $null
        }
    }
    catch {
        Write-Error "Failed to switch traffic: $_"
        return $null
    }
    finally {
        $content.Dispose()
    }
}

<#
.SYNOPSIS
    Performs automated canary deployment with health checks.

.DESCRIPTION
    Gradually rolls out new version with automatic health monitoring and rollback.
    Traffic is increased in steps with soak periods between each stage.

.PARAMETER ServiceName
    Name of the service cluster

.PARAMETER BlueEndpoint
    URL of the blue (current) environment

.PARAMETER GreenEndpoint
    URL of the green (new) environment

.PARAMETER TrafficSteps
    Array of traffic percentages for each stage (default: 10, 25, 50, 100)

.PARAMETER SoakDurationSeconds
    Seconds to wait at each stage before proceeding (default: 60)

.PARAMETER AutoRollback
    Automatically rollback on health check failure (default: true)

.EXAMPLE
    Start-CanaryDeployment -ServiceName "honua-api"

.EXAMPLE
    Start-CanaryDeployment -ServiceName "honua-api" `
        -TrafficSteps @(10, 30, 60, 100) `
        -SoakDurationSeconds 300 `
        -AutoRollback $true
#>
function Start-CanaryDeployment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$ServiceName,

        [Parameter(Mandatory = $false)]
        [string]$BlueEndpoint = "http://$ServiceName-blue:8080",

        [Parameter(Mandatory = $false)]
        [string]$GreenEndpoint = "http://$ServiceName-green:8080",

        [Parameter(Mandatory = $false)]
        [int[]]$TrafficSteps = @(10, 25, 50, 100),

        [Parameter(Mandatory = $false)]
        [int]$SoakDurationSeconds = 60,

        [Parameter(Mandatory = $false)]
        [bool]$AutoRollback = $true
    )

    Test-GatewayConnection

    Write-Host "Starting canary deployment for $ServiceName" -ForegroundColor Cyan
    Write-Host "Traffic steps: $($TrafficSteps -join ', ')%" -ForegroundColor Gray
    Write-Host "Soak duration: $SoakDurationSeconds seconds" -ForegroundColor Gray

    $requestBody = @{
        serviceName    = $ServiceName
        blueEndpoint   = $BlueEndpoint
        greenEndpoint  = $GreenEndpoint
        strategy       = @{
            trafficSteps         = $TrafficSteps
            soakDurationSeconds = $SoakDurationSeconds
            autoRollback        = $AutoRollback
        }
        healthCheckUrl = "$GreenEndpoint/health"
    } | ConvertTo-Json -Depth 3

    $content = [System.Net.Http.StringContent]::new($requestBody, [System.Text.Encoding]::UTF8, "application/json")

    try {
        Write-Host "Initiating canary deployment..." -ForegroundColor Yellow

        $response = $script:HttpClient.PostAsync("/admin/traffic/canary", $content).GetAwaiter().GetResult()
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        if ($response.IsSuccessStatusCode) {
            $result = $responseBody | ConvertFrom-Json

            if ($result.success) {
                Write-Host "`nCanary deployment completed successfully!" -ForegroundColor Green
                Write-Host "Stages completed: $($result.stages.Count)" -ForegroundColor Gray

                foreach ($stage in $result.stages) {
                    $healthIcon = if ($stage.isHealthy) { "✓" } else { "✗" }
                    $healthColor = if ($stage.isHealthy) { "Green" } else { "Red" }

                    Write-Host "  [$healthIcon] $($stage.greenTrafficPercentage)% - $(Get-Date $stage.timestamp -Format 'HH:mm:ss')" `
                        -ForegroundColor $healthColor
                }
            }
            else {
                Write-Warning "Canary deployment failed!"
                Write-Warning "Message: $($result.message)"

                if ($result.rolledBack) {
                    Write-Warning "Traffic automatically rolled back to blue"
                }
            }

            return $result
        }
        else {
            Write-Error "Canary deployment failed: HTTP $([int]$response.StatusCode) - $responseBody"
            return $null
        }
    }
    catch {
        Write-Error "Failed to start canary deployment: $_"
        return $null
    }
    finally {
        $content.Dispose()
    }
}

<#
.SYNOPSIS
    Immediately rolls back to 100% blue environment.

.DESCRIPTION
    Emergency rollback command that instantly routes all traffic to blue.

.PARAMETER ServiceName
    Name of the service cluster

.PARAMETER BlueEndpoint
    URL of the blue environment

.PARAMETER GreenEndpoint
    URL of the green environment

.EXAMPLE
    Rollback-Traffic -ServiceName "honua-api"
#>
function Rollback-Traffic {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$ServiceName,

        [Parameter(Mandatory = $false)]
        [string]$BlueEndpoint = "http://$ServiceName-blue:8080",

        [Parameter(Mandatory = $false)]
        [string]$GreenEndpoint = "http://$ServiceName-green:8080"
    )

    Test-GatewayConnection

    if ($PSCmdlet.ShouldProcess($ServiceName, "Rollback to 100% blue")) {
        Write-Warning "Rolling back $ServiceName to 100% blue..."

        $requestBody = @{
            serviceName    = $ServiceName
            blueEndpoint   = $BlueEndpoint
            greenEndpoint  = $GreenEndpoint
        } | ConvertTo-Json

        $content = [System.Net.Http.StringContent]::new($requestBody, [System.Text.Encoding]::UTF8, "application/json")

        try {
            $response = $script:HttpClient.PostAsync("/admin/traffic/rollback", $content).GetAwaiter().GetResult()
            $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

            if ($response.IsSuccessStatusCode) {
                $result = $responseBody | ConvertFrom-Json

                Write-Host "Rollback completed successfully!" -ForegroundColor Green
                Write-Host "  Blue: $($result.blueTrafficPercentage)%" -ForegroundColor Blue
                Write-Host "  Green: $($result.greenTrafficPercentage)%" -ForegroundColor Green

                return $result
            }
            else {
                Write-Error "Rollback failed: HTTP $([int]$response.StatusCode) - $responseBody"
                return $null
            }
        }
        catch {
            Write-Error "Failed to rollback traffic: $_"
            return $null
        }
        finally {
            $content.Dispose()
        }
    }
}

<#
.SYNOPSIS
    Gets current traffic distribution for a service.

.DESCRIPTION
    Queries the gateway for current traffic routing configuration.

.PARAMETER ServiceName
    Name of the service cluster

.EXAMPLE
    Get-TrafficStatus -ServiceName "honua-api"
#>
function Get-TrafficStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$ServiceName
    )

    Test-GatewayConnection

    try {
        $encodedServiceName = [Uri]::EscapeDataString($ServiceName)
        $response = $script:HttpClient.GetAsync("/admin/traffic/status?serviceName=$encodedServiceName").GetAwaiter().GetResult()
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        if ($response.IsSuccessStatusCode) {
            $result = $responseBody | ConvertFrom-Json

            Write-Host "Traffic status for $ServiceName`:" -ForegroundColor Cyan
            Write-Host "  Blue: $($result.blueTrafficPercentage)%" -ForegroundColor Blue
            Write-Host "  Green: $($result.greenTrafficPercentage)%" -ForegroundColor Green

            Write-Host "`nDestinations:" -ForegroundColor Gray
            foreach ($dest in $result.destinations.PSObject.Properties) {
                $healthIcon = if ($dest.Value.healthy) { "✓" } else { "✗" }
                $healthColor = if ($dest.Value.healthy) { "Green" } else { "Red" }

                Write-Host "  [$healthIcon] $($dest.Name): $($dest.Value.address) (weight: $($dest.Value.weight))" `
                    -ForegroundColor $healthColor
            }

            return $result
        }
        else {
            Write-Error "Failed to get traffic status: HTTP $([int]$response.StatusCode) - $responseBody"
            return $null
        }
    }
    catch {
        Write-Error "Failed to get traffic status: $_"
        return $null
    }
}

<#
.SYNOPSIS
    Performs instant cutover to 100% green.

.DESCRIPTION
    Immediately switches all traffic to green environment without gradual rollout.
    Use with caution - recommended to test with gradual rollout first.

.PARAMETER ServiceName
    Name of the service cluster

.PARAMETER BlueEndpoint
    URL of the blue environment

.PARAMETER GreenEndpoint
    URL of the green environment

.EXAMPLE
    Complete-Cutover -ServiceName "honua-api"
#>
function Complete-Cutover {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$ServiceName,

        [Parameter(Mandatory = $false)]
        [string]$BlueEndpoint = "http://$ServiceName-blue:8080",

        [Parameter(Mandatory = $false)]
        [string]$GreenEndpoint = "http://$ServiceName-green:8080"
    )

    Test-GatewayConnection

    if ($PSCmdlet.ShouldProcess($ServiceName, "Instant cutover to 100% green")) {
        Write-Warning "Performing instant cutover to green for $ServiceName..."

        $requestBody = @{
            serviceName    = $ServiceName
            blueEndpoint   = $BlueEndpoint
            greenEndpoint  = $GreenEndpoint
        } | ConvertTo-Json

        $content = [System.Net.Http.StringContent]::new($requestBody, [System.Text.Encoding]::UTF8, "application/json")

        try {
            $response = $script:HttpClient.PostAsync("/admin/traffic/instant-cutover", $content).GetAwaiter().GetResult()
            $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

            if ($response.IsSuccessStatusCode) {
                $result = $responseBody | ConvertFrom-Json

                Write-Host "Instant cutover completed!" -ForegroundColor Green
                Write-Host "  Blue: $($result.blueTrafficPercentage)%" -ForegroundColor Blue
                Write-Host "  Green: $($result.greenTrafficPercentage)%" -ForegroundColor Green

                return $result
            }
            else {
                Write-Error "Instant cutover failed: HTTP $([int]$response.StatusCode) - $responseBody"
                return $null
            }
        }
        catch {
            Write-Error "Failed to perform instant cutover: $_"
            return $null
        }
        finally {
            $content.Dispose()
        }
    }
}

<#
.SYNOPSIS
    Tests if gateway connection is established.

.DESCRIPTION
    Internal helper function to validate connection before API calls.
#>
function Test-GatewayConnection {
    if ($null -eq $script:GatewayUrl -or $null -eq $script:GatewayToken) {
        throw "Not connected to gateway. Run Connect-TrafficGateway first."
    }
}

<#
.SYNOPSIS
    Disconnects from the traffic gateway and cleans up resources.

.DESCRIPTION
    Disposes HTTP client and clears connection state.

.EXAMPLE
    Disconnect-TrafficGateway
#>
function Disconnect-TrafficGateway {
    [CmdletBinding()]
    param()

    if ($null -ne $script:HttpClient) {
        $script:HttpClient.Dispose()
        $script:HttpClient = $null
    }

    $script:GatewayUrl = $null
    $script:GatewayToken = $null

    Write-Verbose "Disconnected from gateway"
}

# Export module functions
Export-ModuleMember -Function @(
    'Connect-TrafficGateway',
    'Switch-Traffic',
    'Start-CanaryDeployment',
    'Rollback-Traffic',
    'Get-TrafficStatus',
    'Complete-Cutover',
    'Disconnect-TrafficGateway'
)
