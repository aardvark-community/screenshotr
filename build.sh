#!/bin/bash

dotnet tool restore
dotnet paket restore

dotnet build src/screenshotr.sln -c Release

dotnet publish -c Release -o publish/screenshotr src/screenshotr
