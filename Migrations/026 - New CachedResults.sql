IF OBJECT_ID('CachedResults') IS NULL
BEGIN
	CREATE TABLE [dbo].[CachedResults](
		[Id] [int] IDENTITY(1,1) NOT NULL,
		[QueryHash] [uniqueidentifier] NOT NULL,
		[SiteId] [int] NOT NULL,
		[Results] [nvarchar](max) NOT NULL,
		[ExecutionPlan] [nvarchar](max) NULL,
		[Messages] [nvarchar](max) NULL,
		[Truncated] [bit] NOT NULL,
		[CreationDate] [datetime] NOT NULL,
	 CONSTRAINT [PK_CachedResults] PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY]
END
ELSE
BEGIN
	ALTER TABLE [dbo].[CachedResults] ADD ExecutionPlan nvarchar(max) NULL
	ALTER TABLE [dbo].[CachedResults] ADD Truncated bit NOT NULL
	ALTER TABLE [dbo].[CachedResults] ADD [Messages] nvarchar(max) NULL
END

ALTER TABLE [dbo].[CachedResults] ADD  CONSTRAINT [DF_CachedResults_Truncated]  DEFAULT ((0)) FOR [Truncated]