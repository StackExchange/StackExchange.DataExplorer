Quick Guide to Data Explorer: 

- Getting Started

Pre-reqs: 

- For JavaScript hacking only: A web browser

- For back end changes:
- Visual Studio 2010
- SQL Server Express or above
- IIS7 and Url Rewrite 2.0 are required for odata endpoint support: http://www.iis.net/download/urlrewrite, if you do not require that support you can safely comment out the rewrite rule from web.config 
- If you want to compile release make sure "java" is in your path, java is used to bundle the js files. 


The File layout

- If you would like to hack on the grid or the SQL editor (change syntax highlighting, experiment with new JavaScript features) checkout the Static directory. 
   The Static directory contains a single page with the grid and editor. It references the main js and css files in the project. 


- The Lib directory contains all the 3rd party libraries we use
- The Tools directory contains exes that help in the build process 
- The App directory contains the actual SEDE app
- The Data directory contains a blank schema for a "Stack" site and the schema for data explorer.


Configuring the databases:

- Run migrate.local.bat in the migration directory, then import some data about the sites from the Data/sites.sql script. It assumes the DB is called "DataExplorer", if you name it differently or have a named instance, you will have to edit the batch file. 
- Import data into the Stack Overflow database (and any other databases you wish to query) alternatively if a DB exists you can simple run queries that do not depend on data like "SELECT 1"  


- Contributing Patches

1. Install a mercurial client such as tortoiseHG: http://tortoisehg.bitbucket.org/
2. Create a clone of the current trunk in google code on this page: https://code.google.com/p/stack-exchange-data-explorer/source/checkout
3. Check out your clone
4. Commit your changes to the clone 
5. Open up a question on meta.stackoverflow.com asking for your change to be merged, tag it data-explorer


Full list of all third party software used to build Data Explorer:

- ASP.NET MVC Version 2: http://www.microsoft.com/downloads/details.aspx?FamilyID=c9ba1fe1-3ba8-439a-9e21-def90a8615a9&displaylang=en
- Code Mirror: http://marijn.haverbeke.nl/codemirror/
- jQuery 1.4.2: http://jquery.com/
- jQuery validation: http://bassistance.de/jquery-plugins/jquery-plugin-validation/
- JSon.Net: http://james.newtonking.com/projects/json-net.aspx
- DotNetOpenAuth: http://james.newtonking.com/projects/json-net.aspx
- YUI Compressor: http://developer.yahoo.com/yui/compressor/
- Slick Grid: http://github.com/mleibman/SlickGrid