if OBJECT_ID('SavedQueries') is null
begin 
	CREATE TABLE [dbo].[SavedQueries]
	(
		Id int identity primary key, 
		QueryId int not null,
		UserId int not null,
		SiteId int not null,
		Title nvarchar(100) not null, 
		[Description] nvarchar(1000),
		CreationDate datetime,
		LastEditDate datetime,
		FavoriteCount int not null default(0),
		IsFeatured bit, 
		IsSkipped bit,
		IsDeleted bit,
		IsFirst bit not null default(0)
	)
end
go 

if dbo.fnIndexExists('SavedQueries','userid_idx') = 0 
begin
  create index userid_idx on [dbo].[SavedQueries](UserId)
end
go 
if dbo.fnIndexExists('SavedQueries','queryid_idx') = 0 
begin
	create index queryid_idx on [dbo].[SavedQueries](QueryId)
end
