if dbo.fnColumnExists('Sites', 'ImageBackgroundColor') = 0
begin 
	alter table Sites  add ImageBackgroundColor varchar(6) null
end