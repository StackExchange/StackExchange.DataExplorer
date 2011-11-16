IF OBJECT_ID('QueryMap') IS NULL
BEGIN
    -- Create a mapping table for backwards-compatibility
    CREATE TABLE [dbo].[QueryMap]
    (
		Id int NOT NULL IDENTITY PRIMARY KEY,
		OriginalId int NOT NULL,
		SiteId int NULL,
		MigrationType int NOT NULL,
		RevisionId int NOT NULL,
    )
    
    CREATE UNIQUE INDEX lookupIdx ON QueryMap(OriginalId, MigrationType);
    
    -- Define the variables that map to data we care about
    DECLARE @queryId int;
    DECLARE @siteId int;
    DECLARE @creatorId int;
    DECLARE @creatorIP varchar(15);
    DECLARE @activity datetime;
    DECLARE @execution datetime;
    DECLARE @votes int;
    DECLARE @views int;
    DECLARE @featured bit;
    DECLARE @name nvarchar(100);
    DECLARE @description nvarchar(1000);
    DECLARE @revisionId int;
    DECLARE @rootId int;
    DECLARE @ownerId int;
    DECLARE @savedId int;
    DECLARE @first bit;
    
    -- Create a cursor to port over the raw query data
    DECLARE QueryCursor CURSOR FOR
		SELECT Id, CreatorId, CreatorIP, FirstRun, [Views], Name, [Description] FROM [dbo].[Queries] ORDER BY FirstRun;
		
	OPEN QueryCursor;
	
	FETCH NEXT FROM QueryCursor INTO @queryId, @creatorId, @creatorIP, @activity, @views, @name, @description;
	
	WHILE @@FETCH_STATUS = 0
	BEGIN
	    -- Create a revision
		INSERT INTO Revisions (QueryId, RootId, ParentId, OwnerId, OwnerIP, CreationDate) VALUES (
			@queryId,
			NULL,
			NULL,
			@creatorId,
			@creatorIP,
			@activity
		);
		
		SELECT @revisionId = SCOPE_IDENTITY();
		
		IF @creatorId IS NOT NULL
		BEGIN
			SELECT @execution = (SELECT LastRun FROM QueryExecutions WHERE QueryId = @queryId AND UserId = @creatorId);
			
			IF @execution IS NOT NULL
				SELECT @activity = @execution;
		END
		
		-- Create the revision's metadata
		INSERT INTO Metadata (RevisionId, OwnerId, Title, [Description], LastQueryId, LastActivity, Votes, [Views], Featured, Hidden) VALUES (
			@revisionId,
			@creatorId,
			@name,
			@description,
			@queryId,
			@activity,
			0,
			@views,
			0,
			1
		);
		
		-- Create a mapping to the new revision
		INSERT INTO QueryMap (OriginalId, SiteId, MigrationType, RevisionId) VALUES (
			@queryId,
			NULL,
			1,
			@revisionId
		);
		
		-- Update the execution history
		UPDATE QueryExecutions SET RevisionId = @revisionId WHERE QueryId = @queryId;
		
		FETCH NEXT FROM QueryCursor INTO @queryId, @creatorId, @creatorIP, @activity, @views, @name, @description;
	END
	
	CLOSE QueryCursor; DEALLOCATE QueryCursor;
	
	-- Create a cursor to port over the saved query data
	DECLARE SavedQueryCursor CURSOR FOR
		SELECT Id, QueryId, UserId, SiteId, Title, [Description], FavoriteCount, IsFeatured, IsFirst, LastEditDate FROM SavedQueries WHERE (IsDeleted != 1 OR IsDeleted IS NULL) AND (IsSkipped != 1 OR IsSkipped IS NULL);
		
	OPEN SavedQueryCursor;
	
	FETCH NEXT FROM SavedQueryCursor INTO @savedId, @queryId, @creatorId, @siteId, @name, @description, @votes, @featured, @first, @activity;
	
	WHILE @@FETCH_STATUS = 0
	BEGIN
		SELECT @revisionId = Revisions.Id, @ownerId = OwnerId, @views = [Views] FROM Revisions JOIN Queries ON Revisions.QueryId = Queries.Id WHERE QueryId = @queryId;
		SELECT @rootId = @revisionId;
		
		IF @ownerId = @creatorId
		BEGIN
		    -- We trust the saved query's metadata more than the original query
			UPDATE Metadata SET
				Title = @name,
				[Description] = @description,
				Votes = ISNULL(@votes, 0),
				Featured = ISNULL(@featured, 0),
				Hidden = 0,
				[First] = @first
			WHERE RevisionId = @revisionId AND OwnerId = @ownerId;
		END
		ELSE
		BEGIN
			-- Create a revision
			INSERT INTO Revisions (QueryId, RootId, ParentId, OwnerId, OwnerIP, CreationDate) VALUES (
				@queryId,
				@revisionId,
				@revisionId,
				@creatorId,
				'', -- We don't know what IP they used
				@activity
			);
			
			SELECT @revisionId = SCOPE_IDENTITY();
		
			-- Create the revision's metadata
			INSERT INTO Metadata (RevisionId, OwnerId, Title, [Description], LastQueryId, LastActivity, Votes, [Views], Featured, [First]) VALUES (
				@revisionId,
				@creatorId,
				@name,
				@description,
				@queryId,
				@activity,
				ISNULL(@votes, 0),
				@views,
				ISNULL(@featured, 0),
				ISNULL(@first, 0)
			);
			
			-- Update the execution history
			UPDATE QueryExecutions SET RevisionId = @revisionId WHERE QueryId = @queryId AND UserId = @creatorId;
		END
		
		-- Update any existing votes
		UPDATE Votes SET RootId = @rootId, OwnerId = @creatorId WHERE SavedQueryId = @savedId;
		
		-- Create a mapping to the new revision
		INSERT INTO QueryMap (OriginalId, SiteId, MigrationType, RevisionId) VALUES (
			@savedId,
			@siteId,
			2,
			@revisionId
		);
	
		FETCH NEXT FROM SavedQueryCursor INTO @savedId, @queryId, @creatorId, @siteId, @name, @description, @votes, @featured, @first, @activity;
	END
	
	CLOSE SavedQueryCursor; DEALLOCATE SavedQueryCursor;
	
	-- Nuke duplicate saved votes
	DELETE FROM
		Votes
	FROM
		(SELECT MAX(Id) Id, RootId, OwnerId, UserId FROM Votes GROUP BY RootId, OwnerId, UserId) Duplicates
	JOIN
		Votes
	ON
		Votes.RootId = Duplicates.RootId AND
		Votes.UserId = Duplicates.UserId AND
		(Votes.OwnerId = Duplicates.OwnerId OR (Votes.OwnerId IS NULL AND Duplicates.OwnerId IS NULL)) AND
		Votes.Id != Duplicates.Id
		
	-- Update the vote totals
	UPDATE
		Metadata
	SET
		Votes = votes.Total
	FROM
		Metadata
	JOIN
		(SELECT COUNT(*) AS Total, RootId, OwnerId FROM Votes GROUP BY RootId, OwnerId) AS votes
	ON
		Metadata.RevisionId = votes.RootId AND Metadata.OwnerId = votes.OwnerId;
END