#!/bin/bash

dotnet tool restore
dotnet paket restore
dotnet build src/screenshotr.sln -c Release

git tag $1
git push --tags

dotnet paket pack bin --version $1

dotnet nuget push "bin/Screenshotr.Client.$1.nupkg" --skip-duplicate
