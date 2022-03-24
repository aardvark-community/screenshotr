[![Publish](https://github.com/aardvark-community/screenshotr/actions/workflows/publish.yml/badge.svg)](https://github.com/aardvark-community/screenshotr/actions/workflows/publish.yml)
[![Windows](https://github.com/aardvark-community/screenshotr/actions/workflows/windows.yml/badge.svg)](https://github.com/aardvark-community/screenshotr/actions/workflows/windows.yml)
[![Linux](https://github.com/aardvark-community/screenshotr/actions/workflows/linux.yml/badge.svg)](https://github.com/aardvark-community/screenshotr/actions/workflows/linux.yml)
[![MacOS](https://github.com/aardvark-community/screenshotr/actions/workflows/mac.yml/badge.svg)](https://github.com/aardvark-community/screenshotr/actions/workflows/mac.yml)


# Quickstart

Use the appropriate version of `run` and `screenshotr` for your OS. 
- e.g. `./run.sh` and `./screenshotr.sh` for Linux
- or `run.cmd` and `screenshotr.cmd` for Windows

```bash
git clone https://github.com/stefanmaierhofer/screenshotr.git
cd screenshotr
run
```
Open https://localhost:5021/ in a your browser.

```bash
screenshotr import -e https://localhost:5021 --addRandomLabels ./directory/with/images
```

# Server

- `git clone https://github.com/stefanmaierhofer/screenshotr.git`
- `cd screenshotr`
- `run`

# Client

`nuget Screenshotr.Client`

```csharp
using Screenshotr;

var filename = "example.jpg";
var endpoint = "https://localhost:5021";

var client = await ScreenshotrHttpClient.Connect(endpoint);

await client.ImportScreenshot(
    buffer: File.ReadAllBytes(filename),
    tags: new[] { "foo", "bar" },
    timestamp: new FileInfo(filename).LastWriteTime
    );
```

# Command Line Tool

## Commands

`screenshotr <command> <args*>`

### import

```
screenshotr import -e <endpoint> [-t <tags>] <file|folder>* [-x <exclude>]
```

Example
```bash
screenshotr import -e "https://localhost:5020" -t "foo;bar;haha" "./images"
```

### list

`screenshotr list -e <endpoint> [--skip <int>] [--take <int>]`

### tail

`screenshotr tail -e <endpoint>`

## Configuration

Windows

```bash
set Screenshotr:Data=https://localhost:5020 
```

Linux

```bash
export Screenshotr:Data=https://localhost:5020
```

# Docker

## Configuration

By default, all screenshot data is stored inside the container and will disappear each time the 
container is restarted.

Edit `docker-compose.yaml` to configure a permanent data directory on the host machine:
```docker
...

volumes:
   - type: bind
     source: <SET PATH HERE>   # path on host machine ...
     target: /data             # ... maps to default path inside container

...
```

## Run server
```bash
docker compose up -d
```

## Build docker image
```bash
docker build -t screenshotr .
```
