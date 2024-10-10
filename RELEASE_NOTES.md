- Updated System.Text.Json dependency (CVE-2024-43485)

### 1.5.3
- Updated packages
- Fixed missing assembly issue with Microsoft.Bcl.AsyncInterfaces

### 1.5.2
- removed package dependencies upper version limits

### 1.5.1
- fix usage message
- add "screenshotr tail [-v|--verbose]", which prints complete screenshot json (only screenhot id is printed without -v)
- lazy initialization of ScreenshotrHttpClient's websocket connection (in order to speed up first query)

### 1.5.0
- API: GetTags

### 1.4.0
- renamed type Screenshotr.V2i to Screenshotr.ImgSize to avoid name clashes

### 1.3.0
- add API keys
  - upload now requires an apikey with role `importer`
  - `screenshotr apikey *` commands now require an apikey with role `admin`
  - **important**: initial admin apikey is auto-generated on first startup and printed to console

### 1.2.0
- screenshotr cli: add --version, and --help
- move API endpoint to /api/1.0/...

### 1.1.0
- documentation and cleanup

### 1.0.0
- init
