CREATE TABLE [dbo].[Votes]
(
	Id int identity primary key, 
	SavedQueryId int not null, 
	UserId int not null,
	VoteTypeId int not null, 
	CreationDate datetime not null
)
go 
create unique index queryIdx on Votes(SavedQueryId, UserId, VoteTypeId)
go
create unique index queryIdx2 on Votes(SavedQueryId, VoteTypeId, UserId)