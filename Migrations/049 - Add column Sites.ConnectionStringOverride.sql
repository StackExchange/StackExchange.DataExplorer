if dbo.fnColumnExists('Sites','ConnectionStringOverride') = 0
begin
    alter table Sites add ConnectionStringOverride nvarchar(200)
end
