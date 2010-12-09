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
	exec('use [' + @targetDB + '] 
	insert dbo.' + @targetTable + ' select * from  [' + @sourceDB + '].dbo.' + @sourceTable )
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

exec('select *, cast(SUBSTRING(master.sys.fn_varbintohexstr(HashBytes(''MD5'',ltrim(rtrim(cast(Email as varchar(100)))))),3,32) as varchar(32)) as EmailHash into [' + @targetDB + '].dbo.Users from [' + @sourceDB + '].dbo.vExportUsers')
exec('alter table [' + @targetDB + '].dbo.Users drop column Email')

exec spExportTable @sourceDB, @targetDB, 'vExportPosts', 'Posts'
exec spExportTable @sourceDB, @targetDB, 'vExportPostHistory', 'PostHistory'
exec spExportTable @sourceDB, @targetDB, 'vExportVotes', 'Votes'
exec spExportTable @sourceDB, @targetDB, 'vExportBadges', 'Badges'
exec spExportTable @sourceDB, @targetDB, 'vExportComments', 'Comments'

exec('create unique clustered index idxId on  [' + @targetDB + '].dbo.Users (Id)')

exec('create index ParentIdIdx on [' + @targetDB + '].dbo.Posts (ParentId)')

exec('create  index idxPostOwner
ON [' + @targetDB + '].dbo.[Posts] ([OwnerUserId],[CommunityOwnedDate])
INCLUDE ([Id],[ParentId])')

exec ('create index [EmailHashIdx] on [' + @targetDB + '].dbo.Users(EmailHash)')
 

exec (' use [' + @targetDB + '] 
select Id, Name as [TagName], [Count] 
into dbo.Tags
from [' + @sourceDB + '].dbo.Tags
where [Count] > 0

create unique clustered index idxId on dbo.Tags(Id)
create unique  index idxName on dbo.Tags(Name)

select distinct pt.PostId, t.Id as TagId 
into [dbo].[PostTags]
from [' + @sourceDB + '].dbo.Tags t
join [' + @sourceDB + '].dbo.PostTags pt on pt.Tag = t.Name')
 
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
')
