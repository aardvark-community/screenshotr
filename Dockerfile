# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

#RUN dotnet dev-certs https 

COPY . ./
RUN sh ./build.sh

RUN dotnet publish -c Release -o publish/App src/Screenshotr.App

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS build

RUN sudo apt-get install -y ffmpeg libgdiplus

# copy cert from build image to have https
#COPY --from=build-env /root/.dotnet/corefx/cryptography/x509stores/my/* /root/.dotnet/corefx/cryptography/x509stores/my/

WORKDIR /app
COPY --from=build-env /app/publish/App  .
ENTRYPOINT ["dotnet", "Screenshotr.App.dll"]
