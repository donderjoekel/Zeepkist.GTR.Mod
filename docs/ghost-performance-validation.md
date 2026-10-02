# Ghost performance and recovery validation

## Automated checks

Run from repository root:

```powershell
dotnet build Zeepkist.GTR.Mod.csproj -c Release
dotnet test Zeepkist.GTR.Mod.Tests/Zeepkist.GTR.Mod.Tests.csproj -c Release
dotnet test Zeepkist.GTR.Mod.Tests/Zeepkist.GTR.Mod.Tests.csproj -c Release --filter Category=Performance --logger "console;verbosity=detailed"
```

Tests link production readers, encoding, repository, HTTP authentication, queue, and recovery code.
Headless adapters replace Unity value operations, visuals, Steam identity, storage interfaces, and UniTask scheduling.
These tests do not validate native quaternion interpolation, wheel sampling, rendering, or Unity thread scheduling.

Coverage includes V1–V7 decoding, existing V4 delta repetition, V6/V7 raw steering storage, V7 protobuf fields,
finish timestamps, ragdoll transition encoding, snapshot ownership, separate compressors, FIFO publishing,
encoding/upload failures, upload cancellation, unchanged JSON after authentication refresh, and unchanged POST retry policy.
Existing search, seek, reverse playback, horn state, character state, batching, and load budget tests remain enabled.
Repository tests cover load deduplication before disk/decode, separate playback handles, shared decoded payloads,
and usable ghosts after blob/index write failures. Queue tests cover cancellation and capacity release.

WebSocket fixture uses real StrawberryShake 14.3.0 transport and session pool with legacy `graphql-ws` protocol.
First connection sends snapshot, then closes normally or aborts after 50 ms.
Recovery restores second snapshot without player input. Exactly two connections and reapplied headers are verified.
Retry controller tests cover backoff, jitter, obsolete callbacks, disposal, failure recovery, and healthy reset.

## Managed storage measurements

Recorded 2026-10-01, Windows x64, .NET 8.0.31, Release. Each ghost contains 256 V6 frames.
Baseline uses previous class frame layout and `List<Frame>` with exact initial capacity.
Current storage uses production readonly frame structs and exact arrays.
Allocation measurements include arrays, lists, and frame objects. They exclude Unity visuals and parsing temporaries.

- 1 ghost: class 26,712 bytes; struct 20,536 bytes.
- 100 ghosts: class 2,668,824 bytes; struct 2,051,224 bytes.
- 1,000 ghosts: class 26,688,024 bytes; struct 20,512,024 bytes.

Frame storage allocation falls approximately 23%. Data sampling allocates zero bytes in both variants.
Harness also emits sampling p50/p95/p99. Timing varies with JIT tiering and host load.
These results do not establish Mono CPU, retained session memory, GPU upload, or FPS improvements.

## Runtime boundaries

Shared creation dispatcher limits work to 16 operations or 2 ms per Unity update.
Up to 15 fixed loading workers fetch compressed sources before reserving creation capacity.
Source leases deduplicate disk/download and lazy decoding for concurrent consumers.
Dispatcher reserves one of four slots before decoding. This bounds prepared/in-progress decoded payloads to four.
Repository keeps download limit 15 and parse limit 5; creation backpressure can reduce active parsing below that maximum.
Cancellation drops obsolete queued closures and releases reserved slots when preparation unwinds.
Synchronous decompression/compression finishes before worker cancellation can release its buffers.

Recording uses 512-frame struct blocks; growth never copies existing frame payloads on Unity thread.
Publishing queues remain FIFO session memory queues, with one encoder and one uploader.
Slow uploads retain prepared JSON; encoding releases captured frame buffers after each run.
No sampling, format, fidelity, or record POST retry changes are enabled.

## Native verification required before release

No running Zeepkist process was available during implementation. Native checks below remain pending.

Use same level, ghost files, camera path, graphics settings, resolution, and run length for baseline and changed builds.
Record hardware, game version, runtime, ghost frame counts, and build commit with each capture.
Warm assets first. Capture at least three runs per case.

1. Profile 1, 100, and 1,000 ghosts with Unity Profiler and frame capture.
   Compare frame-time p50/p95/p99, main/worker CPU, GC allocations, retained memory, and matrix buffer uploads.
2. Repeat with names visible/hidden, full/bulk profiles, depth occlusion, tint groups, paused playback, and camera movement.
   Confirm each procedural batch uploads matrices once while preserving every mesh part and depth pass.
3. Record online runs during playback. Finish rapidly, change level, change cosmetics, and delay upload responses.
   Compare recording accuracy, exact finish capture, metadata, publish feedback, and queue memory after uploads drain.
4. Compare V1–V7 visuals at identical times. Exercise interpolation, seek, reverse playback, horn edges,
   grounded/slipping/disabled wheels, soap override, arms up, paraglider, and ragdoll transitions.
5. Remove all ghosts and change session. Confirm pooled visuals retain no playback/frame references.
   Confirm GPU buffers and event subscriptions release on quit, disconnect, and plugin destruction.
6. Keep record/leaderboard subscriptions open through server's 60-minute close.
   Confirm fresh snapshots resume without respawn, last snapshot remains during outage, and one subscription per stream survives.
   Also interrupt network, restore network, and change level/disconnect during backoff.

Do not publish an FPS gain claim until native profiling completes.
