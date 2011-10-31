IF OBJECT_ID('Revisions') IS NULL
BEGIN
	CREATE TABLE [dbo].[Revisions](
		[Id] [int] IDENTITY(1,1) NOT NULL,
		[QueryId] [int] NOT NULL,
		[RootId] [int] NULL,
		[ParentId] [int] NULL,
		[OwnerId] [int] NULL,
		[OwnerIP] [varchar](15) NOT NULL,
		[IsFeature] [bit] NOT NULL,
		[CreationDate] [datetime] NOT NULL,
	 CONSTRAINT [PK_Revisions] PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY]
END

ALTER TABLE [dbo].[Revisions] ADD  CONSTRAINT [DF_Revisions_IsFeature]  DEFAULT ((0)) FOR [IsFeature]
GO