if dbo.fnIndexExists('RevisionExecutions','idxUniqueExecutions') = 0
begin
	
	declare @rowcount int = 1
	while @rowcount > 0 
	begin 
		delete RevisionExecutions where Id in (
			select max(Id) from RevisionExecutions 
			group by UserId, SiteId, RevisionId
			having COUNT(*) > 1
		)
		set @rowcount = @@ROWCOUNT
	end
	
	create unique index idxUniqueExecutions on RevisionExecutions(UserId, SiteId, RevisionId)
end


