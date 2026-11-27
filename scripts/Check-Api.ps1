param([ValidateRange(1,65535)][int]$Port = 5180)
$ErrorActionPreference = 'Stop'
$base = "http://127.0.0.1:$Port"
$editor = @{ Authorization = 'Bearer local-a' }
$other = @{ Authorization = 'Bearer local-b' }
$member = @{ Authorization = 'Bearer local-member' }

function Request($method, $path, $expected, $headers = @{}, $body = $null) {
    $params = @{ Uri = "$base$path"; Method = $method; Headers = $headers; TimeoutSec = 10; UseBasicParsing = $true }
    if ($null -ne $body) {
        $params.ContentType = 'application/json; charset=utf-8'
        $params.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 12))
    }
    try {
        $response = Invoke-WebRequest @params
        $code = [int]$response.StatusCode
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        $code = [int]$_.Exception.Response.StatusCode
    }
    if ($code -ne $expected) { throw "$method $path returned $code (expected $expected)." }
    if ($code -lt 300) { return ($response.Content | ConvertFrom-Json) }
}

function Check($condition, $description) {
    if (!$condition) { throw "FAILED: $description" }
    Write-Host "OK  $description"
}

$schema = Request GET '/openapi/v1.json' 200
Check ($null -ne $schema.paths.'/api/landings') 'OpenAPI contains the editor endpoint'
$slug = 'smoke-' + [guid]::NewGuid().ToString('N')
$content = @{
    name = 'First version'; type = 'course'; theme = @{}
    blocks = @(
        @{ id = 'a'; type = 'heading'; props = @{ title = 'Welcome' } }
        @{ id = 'b'; type = 'promo'; props = @{ title = 'Members' }; segments = @('member') }
        @{ id = 'c'; type = 'hero'; props = @{ title = 'Hidden' }; hidden = $true }
    )
}
$draft = Request POST '/api/landings' 201 $editor @{ slug = $slug; content = $content }
$path = "/api/landings/$($draft.id)"
$public = "/api/public/landings/$slug"
$owned = Request GET $path 200 $editor
Request GET $path 401
Request GET $path 404 $other
Request GET $public 404
Check ($owned.slug -eq $slug) 'Draft is private and readable by its owner'

$null = Request POST "$path/publish" 200 $editor @{ revision = $draft.revision }
$guestPage = Request GET $public 200
$memberPage = Request GET $public 200 $member
Check (($guestPage.blocks.id -join ',') -eq 'a') 'Guest receives the shared block only'
Check (($memberPage.blocks.id -join ',') -eq 'a,b') 'Member also receives the offer; hidden block stays hidden'

$content.name = 'Second version'
$updated = Request PUT $path 200 $editor @{ revision = $draft.revision; content = $content }
Request POST "$path/publish" 409 $editor @{ revision = $draft.revision }
$oldPage = Request GET $public 200
Check ($oldPage.name -eq 'First version') 'Edits and stale publication do not change the live page'
$null = Request POST "$path/publish" 200 $editor @{ revision = $updated.revision }
$newPage = Request GET $public 200
Check ($newPage.name -eq 'Second version' -and $newPage.revision -eq $updated.revision) 'Second publication replaces the active revision'
Write-Host "PASS  $base$public"
