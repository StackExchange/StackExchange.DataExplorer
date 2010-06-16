CREATE TABLE [dbo].[Users]
(
	Id int identity,
	Login nvarchar(40) primary key, 
	Email nvarchar(255), 
	LastLogin datetime,
	IsAdmin bit default(0) not null, 
	IPAddress varchar(15),
	IsModerator bit default(0) not null,
	CreationDate datetime default(GetDate()),
	AboutMe nvarchar(max), 
	Website varchar(255),
	Location nvarchar(255),
	DOB datetime, 
	LastActivityDate datetime,
	LastSeenDate datetime
)
go
create unique index IdIdx on [dbo].[Users](Id)
