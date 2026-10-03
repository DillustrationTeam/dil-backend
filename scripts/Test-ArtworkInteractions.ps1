$ErrorActionPreference = 'Stop'
$api = 'http://localhost:5075/api/v1'
$seed = Get-Content (Join-Path $PSScriptRoot '../src/ArtCommission.API/Common/DevelopmentDemoSeeder.cs') -Raw
function Login-Demo([string]$field) {
    $email = [regex]::Match($seed, ($field + ' = "([^"]+)"')).Groups[1].Value
    $password = [regex]::Match($seed, 'Password = "([^"]+)"').Groups[1].Value
    $auth = Invoke-RestMethod "$api/auth/login" -Method Post -ContentType 'application/json' -Body (@{email=$email;password=$password} | ConvertTo-Json)
    return @{ Authorization = 'Bearer ' + $auth.data.tokens.accessToken }
}
$headers = Login-Demo 'ClientEmail'
$otherHeaders = Login-Demo 'CreatorEmail'
$artwork = (Invoke-RestMethod "$api/artworks").data | Select-Object -First 1
if (!$artwork) { throw 'An approved artwork is required for the local smoke test.' }
$url = "$api/marketplace/artworks/$($artwork.id)"
$before = (Invoke-RestMethod "$url/interactions" -Headers $headers).data
$albumId = $null
$checks = 0
function Assert-Test([bool]$condition, [string]$name) {
    if (!$condition) { throw $name }
    $script:checks++
}
function Assert-Status([scriptblock]$action, [int]$expected) {
    try { & $action | Out-Null; throw "Expected HTTP $expected" }
    catch { if ([int]$_.Exception.Response.StatusCode -ne $expected) { throw }; $script:checks++ }
}
try {
    Assert-Status { Invoke-RestMethod "$url/favorite" -Method Put } 401
    $like = (Invoke-RestMethod "$url/favorite" -Method Put -Headers $headers).data
    Assert-Test $like.isLiked 'Like PUT returns persisted true'
    Invoke-RestMethod "$url/favorite" -Method Put -Headers $headers | Out-Null
    $reloaded = (Invoke-RestMethod "$url/interactions" -Headers $headers).data
    Assert-Test ($reloaded.isLiked -and $reloaded.likeCount -eq $like.likeCount) 'Repeated PUT does not double count'
    $likedLibrary = (Invoke-RestMethod "$api/marketplace/me/favorites" -Headers $headers).data
    Assert-Test ($likedLibrary.id -contains $artwork.id) 'Liked artwork appears in personal library'
    $created = (Invoke-RestMethod "$api/marketplace/me/collections" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{name='Interaction smoke ' + [guid]::NewGuid();isPublic=$false} | ConvertTo-Json)).data
    $albumId = [guid]$created.id
    $membership = "$api/marketplace/me/collections/$albumId/artworks/$($artwork.id)"
    $saved = (Invoke-RestMethod $membership -Method Put -Headers $headers).data
    Assert-Test ($saved.collectionIds -contains $created.id) 'Bookmark PUT returns collection membership'
    Assert-Test $saved.isLiked 'Bookmark preserves like'
    Invoke-RestMethod $membership -Method Put -Headers $headers | Out-Null
    $contents = (Invoke-RestMethod "$api/marketplace/me/collections/$albumId/artworks" -Headers $headers).data
    Assert-Test ($contents.Count -eq 1) 'Saved artwork appears once in album'
    $albums = (Invoke-RestMethod "$api/marketplace/me/collections" -Headers $headers).data
    Assert-Test (($albums | Where-Object id -eq $created.id).artworkCount -eq 1) 'Collection counter matches contents'
    Assert-Status { Invoke-RestMethod $membership -Method Delete -Headers $otherHeaders } 404
    Assert-Status { Invoke-RestMethod "$api/marketplace/me/collections/$albumId/artworks" -Headers $otherHeaders } 404
    $unliked = (Invoke-RestMethod "$url/favorite" -Method Delete -Headers $headers).data
    Assert-Test (!$unliked.isLiked -and $unliked.collectionIds -contains $created.id) 'Unlike preserves bookmark'
    $removed = (Invoke-RestMethod $membership -Method Delete -Headers $headers).data
    Assert-Test (!$removed.isLiked -and $removed.collectionIds -notcontains $created.id) 'Removing bookmark leaves like independent'
    Invoke-RestMethod $membership -Method Delete -Headers $headers | Out-Null
    $anonymous = (Invoke-RestMethod "$url/interactions").data
    Assert-Test ($anonymous.collectionIds.Count -eq 0 -and !$anonymous.isLiked) 'Anonymous response hides private states'
    Write-Output "Passed $checks authenticated HTTP interaction checks."
}
finally {
    $method = if ($before.isLiked) { 'Put' } else { 'Delete' }
    Invoke-RestMethod "$url/favorite" -Method $method -Headers $headers | Out-Null
    if ($albumId) {
        # Only this script's generated album is removed. GUID coercion prevents SQL interpolation of arbitrary input.
        sqlcmd -S localhost -d DillustrationLocal -E -C -b -Q "DELETE FROM CollectionArtworks WHERE CollectionId='$albumId'; DELETE FROM PersonalCollections WHERE Id='$albumId';"
        if ($LASTEXITCODE -ne 0) { throw 'Smoke-test album cleanup failed.' }
    }
    Write-Output 'Restored original like state and removed smoke-test album.'
}
