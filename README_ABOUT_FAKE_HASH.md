### How to build locally

#### Option 1: With proxy (original Dockerfile)
Use this if you have a working HTTP proxy that can access `deb.debian.org` and `mediaarea.net`:
```bash
docker build --no-cache \
  --build-arg version="<actual_release>-fakehash.1" \
  --build-arg HTTP_PROXY="http://<local_ip_address>:1080" \
  --build-arg HTTPS_PROXY="http://<local_ip_address>:1080" \
  --build-arg http_proxy="http://<local_ip_address>:1080" \
  --build-arg https_proxy="http://<local_ip_address>:1080" \
  -t shoko-server:custom \
  .
```

With stable Web UI:
```bash
docker build --no-cache \
  --build-arg version="<actual_release>-fakehash.1" \
  --build-arg channel="stable" \
  --build-arg HTTP_PROXY="http://<local_ip_address>:1080" \
  --build-arg HTTPS_PROXY="http://<local_ip_address>:1080" \
  --build-arg http_proxy="http://<local_ip_address>:1080" \
  --build-arg https_proxy="http://<local_ip_address>:1080" \
  -t shoko-server:custom \
  .
```

#### Option 2: Without proxy (Dockerfile.noproxy)
Use this for building without a proxy. Uses Yandex mirror and Debian's mediainfo:
```bash
docker build --no-cache -f Dockerfile.noproxy \
  --build-arg version="<actual_release>-fakehash.1" \
  -t shoko-server:custom \
  .
```

With stable Web UI:
```bash
docker build --no-cache -f Dockerfile.noproxy \
  --build-arg version="<actual_release>-fakehash.1" \
  --build-arg channel="stable" \
  -t shoko-server:custom \
  .
```

#### Notes
> **Proxy instability:** If the build fails with `502 Bad Gateway` errors during `apt-get install`, 
> your proxy may be unstable. Restart the proxy server and try the build again.

> **Channel switching:** If switching between `channel="stable"` and `channel="dev"`, use `--no-cache` 
> to ensure Docker doesn't use cached layers with the wrong Web UI version.

> **Web UI cache:** The Web UI is cached in the config volume (`/home/shoko/.shoko/Shoko.CLI/webui/`).
> If you get version mismatch errors after rebuilding, use the "Force Update to Stable Web UI" button in the Shoko interface.

---

#### FakeHash (v3)
**Purpose:** Store pre-computed (or externally provided) hashes and metadata for a file so Shoko treats it as already hashed.

**Warning:** Supplying fake hashes will affect matching, duplicate detection, and external integrations (AniDB/MyList, hash-based lookups). Only use if you understand the implications.

**Example request:**
```http
POST /api/v3/FakeHash
Content-Type: application/json

{
  "importFolderId": 1,
  "filePath": "Anime/Show/episode01.mkv",
  "fileSize": 123456789,
  "hashes": {
    "ed2k": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
    "crc32": "DEADBEEF",
    "md5": "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
    "sha1": "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"
  },
  "hashSource": "DirectHash",
  "dateCreated": "2024-01-01T00:00:00Z",
  "dateUpdated": "2024-01-01T00:00:00Z",
  "dateImported": "2024-01-01T00:00:00Z"
}
```

```sh
curl -X POST "http://localhost:8111/api/v3/FakeHash" \
  -H "Content-Type: application/json" \
  -H "apikey: <api-key>" \
  -d '{
    "importFolderID": 1,
    "filePath": "Jujutsu Kaisen 2 - 8bit/Jujutsu Kaisen 2 - 23 [BDRip 1080p AVC 8bit FLAC].mkv",
    "fileSize": 1188281484,
    "hashes": {
      "ed2k": "FAFEFAFEFAFEFAFEFAFEFAFEFAFEFAFE",
      "crc32": "FAFEFAFE",
      "md5": "FAFEFAFEFAFEFAFEFAFEFAFEFAFEFAFE",
      "sha1": "FAFEFAFEFAFEFAFEFAFEFAFEFAFEFAFEFAFEFAFE"
    },
    "hashSource": "DirectHash",
    "dateCreated": "2026-01-28T17:58:00Z",
    "dateUpdated": "2026-01-28T17:58:00Z",
    "dateImported": "2026-01-28T17:58:00Z"
  }'
```
