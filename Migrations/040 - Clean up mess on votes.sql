if dbo.fnColumnExists('Votes', 'QuerySetId') = 0
begin
	alter table Votes add QuerySetId int 
end
go

if dbo.fnColumnExists('Votes', 'RootId') = 1
begin
	
	exec('
	update v 
	set v.QuerySetId = q.Id 
	from Votes v 
	join QuerySets q on q.InitialRevisionId = v.RootId')

	delete Votes where QuerySetId is null
	
	declare @default sysname, @sql nvarchar(max)

	select @default = name 
	from sys.default_constraints 
	where parent_object_id = object_id('Votes')
	AND type = 'D'
	AND parent_column_id = (
		select column_id 
		from sys.columns 
		where object_id = object_id('Votes')
		and name = 'RootId'
		)

	set @sql = N'alter table Votes drop constraint ' + @default
	exec sp_executesql @sql

	if dbo.fnIndexExists('Votes', 'queryIdx') = 1 
	begin 
		drop index Votes.queryIdx
	end
	alter table Votes drop column RootId  
end

go

if dbo.fnColumnExists('Votes', 'OwnerId') = 1
begin

	declare @default sysname, @sql nvarchar(max)

	select @default = name 
	from sys.default_constraints 
	where parent_object_id = object_id('Votes')
	AND type = 'D'
	AND parent_column_id = (
		select column_id 
		from sys.columns 
		where object_id = object_id('Votes')
		and name = 'OwnerId'
		)

	set @sql = N'alter table Votes drop constraint ' + @default
	exec sp_executesql @sql


	alter table Votes drop column OwnerId  
end
