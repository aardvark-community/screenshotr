[![Publish](https://github.com/aardvark-community/screenshotr/actions/workflows/publish.yml/badge.svg)](https://github.com/aardvark-community/screenshotr/actions/workflows/publish.yml)
[![Windows](https://github.com/aardvark-community/screenshotr/actions/workflows/windows.yml/badge.svg)](https://github.com/aardvark-community/screenshotr/actions/workflows/windows.yml)
[![Linux](https://github.com/aardvark-community/screenshotr/actions/workflows/linux.yml/badge.svg)](https://github.com/aardvark-community/screenshotr/actions/workflows/linux.yml)
[![MacOS](https://github.com/aardvark-community/screenshotr/actions/workflows/mac.yml/badge.svg)](https://github.com/aardvark-community/screenshotr/actions/workflows/mac.yml)
# Server

- `git clone https://github.com/stefanmaierhofer/screenshotr.git`
- `cd screenshotr`
- `run`

# Client

`nuget Screenshotr.Client`

```csharp
using Screenshotr;

var client = await ScreenshotrHttpClient.Create(endpoint.AbsoluteUri);

var filename = "example.jpg";
var buffer = File.ReadAllBytes(filename);
var tags = new [] { "foo", "bar" };
var timestamp = new FileInfo(filename).LastWriteTime;
var foo = await client.ImportScreenshot(buffer, tags, timestamp);
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
