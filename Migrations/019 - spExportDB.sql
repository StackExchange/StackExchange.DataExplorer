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

exec('select *, cast(SUBSTRING(master.sys.fn_varbintohexstr(HashBytes(''MD5'',ltrim(rtrim(cast(Email as varchar(100)))))),3,32) as varchar(32)) as EmailHash into [' + @targetDB + '].dbo.Users from [' + @sourceDB + '].dbo.vExportUsers')

exec('alter table [' + @targetDB + '].dbo.Users drop column Email')


exec('select * into [' + @targetDB + '].dbo.Posts from [' + @sourceDB + '].dbo.vExportPosts')
exec('select * into [' + @targetDB + '].dbo.PostHistory from [' + @sourceDB + '].dbo.vExportPostHistory')
exec('select * into [' + @targetDB + '].dbo.Votes from [' + @sourceDB + '].dbo.vExportVotes')
exec('select * into [' + @targetDB + '].dbo.Badges from [' + @sourceDB + '].dbo.vExportBadges')
exec('select * into [' + @targetDB + '].dbo.Comments from [' + @sourceDB + '].dbo.vExportComments')


exec('create unique clustered index idxId on  [' + @targetDB + '].dbo.Users (Id)')
exec('create unique clustered index idxId on  [' + @targetDB + '].dbo.Posts (Id)')
exec('create unique clustered index idxId on  [' + @targetDB + '].dbo.PostHistory (Id)')
exec('create unique clustered index idxId on  [' + @targetDB + '].dbo.Votes (Id)')
exec('create unique clustered index idxId on  [' + @targetDB + '].dbo.Badges (Id)')
exec('create unique clustered index idxId on  [' + @targetDB + '].dbo.Comments (Id)')


exec('create index ParentIdIdx on [' + @targetDB + '].dbo.Posts (ParentId)')

exec('create  index idxPostOwner
ON [' + @targetDB + '].dbo.[Posts] ([OwnerUserId],[CommunityOwnedDate])
INCLUDE ([Id],[ParentId])')

exec ('create index [EmailHashIdx] on [' + @targetDB + '].dbo.Users(EmailHash)')
 
exec('
 CREATE TABLE [' + @targetDB + '].[dbo].[PostTags] (
	PostId int, 
	TagId int
)

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
')
