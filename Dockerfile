# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

COPY . ./
RUN sh ./build.sh

RUN dotnet publish -c Release -o publish/App src/Screenshotr.App

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS build

RUN sed -i 's/deb.debian.org/mirrors.ustc.edu.cn/g' /etc/apt/sources.list \
    && apt-get update \
    && apt-get install -y ffmpeg libgdiplus \
    && apt-get clean && apt-get autoclean && apt-get autoremove \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build-env /app/publish/App  .
ENTRYPOINT ["dotnet", "Screenshotr.App.dll"]
