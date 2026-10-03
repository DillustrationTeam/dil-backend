# Local audit fixes (2026-10-03)

Commission creation now rejects missing/unknown packages and malformed rate cards instead of silently constructing a default-priced order. It requires a positive package price, positive named milestones in contiguous sequence starting at 1, and an exact milestone sum matching the configured price. Existing stored orders are untouched.

Marketplace comment writes require approved, nondeleted artwork with a nondeleted creator; invalid artwork IDs no longer reach the FK insert.

The legacy direct creator-review mutation is disabled because its payload cannot identify a completed commission. Reviews must use CommissionService's completed-order route and its ownership/duplicate checks. Historical CreatorReviews are retained for compatibility.

Workflow regression checks now use rate-card fixtures and the current file-upload interfaces, with real wallet logic and local storage/watermark doubles. The workspace audit report `../../project-audit-2026-10-03.md` lists evidence and remaining limitations.
