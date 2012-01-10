if object_id('Metadata') is not null
begin
	exec sp_rename 'Metadata', 'QuerySets'
end

go 

if dbo.fnColumnExists('QuerySets', 'RevisionId') = 1
begin 
	exec sp_rename 'QuerySets.RevisionId', 'InitialRevisionId' 
end

go

if dbo.fnColumnExists('QuerySets', 'LastQueryId') = 1
begin
	alter table QuerySets drop column LastQueryId
end

go

if dbo.fnColumnExists('QuerySets', 'First') = 1
begin 
	alter table QuerySets drop constraint DF_Metadata_First
	alter table QuerySets drop column [First]
end

go 

if dbo.fnColumnExists('QuerySets', 'CurrentRevisionId') = 0
begin
	alter table QuerySets add CurrentRevisionId int 
	exec ('update QuerySets set CurrentRevisionId = InitialRevisionId')
end

go 

declare @rowcount int = 1 

while @rowcount > 1 
begin
	delete QuerySets 
	where Id in 
	(
		select MAX(Id) from QuerySets 
		group by InitialRevisionId
		having COUNT(*) > 1
	)
	set @rowcount = @@ROWCOUNT
end

go

-- initial revision is now unique ... index it 
if dbo.fnIndexExists('QuerySets', 'QuerySets_InitialRevisionId_Unique') = 0
begin 
	create index QuerySets_InitialRevisionId_Unique on QuerySets(InitialRevisionId)
end 

go 

-- same for the current rev, QuerySets have unique revisions
if dbo.fnIndexExists('QuerySets', 'QuerySets_CurrentRevisionId_Unique') = 0
begin 
	create index QuerySets_CurrentRevisionId_Unique on QuerySets(CurrentRevisionId)
end 

go 

-- delete all "crossed" revisions keeping the root 
delete r from Revisions r
join QuerySets on InitialRevisionId = r.Id 
and RootId is not null

-- delete all "revisionless" querysets 
delete q
from QuerySets q
left join Revisions r on r.Id = q.InitialRevisionId 
where r.Id is null 

go 

-- more renaming cause that is how I roll 
if OBJECT_ID('QueryExecutions') is not null
begin 
	exec sp_rename 'QueryExecutions', 'RevisionExecutions'
end

go 

-- duplicate data is only serving to confuse matters
if dbo.fnColumnExists('RevisionExecutions', 'QueryId') = 1
begin
	drop index RevisionExecutions.idxUniqueQE
	alter table RevisionExecutions drop column QueryId  
end
