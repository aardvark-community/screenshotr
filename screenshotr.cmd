@echo off
SETLOCAL
PUSHD %~dp0

if not exist .\publish\screenshotr\screenshotr.exe (
    call build.cmd
)

cd publish\screenshotr
screenshotr.exe %*

POPD