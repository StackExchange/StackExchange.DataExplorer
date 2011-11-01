IF OBJECT_ID('CachedResults') IS NULL
BEGIN
	CREATE TABLE [dbo].[CachedResults](
		[Id] [int] IDENTITY(1,1) NOT NULL,
		[QueryHash] [uniqueidentifier] NOT NULL,
		[SiteId] [int] NOT NULL,
		[Results] [nvarchar](max) NOT NULL,
		[ExecutionPlan] [nvarchar](max) NULL,
		[Messages] [nvarchar](max) NULL,
		[Truncated] [bit] NOT NULL default(0),
		[CreationDate] [datetime] NOT NULL,
	 CONSTRAINT [PK_CachedResults] PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY]
END
ELSE
BEGIN
	if not exists (select 1 from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = 'CachedResults' and COLUMN_NAME = 'ExecutionPlan')
		ALTER TABLE [dbo].[CachedResults] ADD ExecutionPlan nvarchar(max) NULL
	if not exists (select 1 from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = 'CachedResults' and COLUMN_NAME = 'Truncated')
		ALTER TABLE [dbo].[CachedResults] ADD Truncated bit NOT NULL default(0)
	if not exists (select 1 from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = 'CachedResults' and COLUMN_NAME = 'Messages')
		ALTER TABLE [dbo].[CachedResults] ADD [Messages] nvarchar(max) NULL
END