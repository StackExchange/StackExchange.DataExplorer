CREATE TABLE [dbo].[CachedResults]
(
	QueryHash uniqueidentifier, 
	SiteId int,
	Results nvarchar(max), 
	CreationDate datetime
)

go 

create unique clustered index idx_results on [dbo].[CachedResults](QueryHash, SiteId)
