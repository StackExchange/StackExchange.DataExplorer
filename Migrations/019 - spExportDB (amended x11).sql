if OBJECT_ID('spExportTable') is not null 
begin
	drop proc spExportTable
end
go 

create proc spExportTable
	@sourceDB nvarchar(100),
	@targetDB nvarchar(100),
	@sourceTable nvarchar(100), 
	@targetTable nvarchar(100) 
as

	exec('select top 0 Id as Id1, * into [' + @targetDB + '].dbo.' + @targetTable + ' from [' + @sourceDB + '].dbo.' + @sourceTable)
	exec('alter table [' + @targetDB + '].dbo.' + @targetTable + ' drop Column Id1')
	exec('create unique clustered index idxId on  [' + @targetDB + '].dbo.' + @targetTable + ' (Id)')
	
	declare @text varchar(4000)
	
	set @text = 
	'
		use [' + @targetDB + ']
		declare @keepGoing bit 
		declare @current int 
		set @keepGoing = 1 
		set @current = -1 
		while @keepGoing = 1 
		begin
			insert dbo.' + @targetTable + ' select top 50000 * from  [' + @sourceDB + '].dbo.' + @sourceTable + ' where Id > @current order by Id asc
			if @@rowcount = 0 
			begin
				set @keepGoing = 0
				break
			end
			select @current = max(Id) from ' + @targetTable + '
		end
		'
		exec(@text)
		
go

-- used to export a live SE db to a new database for DataExplorer Reasons
if OBJECT_ID('spExportDB') is not null
begin
	drop proc spExportDB
end

go

create proc spExportDB
	@sourceDB nvarchar(100),
	@targetDB nvarchar(100)
as 

if exists (select * from sys.databases where name = @targetDB) 
begin

	DECLARE @SQL varchar(max)
	SET @SQL = ''

	SELECT @SQL = @SQL + 'Kill ' + Convert(varchar, SPId) + ';'
	FROM MASTER..SysProcesses
	WHERE DBId = DB_ID(@targetDB) and status <> 'background'

	EXEC(@SQL)


	exec ('ALTER DATABASE [' + @targetDB +'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE')
	exec ('drop database [' + @targetDB +']')
end

exec ('create database [' + @targetDB +']')

-- set the size 
declare @name nvarchar(1000) 
declare @size int
declare @command nvarchar(1000) 

set @command = 'select @nameOut = name,@sizeOut = size from [' + @sourceDB +  '].sys.database_files where size = (select max(size) from [' + @sourceDB + '].sys.database_files)'
exec sp_executesql @command,N'@nameOut nvarchar(1000) out, @sizeOut int out', @nameOut = @name OUTPUT, @sizeOut = @size OUTPUT 

