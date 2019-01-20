echo off

docfx
copy "_Resources\icon.ico" "_site\favicon.ico"
copy "_Resources\logo.svg" "_site\logo.svg"
del /S /Q "api"

pause