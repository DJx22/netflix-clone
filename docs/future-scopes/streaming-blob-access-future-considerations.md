# Streaming Service — Blob Access & Playback: Future Considerations

**Status:** Discussion only. Nothing in this document is scheduled or implemented. No ADR has been raised from this — it's a parking lot for a problem surfaced during Phase 0/1 work, to be revisited if/when a frontend or browser-facing playback path is actually built.

## Trigger

The Streaming API currently returns a URL pointing at the blob storage backend (Azurite, running in Docker). Hitting that URL directly in a browser returns `400 AuthorizationFailure` instead of playing the video. Streaming's job of "locate the media" works; the browser's ability to actually *fetch* the bytes at that URL does not, because Azurite's URL scheme isn't inherently browser-authorized the way a real signed Blob URL would be.

This project is API-only (see roadmap §0, §9) — there is no frontend service and none is planned before Phase 8/9 at the earliest. So this isn't a bug to fix now. It's a boundary problem worth naming before it's forgotten.

## The actual question

Not "how do we build a video player" — the browser's native `<video>` element already covers play/pause/seek/volume/fullscreen for free. The real question is:

> How does a client obtain media from Blob Storage that it is *authorized* to read?

That's an authorization/access-boundary problem, not a UI problem.

## Three possible shapes (increasing realism, increasing cost)

### 1. Direct blob URL (dev-only shortcut)
Streaming returns Azurite's raw URL; browser hits it directly.
- Simplest to prototype.
- Doesn't solve anything — it's the shape causing the current `400`. Any fix here lives at the Azurite/auth/URL-construction level, not in Streaming's design.
- Not representative of how this would work against real Azure Blob Storage.

### 2. Streaming returns a temporary, authorized URL
Streaming resolves access and hands back a short-lived URL; the browser then talks to Blob Storage directly for the actual bytes. Streaming is not in the data path for playback itself.
- Maps to a real SAS URL once/if real Azure Blob Storage is in play (Phase 9). The existing Streaming ADR already leaves this door open.
- Closest to how this would actually be done outside the learning-project context.

### 3. Streaming as a media proxy
Browser calls Streaming (`GET /playback/{id}/video`), Streaming fetches from Azurite and streams the bytes back.
- Browser never talks to Azurite directly; Streaming owns authorization *and* transport.
- Cost: Streaming now carries every video byte through itself — a meaningfully bigger responsibility than "locate and authorize." Avoid unless there's a concrete reason to take it on.

## Direction, if this is ever picked up

Keep the responsibility split narrow:

> Streaming decides whether the user can access the media and tells the client where/how to get it. Blob Storage delivers the media.

That's option 2. Option 3 should only be chosen deliberately, not defaulted into because option 1 is broken and option 3 "just works."

## Explicitly out of scope, now and near-term

- No frontend/player build. No `<video>` integration work.
- No fix for the current `400` beyond noting it here — this document does not propose a resolution.
- No SAS-URL implementation — there's no real Azure Blob Storage in this project yet (Azurite is a local emulator; see roadmap §9 scope guardrails).

## Open question to resolve later, not now

Whatever the eventual shape, the decision belongs in a proper ADR once there's an actual consumer of these URLs (Phase 8/9-adjacent) — not retrofitted onto the current Streaming ADR, which is about Redis as primary store, not blob access authorization.
