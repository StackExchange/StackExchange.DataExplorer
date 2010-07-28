CREATE TABLE [dbo].[OpenIdWhiteList]
(
    Id int identity primary key,
    OpenId nvarchar (450),
    Approved bit not null default(0),
	IpAddress varchar(20), 
	CreationDate datetime default(getdate()) 
)
go
create unique index idxOpenId on [dbo].[OpenIdWhiteList](OpenId)