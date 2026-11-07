#Requires -Version 7.4
<#
    Repo-Konfiguration der CI. Wird von jedem PowerShell-Einstieg unter ci/ dot-gesourct.
    Nach Kronrat: dies ist die EINZIGE Datei, die sich zwischen den Repos unterscheiden soll.
#>

$script:RepoName = 'schach'

# Basis-SemVer; ci/version.ps1 haengt +build.<count>.sha.<short> an.
$script:BaseVersion = '0.0.0'

$script:Solution = 'Schach.slnx'

# xUnit v3 auf Microsoft.Testing.Platform (global.json test.runner).
$script:TestRunner = 'MTP'

# Stufen in Anzeigereihenfolge.
$script:Stages = @(
    'secrets'
    'scripts'
    'format'
    'build'
    'test'
    'vulns'
    'web'
    'engine'
)

$script:StagePaths = @{
    scripts = @('**/*.sh')
    format  = @('**/*.cs', '*.slnx', 'Directory.Build.*', 'Directory.Packages.props', '.editorconfig')
    build   = @('**/*.cs', '*.slnx', '**/*.csproj', 'Directory.Build.*', 'Directory.Packages.props', 'global.json', 'tests/fen-suite/**')
    test    = @('**/*.cs', 'tests/**', '*.slnx', '**/*.csproj', 'Directory.Build.*', 'Directory.Packages.props', 'global.json')
    vulns   = @('Directory.Packages.props', '**/*.csproj')
    web     = @('src/Schach.Web/**', '.nvmrc')
    engine  = @('engine/**', 'tests/fen-suite/**')
}

# Gruppen laufen nacheinander, Stufen einer Gruppe parallel. build vor test (test laeuft --no-build).
$script:ParallelGroups = @(
    @('secrets', 'scripts', 'format', 'web', 'engine'),
    @('build'),
    @('test', 'vulns')
)

$script:DotnetSdkMinimum = '10.0.403'

# Frontend: Node 22 (src/Schach.Web), gespiegelt in .nvmrc.
$script:NodeVersion = '22'

# Mindest-Schweregrad, der die vulns-Stufe scheitern laesst: Low, Moderate, High, Critical.
$script:VulnFailSeverity = 'High'
