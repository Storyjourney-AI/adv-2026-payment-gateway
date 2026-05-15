param(
    [string]$BaseUrl = "http://localhost:5550",
    [string]$TurnstileHeaderName = "X-Turnstile-Token",
    [string]$DevBypassToken = "dev-turnstile-bypass"
)

function Invoke-JsonPost {
    param(
        [string]$Url,
        [string]$BodyJson,
        [hashtable]$Headers = @{}
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -Method Post -ContentType "application/json" -Headers $Headers -Body $BodyJson -ErrorAction Stop
        return [int]$response.StatusCode
    } catch {
        if ($_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode
        }
        return 0
    }
}

Write-Host "Running security smoke tests against $BaseUrl"

# 1) Captcha required on login
$loginBody = '{"email":"bad@example.com","password":"x"}'
$loginNoCaptcha = Invoke-JsonPost -Url "$BaseUrl/api/auth/login" -BodyJson $loginBody
$loginWithBypass = Invoke-JsonPost -Url "$BaseUrl/api/auth/login" -BodyJson $loginBody -Headers @{ $TurnstileHeaderName = $DevBypassToken }

Write-Host "login_no_captcha_status=$loginNoCaptcha (expected 401)"
Write-Host "login_with_bypass_status=$loginWithBypass (expected 401/200 depending credentials)"

# 2) Webhook missing required field rejected
$webhookMissing = '{"order_id":"","status_code":"200","gross_amount":"10000.00","signature_key":"abc","transaction_status":"pending","transaction_id":"tx-smoke"}'
$webhookMissingStatus = Invoke-JsonPost -Url "$BaseUrl/api/midtrans/payment" -BodyJson $webhookMissing
Write-Host "webhook_missing_field_status=$webhookMissingStatus (expected 400)"

Write-Host "Smoke tests completed."
