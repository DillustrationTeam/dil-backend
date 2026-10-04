# Local audit fixes (2026-10-03)

Commission creation now rejects missing/unknown packages and malformed rate cards instead of silently constructing a default-priced order. It requires a positive package price, positive named milestones in contiguous sequence starting at 1, and an exact milestone sum matching the configured price. Existing stored orders are untouched.

Marketplace comment writes require approved, nondeleted artwork with a nondeleted creator; invalid artwork IDs no longer reach the FK insert.

The legacy direct creator-review mutation is disabled because its payload cannot identify a completed commission. Reviews must use CommissionService's completed-order route and its ownership/duplicate checks. Historical CreatorReviews are retained for compatibility.

Workflow regression checks now use rate-card fixtures and the current file-upload interfaces, with real wallet logic and local storage/watermark doubles. The workspace audit report `../../project-audit-2026-10-03.md` lists evidence and remaining limitations.

## Reconciliation with develop (2026-10-03)

Keep strict package/milestone validation and legacy slug ID conversion alongside develop's commercial license pricing, vouchers, creator statistics, and public terms endpoint. CommissionWorkflowChecks retains the broad earlier regression suite; CommissionPricingChecks preserves develop's suite and adds commercial license checks. Creator ownership uses CreatorProfile IDs for orders and user IDs for authenticated actions.

The two branches independently introduced the same workstation tables. SyncLocalRuntimeSchema keeps its historical ID but becomes a no-op; AddCreatorWorkstationTables creates only missing tables/indexes. Down preserves these shared tables because either migration may have introduced their user data. The combined model matches the latest develop snapshot (no pending EF model changes). Validate both fresh migration sequences and databases with existing workstation tables before deployment.

Verified locally: full migrations passed on a newly created isolated SQL database (removed afterwards) and on DillustrationLocal with existing workstation tables. Backend build has zero errors/warnings; 38 workflow, 34 creator, 26 pricing/workflow, 10 SQL interaction, and 13 authenticated HTTP interaction checks passed. Develop's auction, license pricing, and voucher suites also passed; the optional auction SQL race check was skipped because no dedicated connection was supplied. EF still reports the pre-existing decimal precision and collection value-comparer warnings.

Frontend sends voucherCode when creating the request, matching the commission service contract. Voucher redemption occurs during escrow deposit; checkout displays the persisted discount. This avoids nested voucher forms and claiming a generic redemption changed a commission's price when it did not.
