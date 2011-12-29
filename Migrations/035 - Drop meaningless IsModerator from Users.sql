if dbo.fnColumnExists('Users','IsModerator') = 1
begin

	-- thanks http://stackoverflow.com/questions/314998/sql-server-2005-drop-column-with-constraints 
	declare @default sysname, @sql nvarchar(max)

	select @default = name 
	from sys.default_constraints 
	where parent_object_id = object_id('Users')
	AND type = 'D'
	AND parent_column_id = (
		select column_id 
		from sys.columns 
		where object_id = object_id('Users')
		and name = 'IsModerator'
		)

	set @sql = N'alter table Users drop constraint ' + @default
	exec sp_executesql @sql

	alter table Users drop column IsModerator
end
