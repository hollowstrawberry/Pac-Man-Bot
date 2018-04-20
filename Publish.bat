
rmdir /s /q "bin/Release"

set RUNTIME=win-x86
dotnet publish PacManBot.csproj --runtime %RUNTIME% --configuration Release
copy "bin\contents.bot" "bin/Release/netcoreapp2.0/%RUNTIME%/publish/contents.bot"

set RUNTIME=linux-x64
dotnet publish PacManBot.csproj --runtime %RUNTIME% --configuration Release
copy "bin\contents.bot" "bin/Release/netcoreapp2.0/%RUNTIME%/publish/contents.bot"

pause