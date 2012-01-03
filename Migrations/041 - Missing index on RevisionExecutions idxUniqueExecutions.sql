if dbo.fnIndexExists('RevisionExecutions','idxUniqueExecutions') = 0
begin
	create unique index idxUniqueExecutions on RevisionExecutions(UserId, SiteId, RevisionId)
end