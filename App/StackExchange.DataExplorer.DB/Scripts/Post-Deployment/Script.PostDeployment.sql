if not exists(select * from Sites where Sites.Name = 'StackOverflow')
   insert into Sites(
	Id, 
	TinyName,Name,LongName,
	Url,ImageUrl,IconUrl,
	DatabaseName,
	Tagline,
	TagCss,
	ODataEndpoint) values
	(1, 
	'so', 'StackOverflow', 'Stack Overflow',
	'http://stackoverflow.com', 'http://sstatic.net/so/img/logo.png', 'http://sstatic.net/so/favicon.ico',
	'StackOverflow',
	'Q&A for programmers', 
	'.post-tag{
background-color:#E0EAF1;
border-bottom:1px solid #3E6D8E;
border-right:1px solid #7F9FB6;
color:#3E6D8E;
font-size:90%;
line-height:2.4;
margin:2px 2px 2px 0;
padding:3px 4px;
text-decoration:none;
white-space:nowrap;
}
.post-tag:hover {
background-color:#3E6D8E;
border-bottom:1px solid #37607D;
border-right:1px solid #37607D;
color:#E0EAF1;
text-decoration:none;}',
'https://odata.sqlazurelabs.com/OData.svc/v0.1/rp1uiewita/StackOverflow')
GO
if not exists(select * from Sites where Sites.Name = 'SuperUser')
  insert into Sites(
	Id, 
	TinyName,Name,LongName,
	Url,ImageUrl,IconUrl,
	DatabaseName,
	Tagline,
	TagCss,
	ODataEndpoint) values 
	(2, 
	'su','SuperUser', 'Super User', 
	'http://superuser.com', 'http://sstatic.net/su/img/logo.png', 'http://sstatic.net/su/favicon.ico',
	'SuperUser',
	'Q&A for computer enthusiasts and power users',
	'.post-tag {
-moz-border-radius:7px 7px 7px 7px;
background-color:#FFFFFF;
border:2px solid #14A7C6;
color:#1087A4;
font-size:90%;
line-height:2.4;
margin:2px 2px 2px 0;
padding:3px 5px;
text-decoration:none;
white-space:nowrap;
}
.post-tag:visited {
color:#1087A4;
}
.post-tag:hover {
background-color:#14A7C6;
border:2px solid #14A7C6;
color:#F3F1D9;
text-decoration:none;
}',
'https://odata.sqlazurelabs.com/OData.svc/v0.1/rp1uiewita/SuperUser'
)

GO
if not exists(select * from Sites where Sites.Name = 'ServerFault')
 insert into Sites(
	Id, 
	TinyName,Name,LongName,
	Url,ImageUrl,IconUrl,
	DatabaseName,
	Tagline,
	TagCss,
	ODataEndpoint) values 
	(3, 
	'sf', 'ServerFault', 'Server Fault',
	'http://serverfault.com', 'http://sstatic.net/sf/img/logo.png', 'http://sstatic.net/sf/favicon.ico',
	'ServerFault',
	'Q&A for system administrators and IT professionals',
	'.post-tag {
background-color:#F3F1D9;
border:1px solid #C5B849;
color:#444444;
font-size:90%;
line-height:2.4;
margin:2px 2px 2px 0;
padding:3px 4px;
text-decoration:none;
white-space:nowrap;
}
.post-tag:visited {
color:#444444;
}
.post-tag:hover {
background-color:#444444;
border:1px solid #444444;
color:#F3F1D9;
text-decoration:none
}',
'https://odata.sqlazurelabs.com/OData.svc/v0.1/rp1uiewita/ServerFault')

