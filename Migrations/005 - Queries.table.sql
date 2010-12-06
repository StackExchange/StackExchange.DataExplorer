if OBJECT_ID('Queries') is null 
begin
	CREATE TABLE [dbo].[Queries]
	(
		Id int identity primary key,
		QueryHash uniqueidentifier, 
		QueryBody nvarchar(max),
		CreatorId int,
		CreatorIP varchar(15),
		FirstRun datetime,
		[Views] int,
		Name nvarchar(100), 
		[Description] nvarchar(1000)
	)
end
go

if dbo.fnIndexExists('Queries','idx_qh') = 0 
begin
  create unique index idx_qh on [dbo].[Queries](QueryHash)
end



