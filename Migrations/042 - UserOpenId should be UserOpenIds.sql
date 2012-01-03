if OBJECT_ID('UserOpenId') is not null
begin
	exec sp_rename 'UserOpenId', 'UserOpenIds'
end
