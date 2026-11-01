# Creator asset library

GET /api/v1/creator/workstation/assets?assetType=Pose|Palette|Preset returns only the authenticated creator profile's non-deleted resources. POST creates; PUT /assets/{id} updates an owned non-deleted resource; DELETE soft-deletes. Existing workstation state still includes assets for compatibility. No database migration required.

Validation is centralized in CreatorAssetValidation: name 1–120 characters, known asset type, optional absolute HTTP(S) URL <=2048 without credentials, JSON object <=16384 characters, notes <=3000. Pose requires an image URL; preset requires notes or an image; palette requires 2–16 six-digit HEX values. Empty placeholder creation is no longer accepted. Existing incomplete records can still be read and repaired by editing. Update and deletion are scoped to the authenticated creator's profile; deleted resources cannot be updated.

Metadata JSON uses {"colors":["#112233","#FFAA66"],"notes":"..."} for palettes and {"notes":"..."} for pose/preset resources. Existing metadata remains readable. Frontend exports portable JSON and inserts a textual snapshot into the normal Workroom draft, requiring the normal Send action.

Verification: 73 CreatorFeatureChecks pass, including persistence, type filtering, malformed data, unsafe URLs, other-creator access and deleted-asset protection. API build succeeds without warnings/errors. Artwork AI (43) and commission workflow (154) checks remain passing.
