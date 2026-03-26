param(
    [string]$SourceDir = 'Fixtures',
    [string]$OutputFile = 'fixtures.bundle'
)

$FixtureDir = Join-Path $PSScriptRoot $SourceDir
$StagingDir = Join-Path $PSScriptRoot '.fixture-staging'
$BundlePath = Join-Path $PSScriptRoot $OutputFile

if (Test-Path $StagingDir) {
    Remove-Item -Recurse -Force $StagingDir
}

Write-Host "Staging fixtures to $StagingDir..." -ForegroundColor Cyan
Copy-Item -Path $FixtureDir -Destination $StagingDir -Recurse

Push-Location $StagingDir

try {
    Write-Host 'Initializing git repo and creating bundle...' -ForegroundColor Cyan
    git init
    git config user.email 'test@test.com'
    git config user.name 'Test'
    # Remove build artifacts — they get rebuilt on each clone anyway (git resets timestamps)
    if (Test-Path '.artifacts') { Remove-Item -Recurse -Force '.artifacts' }
    git add .
    git commit -m 'init'
    git tag 1.0.0
    git tag 1.2.3
    git tag 'nuget=3.0.0'
    
    if (Test-Path $BundlePath) {
        Remove-Item $BundlePath
    }
    
    git bundle create $BundlePath --all
    Write-Host "Bundle created at $BundlePath" -ForegroundColor Green
}
finally {
    Pop-Location
    Remove-Item -Recurse -Force $StagingDir
}
