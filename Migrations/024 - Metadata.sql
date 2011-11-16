IF OBJECT_ID('Metadata') IS NULL
BEGIN
	CREATE TABLE [dbo].[Metadata](
		[Id] [int] IDENTITY(1,1) NOT NULL,
		[RevisionId] [int] NOT NULL,
		[OwnerId] [int] NULL,
		[Title] [nvarchar](100) NULL,
		[Description] [nvarchar](1000) NULL,
		[LastQueryId] [int] NOT NULL,
		[LastActivity] [datetime] NOT NULL,
		[Votes] [int] NOT NULL,
		[Views] [int] NOT NULL,
		[Featured] [bit] NOT NULL,
		[Hidden] [bit] NOT NULL,
		[First] [bit] NOT NULL
	 CONSTRAINT [PK_Metadata] PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY]
END

ALTER TABLE [dbo].[Metadata] ADD  CONSTRAINT [DF_Metadata_Votes]  DEFAULT ((0)) FOR [Votes]
GO

ALTER TABLE [dbo].[Metadata] ADD  CONSTRAINT [DF_Metadata_Views]  DEFAULT ((0)) FOR [Views]
GO

ALTER TABLE [dbo].[Metadata] ADD  CONSTRAINT [DF_Metadata_Featured]  DEFAULT ((0)) FOR [Featured]
GO

ALTER TABLE [dbo].[Metadata] ADD  CONSTRAINT [DF_Metadata_Hidden]  DEFAULT ((0)) FOR [Hidden]
GO

ALTER TABLE [dbo].[Metadata] ADD  CONSTRAINT [DF_Metadata_First]  DEFAULT ((0)) FOR [First]
GO