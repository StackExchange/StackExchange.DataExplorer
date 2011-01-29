<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:msxsl="urn:schemas-microsoft-com:xslt" exclude-result-prefixes="msxsl"
  xmlns:s="http://schemas.microsoft.com/sqlserver/2004/07/showplan">
  <xsl:output method="html" indent="no" omit-xml-declaration="yes" />

  <!-- Disable built-in recursive processing templates -->
  <xsl:template match="*|/|text()|@*" mode="NodeLabel" />
  <xsl:template match="*|/|text()|@*" mode="ToolTipDescription" />
  <xsl:template match="*|/|text()|@*" mode="ToolTipDetails" />

  <!-- Default template -->
  <xsl:template match="/">
    <xsl:apply-templates select="s:ShowPlanXML/s:BatchSequence/s:Batch/s:Statements/s:StmtSimple" />
  </xsl:template>
  
  <!-- Matches a statement -->
  <xsl:template match="s:StmtSimple">
    <li>
      <div class="qp-td">
        <div class="qp-node">
          <xsl:element name="div">
            <xsl:attribute name="class">qp-icon-Statement</xsl:attribute>
          </xsl:element>
          <div class="qp-label"><xsl:value-of select="@StatementType" /></div>
          <xsl:apply-templates select="." mode="NodeLabel" />
          <xsl:call-template name="ToolTip" />
        </div>
      </div>
      <ul class="qp-td"><xsl:apply-templates select="*/s:RelOp" /></ul>
    </li>
  </xsl:template>
  
  <!-- Matches a branch in the query plan -->
  <xsl:template match="s:RelOp">
    <li>
      <div class="qp-td">
        <div class="qp-node">
          <xsl:element name="div">
            <xsl:attribute name="class">qp-icon-<xsl:value-of select="translate(@PhysicalOp, ' ', '')" /></xsl:attribute>
          </xsl:element>
          <div class="qp-label"><xsl:value-of select="@PhysicalOp" /></div>
          <xsl:apply-templates select="." mode="NodeLabel" />
          <xsl:call-template name="ToolTip" />
        </div>
      </div>
      <ul class="qp-td"><xsl:apply-templates select="*/s:RelOp" /></ul>
    </li>
  </xsl:template>

  <!-- Writes the tool tip -->
  <xsl:template name="ToolTip">
    <div class="qp-tt">
      <div class="qp-tt-header"><xsl:value-of select="@PhysicalOp | @StatementType" /></div>
      <div><xsl:apply-templates select="." mode="ToolTipDescription" /></div>
      <xsl:call-template name="ToolTipGrid" />
      <xsl:apply-templates select="* | @* | */* | */@*" mode="ToolTipDetails" />
    </div>
  </xsl:template>

  <!-- Writes the grid of node properties to the tool tip -->
  <xsl:template name="ToolTipGrid">
    <table>
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Condition" select="s:QueryPlan/@CachedPlanSize" />
        <xsl:with-param name="Label">Cached plan size</xsl:with-param>
        <xsl:with-param name="Value" select="concat(s:QueryPlan/@CachedPlanSize, ' B')" />
      </xsl:call-template>
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Label">Physical Operation</xsl:with-param>
        <xsl:with-param name="Value" select="@PhysicalOp" />
      </xsl:call-template>
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Label">Logical Operation</xsl:with-param>
        <xsl:with-param name="Value" select="@LogicalOp" />
      </xsl:call-template>
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Label">Actual Number of Rows</xsl:with-param>
        <xsl:with-param name="Value" select="s:RunTimeInformation/s:RunTimeCountersPerThread/@ActualRows" />
      </xsl:call-template>
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Label">Estimated I/O Cost</xsl:with-param>
        <xsl:with-param name="Value" select="@EstimateIO" />
      </xsl:call-template>
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Label">Estimated CPU Cost</xsl:with-param>
        <xsl:with-param name="Value" select="@EstimateCPU" />
      </xsl:call-template>
      <!-- TODO: Estimated Number of Executions -->
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Label">Number of Executions</xsl:with-param>
        <xsl:with-param name="Value" select="s:RunTimeInformation/s:RunTimeCountersPerThread/@ActualExecutions" />
      </xsl:call-template>
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Label">Degree of Parallelism</xsl:with-param>
        <xsl:with-param name="Value" select="s:QueryPlan/@DegreeOfParallelism" />
      </xsl:call-template>
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Label">Memory Grant</xsl:with-param>
        <xsl:with-param name="Value" select="s:QueryPlan/@MemoryGrant" />
      </xsl:call-template>
      <!-- TODO: Estimated Operator Cost -->
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Label">Estimated Subtree Cost</xsl:with-param>
        <xsl:with-param name="Value" select="@StatementSubTreeCost | @EstimatedTotalSubtreeCost" />
      </xsl:call-template>
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Label">Estimated Number of Rows</xsl:with-param>
        <xsl:with-param name="Value" select="@StatementEstRows | @EstimateRows" />
      </xsl:call-template>
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Condition" select="@AvgRowSize" />
        <xsl:with-param name="Label">Estimated Row Size</xsl:with-param>
        <xsl:with-param name="Value" select="concat(@AvgRowSize, ' B')" />
      </xsl:call-template>
      <!-- TODO: Actual Rebinds
           TODO: Actual Rewinds -->
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Condition" select="s:IndexScan/@Ordered" />
        <xsl:with-param name="Label">Ordered</xsl:with-param>
        <xsl:with-param name="Value">
          <xsl:choose>
            <xsl:when test="s:IndexScan/@Ordered = 1">True</xsl:when>
            <xsl:otherwise>False</xsl:otherwise>
          </xsl:choose>
        </xsl:with-param>
      </xsl:call-template>
      <xsl:call-template name="ToolTipRow">
        <xsl:with-param name="Label">Node ID</xsl:with-param>
        <xsl:with-param name="Value" select="@NodeId" />
      </xsl:call-template>
    </table>
  </xsl:template>

  <!-- Renders a row in the tool tip details table -->
  <xsl:template name="ToolTipRow">
    <xsl:param name="Label" />
    <xsl:param name="Value" />
    <xsl:param name="Condition" select="$Value" />
    <xsl:if test="$Condition">
      <tr>
        <th><xsl:value-of select="$Label" /></th>
        <td><xsl:value-of select="$Value" /></td>
      </tr>      
    </xsl:if>
  </xsl:template>

  <!-- Prints the name of an object -->
  <xsl:template match="s:Object | s:ColumnReference" mode="ObjectName">
    <xsl:for-each select="@Database | @Schema | @Table | @Index | @Column | @Alias">
      <xsl:value-of select="." />
      <xsl:if test="position() != last()">.</xsl:if>
    </xsl:for-each>
  </xsl:template>
  
  <!-- 
  ================================
  Tool tip detail sections
  ================================
  The following section contains templates used for writing the detail sections at the bottom of the tool tip,
  for example listing outputs, or information about the object to which an operator applies.
  -->

  <xsl:template match="*/s:Object" mode="ToolTipDetails">
    <!-- TODO: Make sure this works all the time -->
    <div class="qp-bold">Object</div>
    <div><xsl:apply-templates select="." mode="ObjectName" /></div>
  </xsl:template>

  <xsl:template match="s:SetPredicate[s:ScalarOperator/@ScalarString]" mode="ToolTipDetails">
    <div class="qp-bold">Predicate</div>
    <div><xsl:value-of select="s:ScalarOperator/@ScalarString" /></div>
  </xsl:template>

  <xsl:template match="s:OutputList[count(s:ColumnReference) > 0]" mode="ToolTipDetails">
    <div class="qp-bold">Output List</div>
    <xsl:for-each select="s:ColumnReference">
      <div><xsl:apply-templates select="." mode="ObjectName" /></div>
    </xsl:for-each>
  </xsl:template>

  <xsl:template match="s:NestedLoops/s:OuterReferences[count(s:ColumnReference) > 0]" mode="ToolTipDetails">
    <div class="qp-bold">Outer References</div>
    <xsl:for-each select="s:ColumnReference">
      <div><xsl:apply-templates select="." mode="ObjectName" /></div>
    </xsl:for-each>
  </xsl:template>

  <xsl:template match="@StatementText" mode="ToolTipDetails">
    <div class="qp-bold">Statement</div>
    <div><xsl:value-of select="." /></div>
  </xsl:template>

  <xsl:template match="s:Sort/s:OrderBy[count(s:OrderByColumn/s:ColumnReference) > 0]" mode="ToolTipDetails">
    <div class="qp-bold">Order By</div>
    <xsl:for-each select="s:OrderByColumn">
      <div>
        <xsl:apply-templates select="s:ColumnReference" mode="ObjectName" />
        <xsl:choose>
          <xsl:when test="@Ascending = 1"> Ascending</xsl:when>
          <xsl:otherwise> Descending</xsl:otherwise>
        </xsl:choose>
      </div>
    </xsl:for-each>
  </xsl:template>

  <!-- TODO: Seek Predicates -->

  <!--
  ================================
  Operator specific node labels
  ================================
  The following section contains templates used for writing operator-type specific node labels.
  -->
  
  <!-- Node label for "Nested Loops" operation -->
  <xsl:template match="*[s:NestedLoops]" mode="NodeLabel">
    <div class="qp-label">(<xsl:value-of select="@LogicalOp" />)</div>
  </xsl:template>

  <!-- Node label for "Index Scan" operation -->
  <xsl:template match="*[s:IndexScan]" mode="NodeLabel">
    <xsl:variable name="IndexName" select="concat(s:IndexScan/s:Object/@Table, '.', s:IndexScan/s:Object/@Index)" />
    <div class="qp-label">
      <xsl:value-of select="substring($IndexName, 0, 36)" />
      <xsl:if test="string-length($IndexName) >= 36">…</xsl:if>
    </div>
  </xsl:template>

  <xsl:template match="*[s:TableScan]" mode="NodeLabel">
    <xsl:variable name="IndexName" select="concat(s:TableScan/s:Object/@Schema, '.', s:TableScan/s:Object/@Table)" />
    <div class="qp-label">
      <xsl:value-of select="substring($IndexName, 0, 36)" />
      <xsl:if test="string-length($IndexName) >= 36">…</xsl:if>
    </div>
  </xsl:template>

  <!-- 
  ================================
  Tool tip descriptions
  ================================
  The following section contains templates used for writing the description shown in the tool tip.
  -->

  <xsl:template match="*[@PhysicalOp = 'Table Insert']" mode="ToolTipDescription">Insert input rows into the table specified in Argument field.</xsl:template>
  <xsl:template match="*[@PhysicalOp = 'Compute Scalar']" mode="ToolTipDescription">Compute new values from existing values in a row.</xsl:template>
  <xsl:template match="*[@PhysicalOp = 'Sort']" mode="ToolTipDescription">Sort the input.</xsl:template>
  <xsl:template match="*[@PhysicalOp = 'Clustered Index Scan']" mode="ToolTipDescription">Scanning a clustered index, entirely or only a range.</xsl:template>
  <xsl:template match="*[s:TableScan]" mode="ToolTipDescription">Scan rows from a table.</xsl:template>
  <xsl:template match="*[s:NestedLoops]" mode="ToolTipDescription">For each row in the top (outer) input, scan the bottom (inner) input, and output matching rows.</xsl:template>
  <xsl:template match="*[s:Top]" mode="ToolTipDescription">Select the first few rows based on a sort order.</xsl:template>
</xsl:stylesheet>
