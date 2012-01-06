if dbo.fnColumnExists('Revisions', 'OriginalQuerySetId') = 0
begin
	alter table Revisions add OriginalQuerySetId int
	
	exec('
	update r 
	set OriginalQuerySetId = QuerySetId
	from Revisions r 
	join 
	(
	select RevisionId,  MIN(q.QuerySetId) QuerySetId
	from Revisions r
	join QuerySetRevisions q on q.RevisionId = r.Id
	group by RevisionId
	) X on X.RevisionId = r.Id')
end