
set RUNTIME=linux-x64
dotnet publish PacManBot.csproj --runtime %RUNTIME% --configuration Release --self-contained
copy "bin\contents.bot" "bin\Release\netcoreapp2.0\%RUNTIME%\publish\contents.bot"
del /Q "bin\Release\netcoreapp2.0\%RUNTIME%\*"

::set RUNTIME=win-x86
::dotnet publish PacManBot.csproj --runtime %RUNTIME% --configuration Release --self-contained
::copy "bin\contents.bot" "bin\Release\netcoreapp2.0\%RUNTIME%\publish\contents.bot"
::del /Q "bin\Release\netcoreapp2.0\%RUNTIME%\*"

pause