$ErrorActionPreference = 'Stop'
$api = 'http://localhost:5075/api/v1'
$checks = 0
function Assert-Check([bool]$condition, [string]$message) { if (-not $condition) { throw $message }; $script:checks++ }
$public = Invoke-WebRequest "$api/creator/699663b3-887f-4182-94b7-f78c6dc35404" -SkipHttpErrorCheck
Assert-Check ($public.StatusCode -eq 200) 'Guest creator profile should be public'
foreach ($method in @('GET', 'PUT')) {
  $r = Invoke-WebRequest "$api/creator/me" -Method $method -ContentType 'application/json' -Body $(if ($method -eq 'PUT') { '{}' } else { $null }) -SkipHttpErrorCheck
  Assert-Check ($r.StatusCode -eq 401) 'Private profile requires authentication'
}
$seed = Get-Content 'src/ArtCommission.API/Common/DevelopmentDemoSeeder.cs' -Raw
$email = [regex]::Match($seed, 'CreatorEmail = "([^"]+)"').Groups[1].Value
$password = [regex]::Match($seed, 'Password = "([^"]+)"').Groups[1].Value
$auth = (Invoke-RestMethod "$api/auth/login" -Method Post -ContentType 'application/json' -Body (@{email=$email; password=$password} | ConvertTo-Json)).data
$headers = @{Authorization='Bearer '+$auth.tokens.accessToken}
$before = (Invoke-RestMethod "$api/creator/me" -Headers $headers).data
foreach ($case in @(@{websiteUrl='javascript:alert(1)'}, @{bannerUrl='not-a-url'}, @{bannerUrl='data:image/png;base64,abc'}, @{websiteUrl=('https://example.com/'+('x'*500))}, @{location=('x'*201)}, @{availableSlots=-1}, @{availableSlots=101})) {
  $r = Invoke-WebRequest "$api/creator/me" -Method Put -Headers $headers -ContentType 'application/json' -Body ($case | ConvertTo-Json) -SkipHttpErrorCheck
  Assert-Check ($r.StatusCode -eq 400) 'Invalid profile update must return 400'
}
$after = (Invoke-RestMethod "$api/creator/me" -Headers $headers).data
Assert-Check (($before | ConvertTo-Json -Depth 10 -Compress) -eq ($after | ConvertTo-Json -Depth 10 -Compress)) 'Rejected requests must leave profile unchanged'
$restore = @{}
foreach ($field in @('displayName','headline','bio','specialties','location','websiteUrl','bannerUrl','isAcceptingOrders','availableSlots')) { $restore[$field] = $before.$field }
try {
  $changed = (Invoke-RestMethod "$api/creator/me" -Method Put -Headers $headers -ContentType 'application/json' -Body (@{headline=''; websiteUrl=''; isAcceptingOrders=$false; availableSlots=0} | ConvertTo-Json)).data
  Assert-Check ($changed.id -eq $before.id -and -not $changed.headline -and -not $changed.websiteUrl -and -not $changed.isAcceptingOrders -and $changed.availableSlots -eq 0) 'Edit must preserve ID, clear fields and save false/zero'
} finally {
  $null = Invoke-RestMethod "$api/creator/me" -Method Put -Headers $headers -ContentType 'application/json' -Body ($restore | ConvertTo-Json)
}
Write-Output "Passed $checks live profile API checks; demo profile restored."
