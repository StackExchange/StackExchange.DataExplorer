if dbo.fnColumnExists('Queries', 'IsFeatured') = 1 
begin 
	alter table Queries drop column IsFeatured
end

if dbo.fnColumnExists('Queries', 'IsSkipped') = 1 
begin 
	alter table Queries drop column IsSkipped
end

