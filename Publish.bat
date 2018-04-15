echo off

set RUNTIME=win-x86

dotnet publish PacManBot.csproj --runtime %RUNTIME% --configuration Release
move /Y "bin/netcoreapp2.0/%RUNTIME%/publish" "bin/"
rd /s /q "bin/%RUNTIME%"
move /Y "bin/publish" "bin/%RUNTIME%"
robocopy "bin" "bin/%RUNTIME%" *.bot /IS /XF config.bot

set RUNTIME=linux-x64

dotnet publish PacManBot.csproj --runtime %RUNTIME% --configuration Release
move /Y "bin/netcoreapp2.0/%RUNTIME%/publish" "bin/"
rd /s /q "bin/%RUNTIME%"
move /Y "bin/publish" "bin/%RUNTIME%"
robocopy "bin" "bin/%RUNTIME%" *.bot /IS /XF config.bot

rd /s /q "bin/netcoreapp2.0"

pause