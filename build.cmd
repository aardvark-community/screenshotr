@echo off
SETLOCAL
PUSHD %~dp0

dotnet tool restore
dotnet paket restore

dotnet build src/screenshotr.sln

dotnet publish -c Release -o publish/screenshotr src/screenshotr
