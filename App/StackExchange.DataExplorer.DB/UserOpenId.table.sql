CREATE TABLE [dbo].UserOpenId
(
	Id int identity primary key, 
	UserId int, 
	OpenIdClaim nvarchar(450)
)

go

create unique index OpenIdClaimIdx on [dbo].UserOpenId(OpenIdClaim)