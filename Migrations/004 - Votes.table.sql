if OBJECT_ID('Votes') is null 
begin
	CREATE TABLE [dbo].[Votes]
	(
		Id int identity primary key, 
		SavedQueryId int not null, 
		UserId int not null,
		VoteTypeId int not null, 
		CreationDate datetime not null
	)
end
go 
if dbo.fnIndexExists('Votes','queryIdx') = 0 
begin
  create unique index queryIdx on Votes(SavedQueryId, UserId, VoteTypeId)
end
go

if dbo.fnIndexExists('Votes','queryIdx2') = 0 
begin
  create unique index queryIdx2 on Votes(SavedQueryId, VoteTypeId, UserId)
end

