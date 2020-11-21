echo off

::You can change the runtime to win-x64 for Windows, linux-arm for a Raspberry Pi, etc.
set RUNTIME=linux-arm
dotnet publish PacManBot.csproj --runtime %RUNTIME% --configuration Release --no-self-contained
copy "bin\contents.json" "bin\Release\net5.0\%RUNTIME%\publish\contents.json"

del /Q "bin\Release\net5.0\%RUNTIME%\*"

pause