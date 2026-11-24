param([string]$ExpectedCommit,[string]$PackageDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$oldCache=$env:NUGET_PACKAGES
Push-Location $root
try {
    if(-not $ExpectedCommit){$ExpectedCommit=git rev-parse HEAD;if($LASTEXITCODE -ne 0){throw 'Cannot determine Git commit'}}
    if(-not $PackageDirectory){$PackageDirectory=Join-Path $root 'artifacts'}
    $feed=(Resolve-Path -LiteralPath $PackageDirectory).Path
    $package=Join-Path $feed 'FeatureShift.Tool.0.1.0.nupkg'
    $packageHash=(Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    $run=Join-Path $root ('artifacts/smoke-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $run -Force|Out-Null
    $zip=[IO.Compression.ZipFile]::OpenRead($package)
    try {
        $nuspec=@($zip.Entries|Where-Object {$_.FullName -notmatch '/' -and $_.FullName.EndsWith('.nuspec')})
        if($nuspec.Count -ne 1){throw 'Missing manifest'}
        $settings=[Xml.XmlReaderSettings]::new();$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null;$settings.MaxCharactersInDocument=1048576
        $stream=$nuspec[0].Open();$reader=[Xml.XmlReader]::Create($stream,$settings)
        try {$xml=[Xml.XmlDocument]::new();$xml.XmlResolver=$null;$xml.Load($reader)} finally {$reader.Dispose();$stream.Dispose()}
        $meta=$xml.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
        if($meta.SelectSingleNode('*[local-name()="id"]').InnerText -ne 'FeatureShift.Tool' -or $meta.SelectSingleNode('*[local-name()="version"]').InnerText -ne '0.1.0' -or $meta.SelectSingleNode('*[local-name()="repository"]').GetAttribute('commit') -ne $ExpectedCommit){throw 'Package identity or commit mismatch'}
        foreach($name in @('reference.jsonl','candidate.jsonl')){
            $entry=$zip.GetEntry('examples/'+$name);if(-not $entry -or $entry.Length -gt 1048576){throw 'Missing example'}
            $stream=$entry.Open();$target=[IO.File]::Open((Join-Path $run $name),[IO.FileMode]::CreateNew)
            try{$stream.CopyTo($target)}finally{$stream.Dispose();$target.Dispose()}
            if((Get-FileHash (Join-Path $run $name) -Algorithm SHA256).Hash -ne (Get-FileHash (Join-Path $PSScriptRoot $name) -Algorithm SHA256).Hash){throw 'Packaged example differs'}
        }
    } finally {$zip.Dispose()}
    $config=Join-Path $run 'NuGet.Config';$escaped=[Security.SecurityElement]::Escape($feed)
    [IO.File]::WriteAllText($config,('<configuration><packageSources><clear/><add key="local" value="'+$escaped+'"/></packageSources></configuration>'))
    $env:NUGET_PACKAGES=Join-Path $run 'packages';$toolDirectory=Join-Path $run 'tool'
    dotnet tool install FeatureShift.Tool --version 0.1.0 --tool-path $toolDirectory --configfile $config --no-cache
    if($LASTEXITCODE -ne 0){throw 'Local-only install failed'}
    $tool=Join-Path $toolDirectory 'feature-shift';if((& $tool --version) -ne '0.1.0'){throw 'Version mismatch'}
    $reference=Join-Path $run 'reference.jsonl';$candidate=Join-Path $run 'candidate.jsonl'
    $a=(Get-FileHash $reference -Algorithm SHA256).Hash.ToLowerInvariant();$b=(Get-FileHash $candidate -Algorithm SHA256).Hash.ToLowerInvariant()
    $report=& $tool --reference $reference --candidate $candidate|ConvertFrom-Json
    $x=$report.features|Where-Object name -eq 'x';$constant=$report.features|Where-Object name -eq 'constant'
    if($LASTEXITCODE -ne 0 -or $report.referenceRows -ne 2 -or $report.candidateRows -ne 2 -or $report.overlappingIds -ne 1 -or $report.referenceSha256 -ne $a -or $report.candidateSha256 -ne $b -or $x.reference.mean -ne 2 -or $x.reference.populationStdDev -ne 1 -or $x.candidate.mean -ne 4 -or $x.referenceStandardizedMeanDifference -ne 2 -or $x.candidateAboveReferenceMax -ne 1 -or $null -ne $constant.referenceStandardizedMeanDifference){throw 'Hand-computed comparison failed'}
    $invalid=Join-Path $run 'invalid.jsonl';[IO.File]::WriteAllText($invalid,'{"id":"bad","features":{"wrong":1}}')
    $bad=& $tool --reference $reference --candidate $invalid 2> (Join-Path $run 'invalid.stderr.txt')
    if($LASTEXITCODE -ne 2 -or $bad){throw 'Invalid schema did not exit 2 without JSON'}
    if((Get-FileHash $reference -Algorithm SHA256).Hash.ToLowerInvariant() -ne $a -or (Get-FileHash $candidate -Algorithm SHA256).Hash.ToLowerInvariant() -ne $b -or (Get-FileHash $package -Algorithm SHA256).Hash -ne $packageHash){throw 'Input or package changed'}
    $proof=@{commit=$ExpectedCommit;packageSha256=$packageHash.ToLowerInvariant();referenceSha256=$a;candidateSha256=$b;overlap=1;meanDifference=2;standardizedMeanDifference=2;outsideCount=1;constantScale='null';freshLocalFeedAndCache=$true;exits='0/2';evidenceDirectory=$run}
    $proof|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $run 'verification.json') -Encoding utf8
    Write-Output ('Installed smoke passed; evidence: '+$run)
    $global:LASTEXITCODE=0
} finally {$env:NUGET_PACKAGES=$oldCache;Pop-Location}
