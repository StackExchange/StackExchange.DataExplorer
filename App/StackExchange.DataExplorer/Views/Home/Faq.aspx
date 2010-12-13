<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<StackExchange.DataExplorer.Models.User>" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	FAQ - Stack Exchange Data Explorer
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
   <div class="content-page"> 
  <h3>What is this site?</h3>
  <p>
  Stack Exchange Data Explorer allows you to run arbitrary SQL queries on the Stack Exchange family of sites, share those queries with your friends and explore interesting queries.
  </p>
  <h3>Sounds interesting can you share some technical details?</h3>
  <p>
  Stack Exchange Data Explorer is hosted on SQL Server 2008 R2, the connections to the DB are readonly, you may not alter any tables.
  </p>
  <p>
  The controls used are jQuery based, the grid is SlickGrid.
  All query results are cached, so sharing a link to a query will not result in the server going in to a tailspin.
  The query edit control is a slightly modified CodeMirror control, it gives up nice syntax highlighting for queries.
  </p>
  <h3>Sounds good, where is the source code?</h3>
  <p>
    Stack Exchange Data Explorer is <b>Open Source</b>. 
    If you would like to help us make it more awesome, <a href="http://code.google.com/p/stack-exchange-data-explorer/">check out the code</a>.
  </p>
  <h3>Do you support parameterized queries?</h3>
  <p><strong>Yes</strong> to get it to work name your parameters like ##this##.
  </p>
     <p>There is also built in support for strongly typed parameters of type <strong>string</strong>, 
       <strong>int</strong> and <strong>float</strong>. If you name your parameter ##bob:string## it will be treated as a 
       string, this will result in both quoting and escaping of single quotes.
  </p>

  <h3>How do I name and describe my queries?</h3>
  <p>
A query can have a name and description, to name it lead with a comment, to describe it continue commenting, for example:

<pre>
-- This is my query name 
-- This is my description
-- I can span multiple lines
SELECT 1

</pre>
</p>
<h3>What is the point of logging on?</h3>
<p>
Anonymous users can initially name a query, but can not change the names of any existing queries. Additional features for logged on users: 
</p>
<ul>
<li>We will keep track of all the queries you execute</li>
<li>We will automatically populate any parameter named ##UserId## with your user id on the respective sites (if your EmailHash matches)</li>
<li>We will allow you to save queries so you can get back to them later</li>
</ul>

<h3>What is the deal with magic columns?</h3>
<p>
If you alias an id column with as [Post Link] it will automatically create a link in the result set to the parent site. Similarly if you alias an id column with as [User Link] it will display a link to the user page. select top 10 * from Posts will show you how tags are done. (Magic columns for images is planned) example: <a href="/stackoverflow/s/87/most-controversial-posts-on-the-site">most controversial posts</a>
</p>

<h3>What is the deal with featured queries?</h3>
<p>
At the moment, admins have permission to feature interesting queries on <a href="/stackoverflow/queries">this page</a>, we are looking for better ways to manage the huge query list that is building up.
</p>

<h3>Why are my queries not showing up in the recent query list?</h3>
<p>
The recent, featured and popular query lists only show saved queries. If you have a query you would like to share with the community be sure to save it. 
</p>

<!--
<h3>Do you offer Open Data protocol endpoints to the Stack Exchange sites? </h3>
<p> Yes, on the <a href="/">front page</a> there are links to the <a href="http://www.odata.org/">OData</a> endpoints.
</p>
-->

<h3>How frequently is Stack Exchange Data Explorer updated?</h3>
<p>At the moment it is updated shortly after every Stack Exchange <a href="http://blog.stackoverflow.com/category/cc-wiki-dump/">Creative Commons Data Dump</a>.</p>

</div>
</asp:Content>

