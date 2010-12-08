if OBJECT_ID('CachedResults') is null
begin

CREATE TABLE [dbo].[CachedResults](
	[QueryHash] [uniqueidentifier] NULL,
	[SiteId] [int] NULL,
	[Results] [nvarchar](max) NULL,
	[CreationDate] [datetime] NULL,
	[Id] [int] IDENTITY(1,1) NOT NULL
)

create clustered index idx_results on CachedResults([QueryHash], SiteId)

end


