@echo off
SETLOCAL
PUSHD %~dp0

dotnet tool restore
dotnet paket restore

dotnet publish -c Release -o publish/server src/Screenshotr.App

cd publish/server
Screenshotr.App.exe %*

POPD
