if dbo.fnColumnExists('QuerySets', 'CreationDate') = 0
begin
	alter table QuerySets add CreationDate datetime
	exec
	('update qs set CreationDate = r.CreationDate
	from QuerySets qs 
	join Revisions r on r.Id = qs.InitialRevisionId')
	
	ALTER TABLE QuerySets alter column CreationDate datetime not null
end