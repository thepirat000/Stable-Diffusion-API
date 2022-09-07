param (
  [String]$rootDir='./',
  [String]$pattern='*.png'
)

$ErrorActionPreference = "Stop"

Set-Location -Path $rootDir

$files = Get-ChildItem ./ -include $pattern -recurse 

Write-Host "Will process $($files.Count) files"

for ($i=0; $i -lt $files.Count; $i++) {
    $f = $files[$i]
    
    $outfile = $f.BaseName + '.jpg'
    
    & ffmpeg -i $f.FullName -qscale:v 3 -loglevel error $outfile

    if (-not(Test-Path $outfile)) {
        Write-Error "Error processing $($f.FullName)"
        continue;
    }

    Remove-Item $f.FullName
}
