#Requires -Version 5.1
<#
.SYNOPSIS
    Configure Flutter build caches for the current PowerShell session.
.EXAMPLE
    . .\scripts\Windows\powershell\initialize-flutter-cache.ps1 -CacheRoot D:\WKS\FlutterBuildCache
.DESCRIPTION
    Reuses existing Gradle and Pub caches and redirects temporary compiler
    files. Run before Flutter commands in each terminal. Does not delete caches
    or change the Windows temporary directory for other applications.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $CacheRoot
)

if ($CacheRoot -notmatch '^[A-Za-z]:[\\/]') {
    throw 'CacheRoot must be an absolute local drive path.'
}
$flutterCacheRoot = [System.IO.Path]::GetFullPath($CacheRoot)
$flutterCacheDirectories = @{
    GRADLE_USER_HOME = Join-Path $flutterCacheRoot 'gradle'
    PUB_CACHE = Join-Path $flutterCacheRoot 'pub'
    TEMP = Join-Path $flutterCacheRoot 'temp'
    TMP = Join-Path $flutterCacheRoot 'temp'
}
foreach ($flutterCacheDirectory in ($flutterCacheDirectories.Values | Select-Object -Unique)) {
    if (Test-Path -LiteralPath $flutterCacheDirectory) {
        if (-not (Test-Path -LiteralPath $flutterCacheDirectory -PathType Container)) {
            throw "Cache directory is occupied by a file: $flutterCacheDirectory"
        }
    } else {
        New-Item -ItemType Directory -Path $flutterCacheDirectory -ErrorAction Stop | Out-Null
    }
}
foreach ($flutterCacheVariable in $flutterCacheDirectories.Keys) {
    [Environment]::SetEnvironmentVariable(
        $flutterCacheVariable, $flutterCacheDirectories[$flutterCacheVariable], 'Process'
    )
}
Write-Host "Flutter caches: $flutterCacheRoot (Gradle, Pub and temporary compiler files)."
