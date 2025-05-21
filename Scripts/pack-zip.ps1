Remove-Item ..\QuickLook.Plugin.ExifTool.qlplugin -ErrorAction SilentlyContinue

$files = Get-ChildItem -Path ..\Build\Release\ -Exclude *.pdb,*.xml
Compress-Archive $files ..\QuickLook.Plugin.ExifTool.zip
Move-Item ..\QuickLook.Plugin.ExifTool.zip ..\QuickLook.Plugin.ExifTool.qlplugin