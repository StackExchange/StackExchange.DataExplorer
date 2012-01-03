if dbo.fnIndexExists('RevisionExecutions', 'idxLastRun') = 0 
begin 
	create unique index idxLastRun on RevisionExecutions(LastRun, UserId, RevisionId, SiteId)
end