if OBJECT_ID('spMergeUserBasedOnEmail') is not null
 exec ('drop proc spMergeUserBasedOnEmail')
go 

create proc spMergeUserBasedOnEmail
	@email nvarchar(255) 
as 
--set nocount on 
begin tran 

declare @merge_to int

create table #merge (Id int)

insert #merge
select Id
from Users
where Email = @email 

if @@ROWCOUNT < 2 
begin
	rollback tran 
	return 
end

select @merge_to = min(Id) from #merge

create table #votes (
	SavedQueryId int, 
	VoteTypeId int,
	CreationDate datetime
)

-- votes are hard to merge 
insert #votes
select SavedQueryId, VoteTypeId, MIN(CreationDate) as CreationDate
from Votes where UserId in (select Id from #merge) 
group by SavedQueryId, VoteTypeId

delete from Votes where UserId in (select Id from #merge)

insert Votes (SavedQueryId,UserId,VoteTypeId,CreationDate)
select SavedQueryId, @merge_to, VoteTypeId, CreationDate
from #votes

delete from #merge where Id = @merge_to

delete from Users 
where Id in (select Id from #merge) 

update Queries
set CreatorId = @merge_to 
where CreatorId in (select Id from #merge) 

update QueryExecutions
set UserId = @merge_to 
where UserId in (select Id from #merge) 

update SavedQueries
set UserId = @merge_to 
where UserId in (select Id from #merge) 


update UserOpenId
set UserId = @merge_to 
where UserId in (select Id from #merge) 


commit tran
