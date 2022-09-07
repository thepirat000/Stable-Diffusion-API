# Resize and JPG recompress an image
#
# Execute like this: 
# powershell -File FixInputImage.ps1 "C:\cache\folder\initimagefilename.webp" "C:\cache\folder\input.jpg" 512

param (
  [Parameter(Mandatory=$true)][String]$inputfilepath,
  [Parameter(Mandatory=$true)][String]$outputfilepath,
  [int]$maxDimension=512
)

$ErrorActionPreference = "Stop"

$f = Get-Item $inputfilepath
   
$dimensions = (ffprobe -v error -select_streams v -show_entries stream=width,height -of csv=p=0:s=x $f.FullName).Split("x")
$width = [int]$dimensions[0]
$height = [int]$dimensions[1]

$outfile = $outputfilepath
Write-Host $outfile

if ($f.Length -lt 0.1MB) {
    Write-Host "Skipping $($f.Name)"
    Exit 0
}

if ($width -gt $height) {
    if ($width -gt $maxDimension) {
        Write-Host "Will resize by width $($f.Name)"
        & ffmpeg -i $f.FullName -qscale:v 3 -loglevel error -vf scale=$($maxDimension):-1 $outfile
    } else {
        Write-Host "No resize, compress  $($f.Name)"
        & ffmpeg -i $f.FullName -qscale:v 3 -loglevel error $outfile
    }
} else {
    if ($height -gt $maxDimension) {
        Write-Host "Will resize by height $($f.Name)"
        & ffmpeg -i $f.FullName -qscale:v 3 -loglevel error -vf scale=-1:$($maxDimension) $outfile
    } else {
        Write-Host "No resize, compress $($f.Name)"
        & ffmpeg -i $f.FullName -qscale:v 3 -loglevel error $outfile
    }
}

if (-not(Test-Path $outfile)) {
    Write-Error "Error processing $($f.FullName)"
    Exit 1
}