# Commission review and delivery corrections

Final deliverable download is available to commission participants in Delivered and Completed states. This lets the client inspect the submitted file before accepting delivery. Existing participant authorization and 15-minute signed URL expiry remain enforced; other states cannot download final files.

The public creator review DTO includes the saved ReviewerReply for commission reviews. Legacy direct creator reviews return a null reply. No schema change is needed for these corrections.

Regression checks cover download before and after completion, rejection of an unrelated user, and the creator reply returned by the public review query.

Runtime verification found that the cancellation migration also changed the CriteriaScores-to-EventSubmissions foreign key to Cascade, introducing multiple SQL Server cascade paths. Keep this relationship Restrict in the model and migration metadata; the cancellation migration now only adds its two nullable commission columns. This prevents startup migration failure and commission-list SQL errors.

Authenticated `POST /api/v1/uploads/profile-avatar` accepts JPG/PNG/WebP up to 5MB, validates decoded format/dimensions (maximum 16 million pixels), normalizes orientation, crops to 400×400 and stores JPEG through IFileStorageService. It returns the public image URL; profile save remains a separate authenticated action. Avatar files are public profile media and the endpoint does not accept arbitrary folder paths. The static-file storage must persist across deployment instances for production use.
