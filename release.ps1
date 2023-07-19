if ($PSScriptRoot -match '.+?\\bin\\?') {
    $dir = $PSScriptRoot + "\"
}
else {
    $dir = $PSScriptRoot + "\bin\"
}

$out = $dir + "out\"
$copy = $dir + "copy\"
$BIEdir = $dir + "BepInEx\"
$Coredir = $dir + "Core\"
$UMMdir = $dir + "UMM\"

$ver = (Get-Item ($Coredir + "\RuntimeUnityEditor.Core.dll")).VersionInfo.FileVersion.ToString()

New-Item -ItemType Directory -Force -Path ($out)  

Remove-Item -Force -Path ($copy) -Recurse -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path ($copy)
Copy-Item -Path ($BIEdir) -Destination ($copy) -Recurse -Force
Copy-Item -Path ($Coredir+"*") -Destination ($copy + "BepInEx\plugins\RuntimeUnityEditor") -Recurse -Force
Compress-Archive -Path ($copy + "BepInEx") -Force -CompressionLevel "Optimal" -DestinationPath ($out + "RuntimeUnityEditor_BepInEx5_v" + $ver + ".zip")

Remove-Item -Force -Path ($copy) -Recurse -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path ($copy)
Copy-Item -Path ($UMMdir+"*") -Destination ($copy) -Recurse -Force
Copy-Item -Path ($Coredir+"*") -Destination ($copy) -Recurse -Force
$info = Get-Content ($copy+"Info.json") -raw | ConvertFrom-Json
$info.Version = $ver
$info | ConvertTo-Json | Set-Content ($copy+"Info.json")
Compress-Archive -Path ($copy+"*") -Force -CompressionLevel "Optimal" -DestinationPath ($out + "RuntimeUnityEditor_UMM_v" + $ver + ".zip")

Remove-Item -Force -Path ($copy) -Recurse -ErrorAction SilentlyContinue