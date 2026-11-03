$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$fixture=Join-Path $root ('outputs/counter-check-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'build/number.txt') -Destination $fixture
$initial=[int](Get-Content -LiteralPath (Join-Path $fixture 'number.txt') -Raw)
foreach($fail in @('false','true','false')) {
    dotnet msbuild (Join-Path $PSScriptRoot 'Counter.proj') -nologo -v:minimal "-p:EevCounterDirectory=$fixture" "-p:EevTestFail=$fail"
    if (($LASTEXITCODE -eq 0) -ne ($fail -eq 'false')) { throw 'Unexpected fixture result.' }
}
if ([int](Get-Content -LiteralPath (Join-Path $fixture 'number.txt') -Raw) -ne $initial+3) { throw 'A failed build reused its number.' }
$events=@(Get-Content -LiteralPath (Join-Path $fixture 'attempts.tsv'))
if (@($events | Where-Object {$_ -match "`tstarted$"}).Count -ne 3) { throw 'Each attempt must allocate once.' }
if (@($events | Where-Object {$_ -match "`tsucceeded$"}).Count -ne 2) { throw 'Failed attempt must not be marked successful.' }
dotnet msbuild (Join-Path $PSScriptRoot 'Counter.proj') -nologo -v:minimal "-p:EevCounterDirectory=$fixture" -p:DesignTimeBuild=true
if ($LASTEXITCODE -ne 0 -or [int](Get-Content -LiteralPath (Join-Path $fixture 'number.txt') -Raw) -ne $initial+3) { throw 'Design-time operation consumed a build number.' }
if ([int](Get-Content -LiteralPath (Join-Path $root 'build/number.txt') -Raw) -ne $initial) { throw 'Test changed the main counter.' }
Write-Output 'PASS: successful/failed/successful fixture builds allocate distinct numbers; design-time and test builds do not alter the main counter.'
