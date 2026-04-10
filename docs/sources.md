# Sources

## Bing

- Endpoint: `https://www.bing.com/HPImageArchive.aspx?format=js&n=7`
- Type: image
- Notes:
  - ScenicFetch reads `urlbase` from the JSON response and constructs final image URLs from that base.
  - The feed is intentionally small and only exposes recent images.

## Spotlight

- Endpoint: `https://fd.api.iris.microsoft.com/v4/api/selection`
- Type: image
- Notes:
  - ScenicFetch uses placement `88000820`.
  - The response contains nested JSON in `batchrsp.items[].item`, which gets decoded into portrait and landscape asset URLs.
  - This source is undocumented and may change without notice.

## Apple Aerial

- Sources:
  - `https://sylvan.apple.com/itunes-assets/Aerials126/v4/82/2e/34/822e344c-f5d2-878c-3d56-508d5b09ed61/resources-26-0-1.tar`
  - `https://sylvan.apple.com/Aerials/resources-16.tar`
  - `https://sylvan.apple.com/Aerials/resources-13.tar`
- Type: video
- Notes:
  - ScenicFetch extracts `entries.json` from Apple TAR archives and maps available variants like `1080-hevc`, `4k-hevc`, and `1080-h264`.
  - Duplicate assets across manifests are merged by ID, shot ID, or overlapping media file names.
  - Apple manifests are undocumented and their URL patterns may change.
