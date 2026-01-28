### How to build localy
```
docker build \
  --build-arg version="<actual_release-fakehash.1>" \
  --build-arg HTTP_PROXY="http://<local_ip_address>:1080" \
  --build-arg HTTPS_PROXY="http://<local_ip_address>:1080" \
  --build-arg http_proxy="http://<local_ip_address>:1080" \
  --build-arg https_proxy="http://<local_ip_address>:1080" \
  -t shoko-server:custom \
  .
```
or if you want to get stable version of ui:
```
docker build \
  --build-arg version="<actual_release-fakehash.1>" \
  --build-arg channel="stable" \
  --build-arg HTTP_PROXY="http://<local_ip_address>:1080" \
  --build-arg HTTPS_PROXY="http://<local_ip_address>:1080" \
  --build-arg http_proxy="http://<local_ip_address>:1080" \
  --build-arg https_proxy="http://<local_ip_address>:1080" \
  -t shoko-server:custom \
  .
```

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