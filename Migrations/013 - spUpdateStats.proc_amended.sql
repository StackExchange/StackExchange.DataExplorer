if OBJECT_ID('spUpdateStats') is not null
 exec ('drop proc spUpdateStats')
go 

create proc spUpdateStats
as 

declare sites_cursor cursor 
for select Id, DatabaseName from Sites

open sites_cursor 

declare @id int 
declare @database_name nvarchar(max)  

declare @local varchar(2000) 
select @local = DB_NAME()

fetch next from sites_cursor INTO @id, @database_name
While (@@FETCH_STATUS <> -1)
BEGIN
	IF (@@FETCH_STATUS <> -2)
	
	declare @id_string varchar(2000) 
	set @id_string =  cast(@id as varchar(2000))
	
	exec('use [' + @database_name + '] update ' + @local + '.dbo.Sites set TotalQuestions = (select count(*) from Posts where PostTypeId = 1) where Id = ' + @id_string)
	exec('use [' + @database_name + '] update ' + @local + '.dbo.Sites set TotalAnswers = (select count(*) from Posts where PostTypeId = 2) where Id = ' + @id_string)
	exec('use [' + @database_name + '] update ' + @local + '.dbo.Sites set TotalUsers = (select count(*) from Users) where Id = ' + @id_string)		
	exec('use [' + @database_name + '] update ' + @local + '.dbo.Sites set TotalComments = (select count(*) from PostComments) where Id = ' + @id_string)	
	exec('use [' + @database_name + '] update ' + @local + '.dbo.Sites set TotalTags = (select count(*) from Tags) where Id = ' + @id_string)	
	exec('use [' + @database_name + '] update ' + @local + '.dbo.Sites set LastPost = (select p.CreationDate from Posts p where p.Id = (select max(pp.Id) from Posts pp)) where Id = ' + @id_string)		
	fetch next from sites_cursor INTO @id, @database_name
END
close sites_cursor
deallocate sites_cursor 

delete from CachedResults

