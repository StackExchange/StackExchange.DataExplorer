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
go 

create index userid_idx on [dbo].[SavedQueries](UserId)

go 

create index queryid_idx on [dbo].[SavedQueries](QueryId)
