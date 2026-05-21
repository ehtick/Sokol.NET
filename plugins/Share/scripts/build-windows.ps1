# Share plugin — Windows build script.
#
# No native library is needed on Windows.
# SharePlugin.cs uses Process.Start (ShellExecute) and does not call any P/Invoke
# functions on desktop platforms.
#
# Usage: .\plugins\Share\scripts\build-windows.ps1

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "sokol_share — Windows"                    -ForegroundColor Cyan
Write-Host "No native library needed on Windows."     -ForegroundColor Cyan
Write-Host "The Share plugin uses Process.Start for desktop platforms." -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
exit 0
