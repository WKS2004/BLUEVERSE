param(
    [string]$CacheRoot = 'D:\WKS\FlutterBuildCache',
    [string[]]$FlutterArgs = @('analyze')
)

$ErrorActionPreference = 'Stop'
$taskRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskMobile = Join-Path $taskRepository 'apps/mobile'
$taskCache = [IO.Path]::GetFullPath($CacheRoot)
$taskBuild = [IO.Path]::GetFullPath((Join-Path $taskMobile 'build'))
$taskTarget = [IO.Path]::GetFullPath((Join-Path $taskCache 'projects/blueverse-adi/build'))
if (-not $taskTarget.StartsWith($taskCache.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The build target must stay inside the selected cache directory.'
}
foreach ($taskDirectory in @('gradle', 'pub', 'temp', 'projects/blueverse-adi')) {
    New-Item -ItemType Directory -Path (Join-Path $taskCache $taskDirectory) -Force | Out-Null
}
if (Test-Path -LiteralPath $taskBuild) {
    $taskItem = Get-Item -LiteralPath $taskBuild -Force
    if ($taskItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        if ([IO.Path]::GetFullPath([string]$taskItem.Target) -ne $taskTarget) {
            throw 'The existing build link uses another target; inspect it before changing it.'
        }
    } else {
        if (Test-Path -LiteralPath $taskTarget) {
            throw 'Both build directories exist; inspect them before moving generated files.'
        }
        Move-Item -LiteralPath $taskBuild -Destination $taskTarget
        New-Item -ItemType Junction -Path $taskBuild -Target $taskTarget | Out-Null
    }
} else {
    New-Item -ItemType Directory -Path $taskTarget -Force | Out-Null
    New-Item -ItemType Junction -Path $taskBuild -Target $taskTarget | Out-Null
}
$env:GRADLE_USER_HOME = Join-Path $taskCache 'gradle'
$env:PUB_CACHE = Join-Path $taskCache 'pub'
$env:TEMP = Join-Path $taskCache 'temp'
$env:TMP = $env:TEMP
Push-Location $taskMobile
try {
    & flutter @FlutterArgs
    $taskExitCode = $LASTEXITCODE
} finally { Pop-Location }
exit $taskExitCode
