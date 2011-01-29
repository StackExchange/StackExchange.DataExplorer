IF OBJECT_ID('CachedPlans') IS NULL
BEGIN
	CREATE TABLE [dbo].[CachedPlans](
		[Id] [int] IDENTITY(1,1) NOT NULL,
		[QueryHash] [uniqueidentifier] NULL,
		[SiteId] [int] NULL,
		[Plan] [nvarchar](max) NULL,
		[CreationDate] [datetime] NULL
	)

	CREATE CLUSTERED INDEX idx_plans ON CachedPlans([QueryHash], SiteId)
	CREATE UNIQUE INDEX idxIdUnique ON CachedPlans([Id])
END
