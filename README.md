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
Open http://localhost:5020/ in a your browser.

```bash
screenshotr import -e http://localhost:5020 --addRandomLabels ./directory/with/images
```

# Server

- `git clone git@github.com:aardvark-community/screenshotr.git`
- `cd screenshotr`
- `run`

# Client

`nuget Screenshotr.Client`

```csharp
using Screenshotr;

var filename = "example.jpg";
var endpoint = "http://localhost:5020";

var client = await ScreenshotrHttpClient.Connect(endpoint);

await client.ImportScreenshot(
    buffer: File.ReadAllBytes(filename),
    tags: new[] { "foo", "bar" },
    timestamp: new FileInfo(filename).LastWriteTime
    );
```

# Command Line Tool

## Usage

```bash
Usage:

  screenshotr <command> <args*> [-e <endpoint> -k <apikey>]
    You can either specify endpoint (-e) and apikey (-k) each
    time you run the screenshotr command, or you can use the
    connect command, which will remember the values for all subsequent
    runs (until you disconnect).

  screenshotr --version
    Print version.

  screenshotr --help
    Print usage message.

  Commands:
    import [-t <tags>] <file|folder>* [-x <exclude>] [--addRandomLabels]
    list [--skip <int>] [--take <int>]
    tail
    apikeys
      create -d <description> [-r <role>]+ [--days <float>]
             Available roles are: admin, import
      delete <apikey>
      list
    connect -e <endpoint> -k <apikey>
    disconnect

  Examples:
    screenshotr connect -e "http://localhost" -k "7d10785f41e8..."
    screenshotr disconnect
    screenshotr import -t "mytag some-other-tag" img.jpg /data/pictures/
    screenshotr list --skip 10 --take 5
    screenshotr tail
    screenshotr apikeys create -d "alice's import key" -r "import"
    screenshotr apikeys delete "2442d075d2f3888..."
    screenshotr apikeys list
```



# Docker

## Build
A ready-to-run docker image can be built by running
```bash
docker build -t screenshotr .
```
in the project's root directory. 

## Run

```bash
docker run -p 5020:5020 screenshotr
```

By default, all data is stored inside the container and will disappear each time the 
container is restarted. In order to permanently store your data, you have to bind a directory on your host machine to `/data` inside the container, e.g.

```bash
docker run screenshotr -p 5020:5020 -v /my/permanent/storage:/data
```


# Docker Compose

## Configuration

Edit `docker-compose.yml` to configure a permanent data directory on the host machine:
```docker
...
volumes:
  - /my/permanent/storage:/data
...
```

There is also an example traefik configuration in `docker-compose.traefik-example.yml`.

## Run server
```bash
docker compose up -d
```


