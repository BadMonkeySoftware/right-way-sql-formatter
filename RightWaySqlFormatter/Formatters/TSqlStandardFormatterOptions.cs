/*
Poor Man's T-SQL Formatter - a small free Transact-SQL formatting 
library for .Net 2.0 and JS, written in C#. 
Copyright (C) 2011-2013 Tao Klerks

Additional Contributors:
 * Timothy Klenke, 2012

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace PoorMansTSqlFormatterLib.Formatters
{
    public class TSqlStandardFormatterOptions
    {
        public TSqlStandardFormatterOptions()
        {
            IndentString = "    ";
            SpacesPerTab = 4;
            MaxLineWidth = 999;
            ExpandCommaLists = true;
            TrailingCommas = false;
            SpaceAfterExpandedComma = false;
            ExpandBooleanExpressions = true;
            ExpandBetweenConditions = true;
            ExpandCaseStatements = true;
            UppercaseKeywords = true;
            BreakJoinOnSections = false;
            HTMLColoring = false;
			KeywordStandardization = false;
			ExpandInLists = true;
			NewClauseLineBreaks = 1;
			NewStatementLineBreaks = 2;
            SelectFirstColumnOnNewLine = false;
            ColumnAliasStyle = ColumnAliasStyle.AsKeyword;
            AlignColumnDefinitions = false;
            IndentJoinOnClause = false;
            IndentWhereAndOrConditions = false;
            AlignColumnDefinitionsInDDL = false;
            DDLConstraintsOnNewLine = false;
            AlignTableJoins = false;
            AlignTableJoinsAddAliases = true;
            ColumnAlwaysHasAlias = false;
            CompactRaiserror = false;
            CompactSingleStatementBlocks = false;
		}

        //Doesn't particularly need to be lazy-loaded, and doesn't need to be threadsafe.
        private static readonly TSqlStandardFormatterOptions _defaultOptions = new TSqlStandardFormatterOptions();

        public TSqlStandardFormatterOptions(string serializedString) : this() {

            if (string.IsNullOrEmpty(serializedString)) 
                return;
                       
            //PLEASE NOTE: This is not reusable/general-purpose key-value serialization: it does not handle commas in data.
            // This will need to be enhanced if we ever need to store formatter options that might contain equals signs or 
			// commas.
            foreach (string kvp in serializedString.Split(','))
            {
                string[] splitPair = kvp.Split('=');
                string key = splitPair[0];
                string value = splitPair[1];

				if (key == "IndentString") IndentString = value;
				else if (key == "SpacesPerTab") SpacesPerTab = Convert.ToInt32(value);
				else if (key == "MaxLineWidth") MaxLineWidth = Convert.ToInt32(value);
				else if (key == "ExpandCommaLists") ExpandCommaLists = Convert.ToBoolean(value);
				else if (key == "TrailingCommas") TrailingCommas = Convert.ToBoolean(value);
				else if (key == "SpaceAfterExpandedComma") SpaceAfterExpandedComma = Convert.ToBoolean(value);
				else if (key == "ExpandBooleanExpressions") ExpandBooleanExpressions = Convert.ToBoolean(value);
				else if (key == "ExpandBetweenConditions") ExpandBetweenConditions = Convert.ToBoolean(value);
				else if (key == "ExpandCaseStatements") ExpandCaseStatements = Convert.ToBoolean(value);
				else if (key == "UppercaseKeywords") UppercaseKeywords = Convert.ToBoolean(value);
				else if (key == "BreakJoinOnSections") BreakJoinOnSections = Convert.ToBoolean(value);
				else if (key == "HTMLColoring") HTMLColoring = Convert.ToBoolean(value);
				else if (key == "KeywordStandardization") KeywordStandardization = Convert.ToBoolean(value);
				else if (key == "ExpandInLists") ExpandInLists = Convert.ToBoolean(value);
			else if (key == "NewClauseLineBreaks") NewClauseLineBreaks = Convert.ToInt32(value);
				else if (key == "NewStatementLineBreaks") NewStatementLineBreaks = Convert.ToInt32(value);
			else if (key == "SelectFirstColumnOnNewLine") SelectFirstColumnOnNewLine = Convert.ToBoolean(value);
				else if (key == "ColumnAliasStyle") ColumnAliasStyle = (ColumnAliasStyle)Enum.Parse(typeof(ColumnAliasStyle), value);
				else if (key == "AlignColumnDefinitions") AlignColumnDefinitions = Convert.ToBoolean(value);
				else if (key == "IndentJoinOnClause") IndentJoinOnClause = Convert.ToBoolean(value);
				else if (key == "IndentWhereAndOrConditions") IndentWhereAndOrConditions = Convert.ToBoolean(value);
			else if (key == "AlignColumnDefinitionsInDDL") AlignColumnDefinitionsInDDL = Convert.ToBoolean(value);
				else if (key == "DDLConstraintsOnNewLine") DDLConstraintsOnNewLine = Convert.ToBoolean(value);
				else if (key == "AlignTableJoins") AlignTableJoins = Convert.ToBoolean(value);
				else if (key == "AlignTableJoinsAddAliases") AlignTableJoinsAddAliases = Convert.ToBoolean(value);
				else if (key == "ColumnAlwaysHasAlias") ColumnAlwaysHasAlias = Convert.ToBoolean(value);
				else if (key == "CompactRaiserror") CompactRaiserror = Convert.ToBoolean(value);
				else if (key == "CompactSingleStatementBlocks") CompactSingleStatementBlocks = Convert.ToBoolean(value);
				else throw new ArgumentException("Unknown option: " + key);
            }

        }

		//PLEASE NOTE: This is not reusable/general-purpose key-value serialization: it does not handle commas in data.
		// This will need to be enhanced if we ever need to store formatter options that might contain equals signs or 
		// commas.
		public string ToSerializedString()
        { 
            var overrides = new Dictionary<string, string>();

            if (IndentString != _defaultOptions.IndentString) overrides.Add("IndentString", IndentString);
            if (SpacesPerTab != _defaultOptions.SpacesPerTab) overrides.Add("SpacesPerTab", SpacesPerTab.ToString());
            if (MaxLineWidth != _defaultOptions.MaxLineWidth) overrides.Add("MaxLineWidth", MaxLineWidth.ToString());
            if (ExpandCommaLists != _defaultOptions.ExpandCommaLists) overrides.Add("ExpandCommaLists", ExpandCommaLists.ToString());
            if (TrailingCommas != _defaultOptions.TrailingCommas) overrides.Add("TrailingCommas", TrailingCommas.ToString());
            if (SpaceAfterExpandedComma != _defaultOptions.SpaceAfterExpandedComma) overrides.Add("SpaceAfterExpandedComma", SpaceAfterExpandedComma.ToString());
            if (ExpandBooleanExpressions != _defaultOptions.ExpandBooleanExpressions) overrides.Add("ExpandBooleanExpressions", ExpandBooleanExpressions.ToString());
            if (ExpandBetweenConditions != _defaultOptions.ExpandBetweenConditions) overrides.Add("ExpandBetweenConditions", ExpandBetweenConditions.ToString());
            if (ExpandCaseStatements != _defaultOptions.ExpandCaseStatements) overrides.Add("ExpandCaseStatements", ExpandCaseStatements.ToString());
            if (UppercaseKeywords != _defaultOptions.UppercaseKeywords) overrides.Add("UppercaseKeywords", UppercaseKeywords.ToString());
            if (BreakJoinOnSections != _defaultOptions.BreakJoinOnSections) overrides.Add("BreakJoinOnSections", BreakJoinOnSections.ToString());
            if (HTMLColoring != _defaultOptions.HTMLColoring) overrides.Add("HTMLColoring", HTMLColoring.ToString());
			if (KeywordStandardization != _defaultOptions.KeywordStandardization) overrides.Add("KeywordStandardization", KeywordStandardization.ToString());
			if (ExpandInLists != _defaultOptions.ExpandInLists) overrides.Add("ExpandInLists", ExpandInLists.ToString());
			if (NewClauseLineBreaks != _defaultOptions.NewClauseLineBreaks) overrides.Add("NewClauseLineBreaks", NewClauseLineBreaks.ToString());
			if (NewStatementLineBreaks != _defaultOptions.NewStatementLineBreaks) overrides.Add("NewStatementLineBreaks", NewStatementLineBreaks.ToString());
			if (SelectFirstColumnOnNewLine != _defaultOptions.SelectFirstColumnOnNewLine) overrides.Add("SelectFirstColumnOnNewLine", SelectFirstColumnOnNewLine.ToString());
			if (ColumnAliasStyle != _defaultOptions.ColumnAliasStyle) overrides.Add("ColumnAliasStyle", ColumnAliasStyle.ToString());
			if (AlignColumnDefinitions != _defaultOptions.AlignColumnDefinitions) overrides.Add("AlignColumnDefinitions", AlignColumnDefinitions.ToString());
			if (IndentJoinOnClause != _defaultOptions.IndentJoinOnClause) overrides.Add("IndentJoinOnClause", IndentJoinOnClause.ToString());
			if (IndentWhereAndOrConditions != _defaultOptions.IndentWhereAndOrConditions) overrides.Add("IndentWhereAndOrConditions", IndentWhereAndOrConditions.ToString());
			if (AlignColumnDefinitionsInDDL != _defaultOptions.AlignColumnDefinitionsInDDL) overrides.Add("AlignColumnDefinitionsInDDL", AlignColumnDefinitionsInDDL.ToString());
			if (DDLConstraintsOnNewLine != _defaultOptions.DDLConstraintsOnNewLine) overrides.Add("DDLConstraintsOnNewLine", DDLConstraintsOnNewLine.ToString());
			if (AlignTableJoins != _defaultOptions.AlignTableJoins) overrides.Add("AlignTableJoins", AlignTableJoins.ToString());
			if (AlignTableJoinsAddAliases != _defaultOptions.AlignTableJoinsAddAliases) overrides.Add("AlignTableJoinsAddAliases", AlignTableJoinsAddAliases.ToString());
			if (ColumnAlwaysHasAlias != _defaultOptions.ColumnAlwaysHasAlias) overrides.Add("ColumnAlwaysHasAlias", ColumnAlwaysHasAlias.ToString());
			if (CompactRaiserror != _defaultOptions.CompactRaiserror) overrides.Add("CompactRaiserror", CompactRaiserror.ToString());
			if (CompactSingleStatementBlocks != _defaultOptions.CompactSingleStatementBlocks) overrides.Add("CompactSingleStatementBlocks", CompactSingleStatementBlocks.ToString());
    
            if (overrides.Count == 0) return string.Empty;
            return string.Join(",", overrides.Select((kvp) => kvp.Key + "=" + kvp.Value).ToArray());
           
        }

        private string? _indentString;
        public string IndentString
        {
            get
            {
                return _indentString ?? string.Empty;
            }
            set
            {
                _indentString = value.Replace("\\t", "\t").Replace("\\s", " ");
            }
        }

        public int SpacesPerTab { get; set; }
        public int MaxLineWidth { get; set; }
        public bool ExpandCommaLists { get; set; }
        public bool TrailingCommas { get; set; }
        public bool SpaceAfterExpandedComma { get; set; }
        public bool ExpandBooleanExpressions { get; set; }
        public bool ExpandCaseStatements { get; set; }
        public bool ExpandBetweenConditions { get; set; }
        public bool UppercaseKeywords { get; set; }
        public bool BreakJoinOnSections { get; set; }
        public bool HTMLColoring { get; set; }
		public bool KeywordStandardization { get; set; }
		public bool ExpandInLists { get; set; }
		public int NewClauseLineBreaks { get; set; }
		public int NewStatementLineBreaks { get; set; }

        /// <summary>
        /// When true and ExpandCommaLists is also true, the first column in a SELECT
        /// list breaks to a new line instead of staying inline with the SELECT keyword.
        /// e.g.: SELECT\n\tcol1\n\t,col2  instead of  SELECT col1\n\t,col2
        /// </summary>
        public bool SelectFirstColumnOnNewLine { get; set; }

        // ----------------------------------------------------------------
        // Task 2: Column alias style
        // ----------------------------------------------------------------
        /// <summary>
        /// Controls the style used for column aliases in SELECT lists.
        /// AsKeyword (default): col AS alias
        /// EqualSign: alias = col
        /// </summary>
        public ColumnAliasStyle ColumnAliasStyle { get; set; }

        // ----------------------------------------------------------------
        // Task 3: Column alignment
        // ----------------------------------------------------------------
        /// <summary>
        /// When true and ExpandCommaLists is true, pads column expressions so
        /// AS keywords align vertically in the SELECT list.
        /// </summary>
        public bool AlignColumnDefinitions { get; set; }

        // ----------------------------------------------------------------
        // Task 4: JOIN / WHERE alignment
        // ----------------------------------------------------------------
        /// <summary>
        /// When true, indents the ON clause an extra level relative to the
        /// JOIN keyword, aligning it under the join condition columns.
        /// </summary>
        public bool IndentJoinOnClause { get; set; }

        /// <summary>
        /// When true, AND/OR conditions in a WHERE clause are placed on their
        /// own lines aligned under the first condition (not the WHERE keyword).
        /// Default is true to preserve existing formatter behaviour.
        /// </summary>
        public bool IndentWhereAndOrConditions { get; set; }

        // ----------------------------------------------------------------
        // Task 5: DDL formatting
        // ----------------------------------------------------------------
        /// <summary>
        /// When true, in CREATE TABLE statements, aligns column name, data type,
        /// nullability, and constraints into vertical columns.
        /// </summary>
        public bool AlignColumnDefinitionsInDDL { get; set; }

        /// <summary>
        /// When true (default), in CREATE TABLE statements, each column constraint
        /// is placed on its own line.
        /// Default is false to avoid breaking existing tests.
        /// </summary>
        public bool DDLConstraintsOnNewLine { get; set; }

        // ----------------------------------------------------------------
        // Task 6: Table join alignment
        // ----------------------------------------------------------------
        /// <summary>
        /// When true, aligns FROM/JOIN table names, aliases, and ON conditions
        /// into vertical columns across all joins in a query block.
        /// - Pads keyword+tablename so all table names end at the same tab stop
        /// - Adds AS alias if none exists (using the table name as the alias)
        /// - Vertically aligns the AS keyword and alias across all joins
        /// - Vertically aligns the ON keyword
        /// - Long multi-condition ON clauses (> 100 chars) wrap to next line
        /// </summary>
        public bool AlignTableJoins { get; set; }

        // ----------------------------------------------------------------
        // Task 7: Column always has alias
        // ----------------------------------------------------------------
        /// <summary>
        /// When true, every column in a SELECT list is given an explicit alias.
        /// - Bare column references (e.g. t.ColName, [MyCol]) → alias is the column base name.
        /// - Complex expressions (functions, arithmetic, CASE, *) → alias is ColumnAlias_N
        ///   where N is a counter that increments for each auto-generated alias within a query.
        /// - Columns that already have an alias are left unchanged.
        /// </summary>
        public bool ColumnAlwaysHasAlias { get; set; }

        /// <summary>
        /// When AlignTableJoins is active: also add an alias (derived from the table name)
        /// to tables that have none. Default true (historical AlignTableJoins behavior);
        /// set false to align only, without inventing aliases.
        /// </summary>
        public bool AlignTableJoinsAddAliases { get; set; }

        /// <summary>
        /// Keep RAISERROR(...) argument lists on a single line instead of expanding
        /// each comma-separated argument (default false = historical behavior).
        /// </summary>
        public bool CompactRaiserror { get; set; }

        /// <summary>
        /// Render single-statement IF/ELSE/WHILE bodies (no BEGIN/END) on the same line
        /// as their control keyword when the result is single-line and fits MaxLineWidth.
        /// </summary>
        public bool CompactSingleStatementBlocks { get; set; }

    }

    /// <summary>Style used for SELECT column aliases.</summary>
    public enum ColumnAliasStyle
    {
        /// <summary>col AS alias  (default, existing behaviour)</summary>
        AsKeyword = 0,
        /// <summary>alias = col</summary>
        EqualSign = 1
    }
}
