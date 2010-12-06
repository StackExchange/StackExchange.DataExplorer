if OBJECT_ID('UserOpenId') is null
begin
	CREATE TABLE [dbo].UserOpenId
	(
		Id int identity primary key, 
		UserId int, 
		OpenIdClaim nvarchar(450)
	)
end

go

if dbo.fnIndexExists('UserOpenId','OpenIdClaimIdx') = 0 
begin
  create unique index OpenIdClaimIdx on [dbo].UserOpenId(OpenIdClaim)
end

