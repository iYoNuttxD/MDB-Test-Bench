[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'osx-arm64', 'osx-x64', 'linux-x64')]
    [string] $RuntimeIdentifier,
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$')]
    [string] $Version = '0.1.0'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$publishDirectory = Join-Path $artifactsRoot "publish/$RuntimeIdentifier"
$stagingDirectory = Join-Path $artifactsRoot "staging/$RuntimeIdentifier"
$packagesDirectory = Join-Path $artifactsRoot 'packages'

foreach ($directory in @($publishDirectory, $stagingDirectory)) {
    if (Test-Path $directory) { Remove-Item $directory -Recurse -Force }
    New-Item $directory -ItemType Directory -Force | Out-Null
}
New-Item $packagesDirectory -ItemType Directory -Force | Out-Null

dotnet publish (Join-Path $repositoryRoot 'src/MdbTestBench.App/MdbTestBench.App.csproj') `
    --configuration Release `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --output $publishDirectory `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $RuntimeIdentifier" }
Get-ChildItem $publishDirectory -Filter '*.pdb' -File -Recurse | Remove-Item -Force
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $stagingDirectory 'smoke-cache'
New-Item $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR -ItemType Directory -Force | Out-Null

if ($RuntimeIdentifier -eq 'win-x64') {
    $executable = Join-Path $publishDirectory 'MDB-Test-Bench.exe'
    $process = Start-Process $executable -ArgumentList '--smoke-test' -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw 'Published Windows smoke test failed.' }
    $package = Join-Path $packagesDirectory "MDB-Test-Bench-v$Version-windows-x64.zip"
    if (Test-Path $package) { Remove-Item $package -Force }
    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $package
}
elseif ($RuntimeIdentifier.StartsWith('osx-')) {
    $appDirectory = Join-Path $stagingDirectory 'MDB Test Bench.app'
    $macOsDirectory = Join-Path $appDirectory 'Contents/MacOS'
    New-Item $macOsDirectory -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $publishDirectory '*') $macOsDirectory -Recurse -Force
    $template = Get-Content (Join-Path $repositoryRoot 'build/macos/Info.plist.template') -Raw
    $template.Replace('@VERSION@', $Version) | Set-Content (Join-Path $appDirectory 'Contents/Info.plist') -NoNewline
    $architecture = $RuntimeIdentifier.Substring(4)
    $package = Join-Path $packagesDirectory "MDB-Test-Bench-v$Version-macos-$architecture.zip"
    if (Test-Path $package) { Remove-Item $package -Force }
    Compress-Archive -Path $appDirectory -DestinationPath $package
}
else {
    $executable = Join-Path $publishDirectory 'MDB-Test-Bench'
    & chmod +x $executable
    & $executable --smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Published Linux smoke test failed.' }
    $package = Join-Path $packagesDirectory "MDB-Test-Bench-v$Version-linux-x64.tar.gz"
    if (Test-Path $package) { Remove-Item $package -Force }
    & tar -C $publishDirectory -czf $package .
    if ($LASTEXITCODE -ne 0) { throw 'Linux archive creation failed.' }
}

Write-Output $package
