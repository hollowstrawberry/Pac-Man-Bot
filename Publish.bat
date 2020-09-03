echo off

::You can change the runtime to win-x64 for Windows, linux-arm for a Raspberry Pi, etc.
set RUNTIME=linux-arm
dotnet publish PacManBot.csproj --runtime %RUNTIME% --configuration Release --self-contained
copy "bin\contents.json" "bin\Release\netcoreapp2.0\%RUNTIME%\publish\contents.json"

::del /Q "bin\Release\netcoreapp2.0\%RUNTIME%\*"

pause