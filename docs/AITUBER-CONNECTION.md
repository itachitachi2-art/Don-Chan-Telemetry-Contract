# AITuber connection (transport v1)

The existing LIGHT schema v1 is unchanged. This adds a read-only HTTP feed on
`127.0.0.1:18732`; it never accepts gameplay commands or accesses Unity objects
from the network thread. Existing file outputs continue independently.

Build/install using `build-install.bat` while the game is closed. Start 7DTD with
EAC disabled as before. The default XML enables the feed. Change `HttpFeedPort`
and AITuber's `AITUBERKIT_TELEMETRY_PORT` together if needed. No URL ACL or admin
HTTP registration is required. Port binding failure disables only the feed and
is written to the game log; file telemetry continues.

AITuber's game commentary settings contain **7DTD MOD接続 → MODに接続**.
The AITuber server and game must run on the same PC. This implementation receives,
deduplicates and aggregates input; it does not yet generate telemetry commentary.
The image commentary path remains independently controlled.

## Wire protocol

`GET /v1/feed?sessionId=<32 lowercase hex or empty>&after=<safe integer>`

Response: `transportVersion=1`, `sessionId`, `nextSequence`, `reset`, `gap`,
`hasMore`, `snapshot` (latest LIGHT record or null), `events` (LIGHT events).
No AUDIT records are published.

- Initial request, changed session, or future cursor: `reset=true`, current
  snapshot and live cursor, no historical events. This is an explicit new baseline.
- Continuing consumer: only events newer than `after`. At most 256 events / 512 KiB
  per response plus one snapshot. Advance using `nextSequence`, **not** snapshot.sequence.
- Snapshot sequence may be ahead of a paginated event batch. Snapshot-only sequence
  gaps are normal. Do not infer lost events from non-consecutive event sequences.
- Buffer: at most 4096 events / 8 MiB; a single record is limited to 256 KiB.
  Evicted or oversized events cause `gap=true` for affected cursors. Loss cannot
  be recovered over HTTP; original file logs remain available for offline audit.
- Restart creates a new session. Stopping/restarting the AITuber connection starts
  at current state, rather than replaying the previous session.
- Endpoint accepts only 127.0.0.1 Host with configured port and no browser Origin
  or Sec-Fetch-Site header. AITuber proxies server-side; no CORS is enabled.
- Memory retention is bounded, not durable delivery. HTTP is not a delivery receipt
  for AI/speech. Future consumers must define their own material selection.

## Validation

`powershell -NoProfile -ExecutionPolicy Bypass -File tests/run-feed-tests.ps1`
compiles only JsonUtil + feed + transport and runs a real loopback HTTP test.
It needs Windows .NET Framework but no game DLLs. This does not verify full MOD
compilation, Unity runtime behavior, or commentary quality.
