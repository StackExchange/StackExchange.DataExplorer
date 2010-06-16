CREATE TABLE [dbo].[QueryExecutions]
(
	Id int identity, 
	QueryId int NOT NULL,
	UserId int NOT NULL, 
	SiteId int NOT NULL, 
	FirstRun datetime not null,  
	LastRun datetime not null, 
	ExecutionCount int not null
)
go
create unique clustered index idxUniqueQE on [dbo].[QueryExecutions] (UserId, QueryId)
go 
create unique index idxIdQE on [dbo].[QueryExecutions] (Id)