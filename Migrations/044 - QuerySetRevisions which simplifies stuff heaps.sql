if OBJECT_ID('QuerySetRevisions') is null
begin 
	create table QuerySetRevisions (Id int identity primary key, QuerySetId int not null, RevisionId int not null)
	
	create unique index idxQuerySetIdRevisionId on QuerySetRevisions(QuerySetId,RevisionId)
	
	-- port over the first and last revision, don't care about the rest 
	insert QuerySetRevisions(QuerySetId,RevisionId)
	select Id, InitialRevisionId
	from QuerySets
	
	insert QuerySetRevisions(QuerySetId,RevisionId)
	select qs.Id, qs.CurrentRevisionId
	from QuerySets qs
	left join QuerySetRevisions qr on qr.QuerySetId = qs.Id and qr.RevisionId = qs.CurrentRevisionId 
	where qr.Id is null
end
go
-- nuke confusing columns from from Revisions 
if dbo.fnColumnExists('Revisions', 'RootId') = 1
begin 
   alter table Revisions drop column RootId 
end

if dbo.fnColumnExists('Revisions', 'ParentId') = 1
begin 
   alter table Revisions drop column ParentId 
end

if dbo.fnColumnExists('Revisions', 'IsFeature') = 1
begin 
   alter table Revisions drop constraint DF_Revisions_IsFeature
   alter table Revisions drop column IsFeature 
end

-- kill the orphans 
delete r
from Revisions r 
left join QuerySetRevisions qr on qr.RevisionId = r.Id
where qr.Id is null 

-- 
if dbo.fnColumnExists('QuerySets', 'ForkedQuerySetId') = 0
begin
	alter table QuerySets add ForkedQuerySetId int
end