set @command = 'alter database [' + @targetDB + '] modify file  (NAME = '''+ @targetDB + ''', FILEGROWTH = 50%, Size = ' + CAST(@size / 400 as varchar(100)) + 'mb )'
exec(@command) 

exec ('ALTER DATABASE [' + @targetDB +'] SET RECOVERY SIMPLE')

RAISERROR( 'Exporting Users',0,1) WITH NOWAIT
exec('select *, cast(SUBSTRING(master.sys.fn_varbintohexstr(HashBytes(''MD5'',ltrim(rtrim(cast(Email as varchar(100)))))),3,32) as varchar(32)) as EmailHash into [' + @targetDB + '].dbo.Users from [' + @sourceDB + '].dbo.vExportUsers')
exec('alter table [' + @targetDB + '].dbo.Users drop column Email')
exec('alter table [' + @targetDB + '].dbo.Users add Age int null')
exec('update [' + @targetDB + '].dbo.Users set Age = DATEDIFF(yy, Birthday, getdate())')
exec('alter table [' + @targetDB + '].dbo.Users drop column Birthday')

RAISERROR( 'Exporting Posts',0,1) WITH NOWAIT
exec spExportTable @sourceDB, @targetDB, 'vExportPosts', 'Posts'

RAISERROR( 'Exporting History',0,1) WITH NOWAIT
exec spExportTable @sourceDB, @targetDB, 'vExportPostHistory', 'PostHistory'

RAISERROR( 'Exporting Votes',0,1) WITH NOWAIT
exec spExportTable @sourceDB, @targetDB, 'vExportVotes', 'Votes'

RAISERROR( 'Exporting Badges',0,1) WITH NOWAIT
exec spExportTable @sourceDB, @targetDB, 'vExportBadges', 'Badges'

RAISERROR( 'Exporting Comments',0,1) WITH NOWAIT
exec spExportTable @sourceDB, @targetDB, 'vExportComments', 'Comments'

RAISERROR( 'Indexing',0,1) WITH NOWAIT
exec('create unique clustered index idxId on  [' + @targetDB + '].dbo.Users (Id)')
exec('create index ParentIdIdx on [' + @targetDB + '].dbo.Posts (ParentId)')

exec('create  index idxPostOwner
ON [' + @targetDB + '].dbo.[Posts] ([OwnerUserId],[CommunityOwnedDate])
INCLUDE ([Id],[ParentId])')

exec ('create index [EmailHashIdx] on [' + @targetDB + '].dbo.Users(EmailHash)')
 
RAISERROR( 'Exporting Tags',0,1) WITH NOWAIT
exec (' use [' + @targetDB + '] 
select Id, Name as [TagName], [Count] 
into dbo.Tags
from [' + @sourceDB + '].dbo.Tags
where [Count] > 0

create unique clustered index idxId on dbo.Tags(Id)
create unique  index idxName on dbo.Tags(TagName)

select distinct pt.PostId, t.Id as TagId 
into [dbo].[PostTags]
from [' + @sourceDB + '].dbo.Tags t
join [' + @sourceDB + '].dbo.PostTags pt on pt.TagId = t.Id')

RAISERROR( 'Indexing',0,1) WITH NOWAIT
exec('

create unique clustered index PostTagsIndex on [' + @targetDB + '].dbo.PostTags (PostId,TagId)
create unique index PostTagsTagPostIndex on [' + @targetDB + '].dbo.PostTags (TagId, PostId)')

exec('create table [' + @targetDB + '].dbo.VoteTypes ( Id int primary key, Name varchar(40))

insert [' + @targetDB + '].dbo.VoteTypes 
select 
1, ''AcceptedByOriginator''
union all select
2, ''UpMod''
union all select
3, ''DownMod''
union all select
4, ''Offensive''
union all select
5, ''Favorite''
union all select
6, ''Close''
union all select
7, ''Reopen''
union all select
8, ''BountyStart''
union all select
9, ''BountyClose''
union all select
10, ''Deletion''
union all select
11, ''Undeletion''
union all select
12, ''Spam''
union all select
13, ''InformModerator''
union all select
15, ''ModeratorReview''
union all select
16, ''PendingEditRepBonus''
')

EXEC
('
	CREATE TABLE [' + @targetDB + '].dbo.PostTypes
	(
		Id int PRIMARY KEY,
		Name varchar(40)
	)

	INSERT INTO [' + @targetDB + '].dbo.PostTypes
		SELECT 1, ''Question''
		UNION ALL
		SELECT 2, ''Answer''
		UNION ALL
		SELECT 3, ''Tag Wiki''
')

EXEC
('
	CREATE TABLE [' + @targetDB + '].dbo.PostHistoryTypes
	(
		Id int PRIMARY KEY,
		Name varchar(40)
	)
	
	INSERT INTO [' + @targetDB + '].dbo.PostHistoryTypes
		SELECT 1, ''Initial Title''
		UNION ALL
		SELECT 2, ''Initial Body''
		UNION ALL
		SELECT 3, ''Initial Tags''
		UNION ALL
		SELECT 4, ''Edit Title''
		UNION ALL
		SELECT 5, ''Edit Body''
		UNION ALL
		SELECT 6, ''Edit Tags''
		UNION ALL
		SELECT 7, ''Rollback Title''
		UNION ALL
		SELECT 8, ''Rollback Body''
		UNION ALL
		SELECT 9, ''Rollback Tags''
		UNION ALL
		SELECT 10, ''Post Closed''
		UNION ALL
		SELECT 11, ''Post Reopened''
		UNION ALL
		SELECT 12, ''Post Deleted''
		UNION ALL
		SELECT 13, ''Post Undeleted''
		UNION ALL
		SELECT 14, ''Post Locked''
		UNION ALL
		SELECT 15, ''Post Unlocked''
		UNION ALL
		SELECT 16, ''Community Owned''
		UNION ALL
		SELECT 17, ''Post Migrated''
		UNION ALL
		SELECT 18, ''Question Merged''
		UNION ALL
		SELECT 19, ''Question Protected''
		UNION ALL
		SELECT 20, ''Question Unprotected''
		UNION ALL
		SELECT 21, ''Post Dissociated''
		UNION ALL
		SELECT 22, ''Question Unmerged''
')
