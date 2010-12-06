if OBJECT_ID('trSavedQuery') is not null
 exec ('drop trigger trSavedQuery')
go 

CREATE TRIGGER [trSavedQuery]
ON SavedQueries
FOR INSERT, UPDATE, DELETE 
AS 
BEGIN
	SET NOCOUNT ON
	IF UPDATE(QueryId) 
	BEGIN
	    
		UPDATE s set IsFirst = (
			SELECT CASE when ISNULL(MAX(s1.Id), -1) = s.Id then 1 else 0 end 
			from SavedQueries s1 
			where s1.QueryId = s.QueryId and isnull(s1.IsDeleted,0) = 0 
		)
		from SavedQueries s 
		/* its causing warnings for some odd reason
		   where  s.QueryId in  (
		   select d.QueryId from deleted d 
		) or 
		s.QueryId in (
		   select i.QueryId from inserted i) */
	END 

END
