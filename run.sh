#!/bin/bash

dotnet tool restore
dotnet paket restore

dotnet publish -c Release -o publish/server src/Screenshotr.App

pushd publish/server
./Screenshotr.App "$@"
popd
