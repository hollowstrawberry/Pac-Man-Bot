dotnet publish PacManBot_DotNetCore.csproj --runtime linux-arm --output ".\bin\linux-arm" --configuration Release --self-contained 
robocopy ".\bin\ " ".\bin\linux-arm\ " *.bot /IS
rmdir "bin\Release" /s /q
pause