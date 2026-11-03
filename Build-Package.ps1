param(
    [string]$RepoGamePath = $env:REPO_GAME_DIR,
    [Parameter(Mandatory = $true)][string]$RepoConfigPath,
    [string]$PreviousDll
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location $PSScriptRoot
try {
    if (-not $RepoGamePath -and (Test-Path -LiteralPath 'Directory.Repo.props')) {
        [xml]$localProperties = Get-Content -LiteralPath 'Directory.Repo.props' -Raw
        $RepoGamePath = [string]$localProperties.Project.PropertyGroup.RepoGamePath
    }
    if (-not $RepoGamePath) { throw 'Provide RepoGamePath or set REPO_GAME_DIR.' }
    $managed = Join-Path $RepoGamePath 'REPO_Data\Managed'
    $gameDll = Join-Path $managed 'Assembly-CSharp.dll'
    $RepoConfigPath = (Resolve-Path -LiteralPath $RepoConfigPath).Path
    $dll = Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\EliteEnemyVariants.dll'
    $menu = Join-Path $PSScriptRoot 'lib\MenuLib.dll'
    if (-not (Test-Path -LiteralPath $gameDll)) { throw 'Game Assembly-CSharp.dll not found.' }
    dotnet build EliteEnemyVariants.csproj -c Release "-p:RepoGamePath=$RepoGamePath" -p:NuGetAudit=false -warnaserror
    if ($LASTEXITCODE) { throw 'Release build failed.' }
    $compatibilityArgs = @($managed, $dll, $menu, $RepoConfigPath)
    if ($PreviousDll) { $compatibilityArgs += (Resolve-Path -LiteralPath $PreviousDll).Path }
    $suites = @(
        @{ Project = 'checks\Compatibility\Compatibility.Checks.csproj'; Arguments = $compatibilityArgs },
        @{ Project = 'checks\Menu\Menu.Checks.csproj'; Arguments = @($dll, $gameDll) },
        @{ Project = 'checks\Presentation\Presentation.Checks.csproj'; Arguments = @($gameDll, (Join-Path $PSScriptRoot '..\RoleShuffle')) },
        @{ Project = 'checks\Tactics\Tactics.Checks.csproj'; Arguments = @($gameDll, $dll) },
        @{ Project = 'checks\Pony\Pony.Checks.csproj'; Arguments = @($gameDll, $dll) },
        @{ Project = 'checks\Localization\Localization.Checks.csproj'; Arguments = @($dll, $PSScriptRoot, $managed) },
        @{ Project = 'checks\OrbRewards\OrbRewards.Checks.csproj'; Arguments = @($gameDll) }
    )
    foreach ($suite in $suites) {
        dotnet run --project $suite.Project -c Release -- @($suite.Arguments)
        if ($LASTEXITCODE) { throw "Checks failed: $($suite.Project)" }
    }
    $manifest = Get-Content -LiteralPath 'package\manifest.json' -Raw | ConvertFrom-Json
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dll).Version
    if ($assemblyVersion.ToString(3) -ne $manifest.version_number) { throw 'DLL/manifest versions differ.' }
    if ((Get-Content -LiteralPath 'package\CHANGELOG.md' -TotalCount 1) -ne "## v$($manifest.version_number)") {
        throw 'Changelog version differs.'
    }
    Copy-Item -LiteralPath $dll -Destination 'package\EliteEnemyVariants.dll'
    $entries = @('README.md','CHANGELOG.md','icon.png','manifest.json','EliteEnemyVariants.dll')
    $entries += @('LICENSES/NotoCJK-OFL.txt','LICENSES/NotoSans-OFL.txt')
    $files = @('README.md','CHANGELOG.md','icon.png','manifest.json','EliteEnemyVariants.dll','LICENSES' | ForEach-Object { Join-Path $PSScriptRoot "package\$_" })
    $zipPath = Join-Path $PSScriptRoot 'EliteEnemyVariants.zip'
    Compress-Archive -LiteralPath $files -DestinationPath $zipPath -Force
    $expectedHash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath 'package\EliteEnemyVariants.dll' -Algorithm SHA256).Hash -ne $expectedHash) {
        throw 'Package DLL hash mismatch.'
    }
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $zipFiles = @($zip.Entries | Where-Object { -not $_.FullName.EndsWith('/') })
        if (@(Compare-Object ($entries | Sort-Object) ($zipFiles.FullName.Replace('\','/') | Sort-Object)).Count) { throw 'ZIP entry list differs.' }
        foreach ($entry in $zipFiles) {
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
            finally { $sha.Dispose(); $stream.Dispose() }
            if ($hash -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot "package\$($entry.FullName)") -Algorithm SHA256).Hash) {
                throw "ZIP content mismatch: $($entry.FullName)"
            }
        }
    } finally { $zip.Dispose() }
    Write-Output "PASS: build/package/ZIP DLL SHA-256 $expectedHash"
    Write-Output "Package: $zipPath"
} finally { Pop-Location }
