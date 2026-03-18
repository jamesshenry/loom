# codebase.ps1 - Generate codebase documentation for AI analysis
# This script creates a comprehensive text file containing the directory structure
# and all source code files from your project's source directory for AI processing.
#
# Customize the $sourceDirectory path to match your project's structure.

$repoRoot = git rev-parse --show-toplevel
Write-Host "Repository root: $repoRoot"

$sourceDirectory = Join-Path $repoRoot 'src' 
Write-Host "Source directory: $sourceDirectory"

$outputDir = "$repoRoot/.ai/outputs"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force
}

$outputPath = Join-Path $outputDir 'codebase.txt'
Write-Host "Output path: $outputPath"

# Define the exclusion pattern (bin, obj, and Tests)
# This regex matches the folder names surrounded by directory separators
$excludePattern = '[\\/](bin|obj|Tests)([\\/]|$)'

# Build directory tree
$directoryTree = Get-ChildItem -Directory -Path $sourceDirectory -Recurse | Where-Object {
    $_.FullName -notmatch $excludePattern
} | ForEach-Object {
    $indent = '  ' * ($_.FullName.Split('\').Length - $sourceDirectory.Split('\').Length)
    "$indent- $($_.Name)"
} | Out-String

$contextBlock = "$directoryTree`n# --- Start of Code Files ---`n`n"
Set-Content -Path $outputPath -Value $contextBlock

# Extension -> language mapping
$languageMap = @{
    '.cs'   = 'csharp'
    '.ps1'  = 'powershell'
    '.json' = 'json'
    '.xml'  = 'xml'
    '.yml'  = 'yaml'
    '.yaml' = 'yaml'
    '.md'   = 'markdown'
    '.sh'   = 'bash'
    '.ts'   = 'typescript'
    '.js'   = 'javascript'
}

# Grab all files, filtering out the excluded directories
$allFiles = Get-ChildItem -Path $sourceDirectory -Recurse -File -Include *.cs, *.ps1, *.json, *.xml, *.yml, *.yaml, *.md, *.sh, *.ts, *.js | Where-Object {
    $_.FullName -notmatch $excludePattern
}

foreach ($file in $allFiles) {
    # Calculate relative path from the repo root for clearer documentation
    $relativePath = $file.FullName.Substring($repoRoot.Length + 1)

    $ext = $file.Extension.ToLower()
    $lang = if ($languageMap.ContainsKey($ext)) { $languageMap[$ext] } else { 'text' }

    $filePathHeader = @"
// File: $relativePath
"@

    $codeBlockStart = "`n`n``````$lang`n"

    $codeBlockEnd = "`n```````n"

    $fileContent = Get-Content -Path $file.FullName -Raw
    $formattedContent = $filePathHeader + $codeBlockStart + $fileContent + $codeBlockEnd
    Add-Content -Path $outputPath -Value $formattedContent
}
Write-Host "Done! Codebase exported to $outputPath" -ForegroundColor Green
