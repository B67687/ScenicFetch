# ScenicFetch

ScenicFetch is a small `.NET 10` CLI and library for fetching scenic wallpaper and screensaver media from three sources:

- `bing`
- `spotlight`
- `apple-aerial`

It is metadata-first: you can list items as JSON or text, then optionally download the media you want. Apple Aerial is treated as a video source in v1.

## Commands

```powershell
scenicfetch sources list
scenicfetch sources list --json

scenicfetch items list --source bing --limit 3 --json
scenicfetch items list --source spotlight --country US --orientation portrait
scenicfetch items list --source apple-aerial --catalog all --variant 1080-hevc --json

scenicfetch download --source bing --output .\downloads --latest
scenicfetch download --source spotlight --output .\downloads --country US --orientation landscape
scenicfetch download --source apple-aerial --output .\downloads --catalog macos26 --variant 4k-hevc
```

## Provider Flags

- Bing: `--resolution 1080x1920|768x1366|1366x768|1920x1080|UHD`
- Spotlight: `--country <ISO2>` and `--orientation portrait|landscape`
- Apple Aerial: `--catalog all|macos26|tvos16|tvos13` and `--variant 1080-hevc|4k-hevc|1080-h264`

## Build

```powershell
dotnet restore .\ScenicFetch.slnx
dotnet build .\ScenicFetch.slnx
dotnet test .\ScenicFetch.slnx
```

## Notes

- Bing is the most stable source here, but its public feed only exposes a small recent archive.
- Spotlight relies on an undocumented Microsoft endpoint.
- Apple Aerial relies on undocumented Apple-hosted manifests and uses a host-scoped TLS validation bypass for `sylvan.apple.com` only.
- ScenicFetch does not bundle third-party media inside the repository.

More detail lives in [`docs/sources.md`](docs/sources.md) and [`docs/legal-notes.md`](docs/legal-notes.md).
