param([string]$Version = '2.0.0', [string]$ArtifactDirectory = 'artifacts/final')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Stable numeric version required.' }
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $declared = ([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.Version
    if ($Version -ne $declared) { throw 'Tag/package version differs from application version.' }
    $artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot $ArtifactDirectory))
    if (!$artifactRoot.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Artifacts must stay within this checkout.' }
    $publish = Join-Path $artifactRoot 'publish'
    if (Test-Path $publish) { throw 'Use a clean checkout or a new artifact directory; packaging refuses stale publish output.' }
    dotnet publish src/P7SUniversalViewer/P7SUniversalViewer.csproj -c Release -r win-x64 --self-contained true -o $publish -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE) { throw 'Publish failed.' }
    Get-ChildItem $publish -Filter *.xml -Recurse | Remove-Item
    Copy-Item LICENSE,README.md,THIRD_PARTY_NOTICES.md -Destination $publish
    $runtimeVersion = (Get-Content (Join-Path $publish "P7SUniversalViewer.runtimeconfig.json") -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks[0].version
    $coreRuntime = Join-Path $env:USERPROFILE ".nuget/packages/microsoft.netcore.app.runtime.win-x64/$runtimeVersion"
    Copy-Item (Join-Path $coreRuntime "LICENSE.TXT") (Join-Path $publish "DOTNET-LICENSE.txt")
    Copy-Item (Join-Path $coreRuntime "THIRD-PARTY-NOTICES.TXT") (Join-Path $publish "DOTNET-THIRD-PARTY-NOTICES.txt")
    $desktopRuntime = Join-Path $env:USERPROFILE ".nuget/packages/microsoft.windowsdesktop.app.runtime.win-x64/$runtimeVersion"
    Copy-Item (Join-Path $desktopRuntime "LICENSE") (Join-Path $publish "WPF-LICENSE.txt")
    $packageRoot = Join-Path $env:USERPROFILE '.nuget/packages'
    $webLicense = Join-Path $packageRoot 'microsoft.web.webview2/1.0.4258.31/LICENSE.txt'
    if (!(Test-Path $webLicense)) { throw 'WebView2 SDK license missing.' }
    Copy-Item $webLicense (Join-Path $publish 'WebView2-SDK-LICENSE.txt')
    Copy-Item (Join-Path $packageRoot 'microsoft.web.webview2/1.0.4258.31/NOTICE.txt') (Join-Path $publish 'WebView2-SDK-NOTICE.txt')
    $zip = Join-Path $artifactRoot "P7SUniversalViewer-v$Version-win-x64.zip"
    Compress-Archive -Path "$publish/*" -DestinationPath $zip
    $compiler = 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe'
    if (!(Test-Path $compiler)) { $compiler = (Get-Command ISCC.exe -ErrorAction Stop).Source }
    & $compiler "/DVersion=$Version" "/DPublishDirectory=$publish" "/O$artifactRoot" installer/P7SUniversalViewer.iss
    if ($LASTEXITCODE) { throw 'Installer build failed.' }
    $installer = Join-Path $artifactRoot "P7SViewer_Setup-v$Version.exe"
    foreach ($file in @($zip,$installer)) { $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(); [IO.File]::WriteAllText("$file.sha256", "$hash  $([IO.Path]::GetFileName($file))`n", [Text.UTF8Encoding]::new($false)) }
} finally { Pop-Location }
