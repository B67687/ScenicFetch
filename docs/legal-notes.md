# Legal Notes

ScenicFetch is a fetcher, not a media mirror.

## General

- The repository does not ship third-party wallpapers or videos.
- Downloaded media remains owned by its original rights holders.
- Users are responsible for complying with upstream terms and any jurisdiction-specific restrictions.

## Bing

- Bing homepage imagery belongs to Microsoft or the original rights holders.
- ScenicFetch only reads metadata from a public feed and downloads user-selected files on demand.

## Spotlight

- Windows Spotlight uses undocumented Microsoft endpoints.
- Use this source with the understanding that Microsoft may change, rate limit, or remove access without notice.

## Apple Aerial

- Apple Aerial content is fetched from Apple-hosted manifests and media URLs.
- ScenicFetch uses a narrowly scoped TLS validation bypass for `sylvan.apple.com` because the standard certificate chain does not validate in some environments.
- That bypass is limited to that host and should not be expanded casually.
