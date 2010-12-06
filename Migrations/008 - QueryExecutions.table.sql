if OBJECT_ID('QueryExecutions') is null
begin
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
end
go

if dbo.fnIndexExists('QueryExecutions','idxUniqueQE') = 0
begin 
	create unique clustered index idxUniqueQE on [dbo].[QueryExecutions] (UserId, QueryId)
end

go

if dbo.fnIndexExists('QueryExecutions','idxIdQE') = 0
begin 
	create unique index idxIdQE on [dbo].[QueryExecutions] (Id)
end