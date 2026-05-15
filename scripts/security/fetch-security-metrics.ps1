param(
    [string]$BaseUrl = "http://localhost:5550",
    [string]$AccessToken = ""
)

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    Write-Error "AccessToken is required. Pass -AccessToken '<jwt>'"
    exit 1
}

try {
    $response = Invoke-WebRequest `
        -Uri "$BaseUrl/api/security/metrics" `
        -Method Get `
        -Headers @{ Authorization = "Bearer $AccessToken" } `
        -ContentType "application/json"

    Write-Host $response.Content
}
catch {
    if ($_.Exception.Response) {
        $status = [int]$_.Exception.Response.StatusCode
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $body = $reader.ReadToEnd()
        Write-Error "Failed to fetch metrics. Status=$status Body=$body"
        exit 1
    }

    throw
}