GO
if not exists(select * from Sites where Sites.Name = 'Meta')
  insert into Sites(
	Id, 
	TinyName,Name,LongName,
	Url,ImageUrl,IconUrl,
	DatabaseName,
	Tagline,
	TagCss,
	ODataEndpoint) values 
	(4, 
	'mso', 'Meta', 'Meta Stack Overflow',
	'http://meta.stackoverflow.com', 'http://sstatic.net/mso/img/logo.png','http://sstatic.net/mso/favicon.ico',
	'Meta',
	'Q&A about Stack Overflow, Server Fault and Super User',
	'.post-tag  {
background-color:#E7E7E7;
border-bottom:1px solid #626262;
border-right:1px solid #979797;
color:#6F6F6F;
font-size:90%;
line-height:2.4;
margin:2px 2px 2px 0;
padding:3px 4px;
text-decoration:none;
white-space:nowrap;
}
.post-tag:visited {
color:#6F6F6F;
}
.post-tag:hover {
background-color:#626262;
border-bottom:1px solid #565656;
border-right:1px solid #565656;
color:#E7E7E7;
text-decoration:none;}',
'https://odata.sqlazurelabs.com/OData.svc/v0.1/rp1uiewita/Meta'
	)

GO

if not exists(select * from Sites where Sites.Name = 'MetaSuperUser')
  insert into Sites(
	Id, 
	TinyName,Name,LongName,
	Url,ImageUrl,IconUrl,
	DatabaseName,
	Tagline,
	TagCss,
	ODataEndpoint) values 
	(5, 
	'msu', 'MetaSuperUser', 'Meta Super User',
	'http://meta.superuser.com', 'http://sstatic.net/superusermeta/img/logo.png','http://sstatic.net/superusermeta/img/favicon.ico',
	'Meta',
	'Q&A about Super User',
	'.post-tag  {
background-color:#E7E7E7;
border-bottom:1px solid #626262;
border-right:1px solid #979797;
color:#6F6F6F;
font-size:90%;
line-height:2.4;
margin:2px 2px 2px 0;
padding:3px 4px;
text-decoration:none;
white-space:nowrap;
}
.post-tag:visited {
color:#6F6F6F;
}
.post-tag:hover {
background-color:#626262;
border-bottom:1px solid #565656;
border-right:1px solid #565656;
color:#E7E7E7;
text-decoration:none;}',
''
	)
	
GO

if not exists(select * from Sites where Sites.Name = 'MetaServerFault')
  insert into Sites(
	Id, 
	TinyName,Name,LongName,
	Url,ImageUrl,IconUrl,
	DatabaseName,
	Tagline,
	TagCss,
	ODataEndpoint) values 
	(6, 
	'msf', 'MetaServerFault', 'Meta Server Fault',
	'http://meta.serverfault.com', 'http://sstatic.net/serverfaultmeta/img//logo.png','http://sstatic.net/serverfaultmeta/img/favicon.ico',
	'Meta',
	'Q&A about Server Fault',
	'.post-tag  {
background-color:#E7E7E7;
border-bottom:1px solid #626262;
border-right:1px solid #979797;
color:#6F6F6F;
font-size:90%;
line-height:2.4;
margin:2px 2px 2px 0;
padding:3px 4px;
text-decoration:none;
white-space:nowrap;
}
.post-tag:visited {
color:#6F6F6F;
}
.post-tag:hover {
background-color:#626262;
border-bottom:1px solid #565656;
border-right:1px solid #565656;
color:#E7E7E7;
text-decoration:none;}',
''
	)

	if not exists(select * from Sites where Sites.Name = 'WebApps')
   insert into Sites(
	Id, 
	TinyName,Name,LongName,
	Url,ImageUrl,IconUrl,
	DatabaseName,
	Tagline,
	TagCss,
	ODataEndpoint) values
	(7, 
	'webapps', 'WebApps', 'Web Apps',
	'http://webapps.stackexchange.com', 'http://sstatic.net/webapps/img/logo.png', 'http://sstatic.net/webapps/img/favicon.ico',
	'Web Apps',
	'Q&A for power users of web applications', 
	'.post-tag{
background-color:#E0EAF1;
border-bottom:1px solid #3E6D8E;
border-right:1px solid #7F9FB6;
color:#3E6D8E;
font-size:90%;
line-height:2.4;
margin:2px 2px 2px 0;
padding:3px 4px;
text-decoration:none;
white-space:nowrap;
}
.post-tag:hover {
background-color:#3E6D8E;
border-bottom:1px solid #37607D;
border-right:1px solid #37607D;
color:#E0EAF1;
text-decoration:none;}',
'')