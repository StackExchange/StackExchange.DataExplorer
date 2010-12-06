if OBJECT_ID('QueryNameHistory') is null
begin
	CREATE TABLE [dbo].[QueryNameHistory]
	(
		Id int identity not null primary key,
		RevisionId int not null, 
		QueryId int NOT NULL, 
		UserId int,
		Name nvarchar(100), 
		[Description] nvarchar(1000)	
	)
end
