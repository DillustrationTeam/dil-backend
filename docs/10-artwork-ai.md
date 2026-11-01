# Artwork AI: metadata, recommendations and synthetic-image detection

## Configuration

Secrets belong in User Secrets or environment variables. Never commit keys.

- `Gemini:ApiKey` / `Gemini__ApiKey`: shared with the existing Gemini integration.
- `ArtworkAi:VisionFallbackModel`: default `gemini-3.1-flash-lite`; fallback on unavailable model, quota or provider 5xx.
- `ArtworkAi:VisionModel`: default `gemini-3.5-flash-lite`, independently configurable from chat.
- `ArtworkAi:SightengineApiUser` and `ArtworkAi:SightengineApiSecret`: Sightengine credentials.
- `ArtworkAi:AllowedImageHosts`: exact HTTPS storage host allowlist, default res.cloudinary.com. CloudflareR2:PublicDomain is also recognized.

Example commands from the backend root (replace placeholders locally):

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY" --project src/ArtCommission.API
dotnet user-secrets set "ArtworkAi:SightengineApiUser" "YOUR_USER" --project src/ArtCommission.API
dotnet user-secrets set "ArtworkAi:SightengineApiSecret" "YOUR_SECRET" --project src/ArtCommission.API
```

Local development images are sent as multipart file uploads from the configured LocalStorage root. Paths must stay inside public/artworks. Other URLs are only sent to the detector when HTTPS and the configured host match. Files in private workroom storage are never read by this feature.

## API and behavior

- POST /api/v1/artworks/suggest-metadata: Creator-only multipart `file`, PNG/JPEG/WebP <=5MB and <=25MP. Gemini returns a validated title, description, <=10 tags and style. The original image is resized/re-encoded before sending. Suggestions never write artwork automatically; the creator explicitly applies them. Missing key/provider failure is an explicit error and manual entry remains available.
- GET /api/v1/me/artwork-recommendations: authenticated user identity, content-based ranking using tags from favorites/saved artworks and followed creators, with recent/popular approved artwork as cold-start fallback. No claimed learned/embedding model. Bounded relevant candidates and reasons accompany each result. Deleted, hidden, pending, own and already-liked/saved artworks are excluded.
- GET /api/v1/artworks/{id}/ai-scan: creator owner or Moderator/Administrator only. Missing configuration, queued, processing, retrying, failed and succeeded are distinct. A score is exposed only for a succeeded scan of the current image URL.
- POST /api/v1/admin/moderation/{id}/ai-scan: Moderator/Administrator only; queues a new scan of Pending/Flagged artwork with cooldown. New requests are rate-limited, as are metadata suggestions (6/minute/user).

BackgroundService polls durable state stored on Artworks. Claims use AiScanUpdatedAt optimistic concurrency; timed-out Processing leases recover after 5 minutes. Provider errors retry up to 3 attempts, with 2-minute backoff. Editing the image clears previous scores and restarts the retry budget. Late results are discarded if image URL/claim changes or artwork is deleted. URLs from upload storage are unique/version-bound; replacing bytes in-place on an existing URL is unsupported.

Sightengine runs the genai model. Its score is an advisory signal; it does not determine approval, IsAiGenerated or copyright. Moderator decisions remain explicit. Client-supplied AiDetectionScore is ignored. Manual flag_ai never fabricates a detector score. Content safety scores are separate and UI shows missing values instead of fake percentages.

## Database and testing

Migration 20261023125301_AddArtworkAiScanState adds scan state/provenance and changes score precision to 4 decimal places. Applied to local ArtCommissionDb during development.

```powershell
dotnet run --project tests/CommissionWorkflowChecks -- --artwork-ai
```

Coverage includes ranking visibility/ownership, missing provider config, malformed/out-of-range output, retry budget/cooldown, changed-image invalidation, late result discard, frontend score spoofing, and storage host restriction. Live Sightengine smoke test used its own public documentation sample; credentials were not written to source or test fixtures.

Primary API references:
- https://ai.google.dev/gemini-api/docs/image-understanding
- https://ai.google.dev/gemini-api/docs/generate-content/structured-output
- https://sightengine.com/docs/ai-generated-image-detection

Live verification on 2026-10-23: Sightengine genai returned success for its own public sample. Gemini 2.5 Flash rejected new-account access and 3.8 Flash was overloaded; the configured 3.5 Flash Lite completed successfully. The actual metadata endpoint returned HTTP 200 with validated tags/description; UI preview/apply populated the form without saving. A temporary QA artwork went through normal upload, durable background scan and Succeeded with an actual provider score; the QA record was soft-deleted afterward.

Regression fixes (2026-10-23): Local uploaded images retain the existing 25 MB limit. Images above 10 MB are re-encoded as a first-frame JPEG bounded to 2048 pixels for Sightengine only; the source file is unchanged. Gemini response reading/parsing shares the 60-second request budget. Provider timeouts and invalid envelope/model JSON attempt the fallback while budget remains, then return a controlled error; caller cancellation propagates. Tests cover malformed envelopes, invalid model JSON, timeout recovery, cancellation, 11/25 MB uploads and oversized rejection.
