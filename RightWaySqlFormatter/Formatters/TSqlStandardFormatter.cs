/*
Poor Man's T-SQL Formatter - a small free Transact-SQL formatting 
library for .Net 2.0 and JS, written in C#. 
Copyright (C) 2011-2017 Tao Klerks

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
using System.Text.RegularExpressions;
using PoorMansTSqlFormatterLib.Interfaces;
using PoorMansTSqlFormatterLib.ParseStructure;

namespace PoorMansTSqlFormatterLib.Formatters
{
    public class TSqlStandardFormatter : ISqlTreeFormatter
    {
        public TSqlStandardFormatter() : this(new TSqlStandardFormatterOptions()) { }
        
        public TSqlStandardFormatter(TSqlStandardFormatterOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");

            Options = options;

            if (options.KeywordStandardization)
                KeywordMapping = StandardKeywordRemapping.Instance;
            ErrorOutputPrefix = MessagingConstants.FormatErrorDefaultMessage + Environment.NewLine;
        }

        [Obsolete("Use the constructor with the TSqlStandardFormatterOptions parameter")]
        public TSqlStandardFormatter(string indentString, int spacesPerTab, int maxLineWidth, bool expandCommaLists, bool trailingCommas, bool spaceAfterExpandedComma, bool expandBooleanExpressions, bool expandCaseStatements, bool expandBetweenConditions, bool breakJoinOnSections, bool uppercaseKeywords, bool htmlColoring, bool keywordStandardization)
        {
            Options = new TSqlStandardFormatterOptions
                {
                    IndentString = indentString,
                    SpacesPerTab = spacesPerTab,
                    MaxLineWidth = maxLineWidth,
                    ExpandCommaLists = expandCommaLists,
                    TrailingCommas = trailingCommas,
                    SpaceAfterExpandedComma = spaceAfterExpandedComma,
                    ExpandBooleanExpressions = expandBooleanExpressions,
                    ExpandBetweenConditions = expandBetweenConditions,
                    ExpandCaseStatements = expandCaseStatements,
                    UppercaseKeywords = uppercaseKeywords,
                    BreakJoinOnSections = breakJoinOnSections,
                    HTMLColoring = htmlColoring,
                    KeywordStandardization = keywordStandardization
                };

            if (keywordStandardization)
                KeywordMapping = StandardKeywordRemapping.Instance;
            ErrorOutputPrefix = MessagingConstants.FormatErrorDefaultMessage + Environment.NewLine;
        }
        
        public TSqlStandardFormatterOptions Options { get; private set; }

        public IDictionary<string, string> KeywordMapping = new Dictionary<string, string>();

        [Obsolete("Use Options.IndentString instead")]
        public string IndentString { get { return Options.IndentString; } set { Options.IndentString = value; } }
        [Obsolete("Use Options.SpacesPerTab instead")]
        public int SpacesPerTab { get { return Options.SpacesPerTab; } set { Options.SpacesPerTab = value; } }
        [Obsolete("Use Options.MaxLineWidth instead")]
        public int MaxLineWidth { get { return Options.MaxLineWidth; } set { Options.MaxLineWidth = value; } }
        [Obsolete("Use Options.ExpandCommaLists instead")]
        public bool ExpandCommaLists { get { return Options.ExpandCommaLists; } set { Options.ExpandCommaLists = value; } }
        [Obsolete("Use Options.TrailingCommas instead")]
        public bool TrailingCommas { get { return Options.TrailingCommas; } set { Options.TrailingCommas = value; } }
        [Obsolete("Use Options.SpaceAfterExpandedComma instead")]
        public bool SpaceAfterExpandedComma { get { return Options.SpaceAfterExpandedComma; } set { Options.SpaceAfterExpandedComma = value; } }
        [Obsolete("Use Options.ExpandBooleanExpressions instead")]
        public bool ExpandBooleanExpressions { get { return Options.ExpandBooleanExpressions; } set { Options.ExpandBooleanExpressions = value; } }
        [Obsolete("Use Options.ExpandBetweenConditions instead")]
        public bool ExpandCaseStatements { get { return Options.ExpandCaseStatements; } set { Options.ExpandCaseStatements = value; } }
        [Obsolete("Use Options.ExpandCaseStatements instead")]
        public bool ExpandBetweenConditions { get { return Options.ExpandBetweenConditions; } set { Options.ExpandBetweenConditions = value; } }
        [Obsolete("Use Options.UppercaseKeywords instead")]
        public bool UppercaseKeywords { get { return Options.UppercaseKeywords; } set { Options.UppercaseKeywords = value; } }
        [Obsolete("Use Options.BreakJoinOnSections instead")]
        public bool BreakJoinOnSections { get { return Options.BreakJoinOnSections; } set { Options.BreakJoinOnSections = value; } }
        [Obsolete("Use Options.HTMLColoring instead")]
        public bool HTMLColoring { get { return Options.HTMLColoring; } set { Options.HTMLColoring = value; } }

        public bool HTMLFormatted { get { return Options.HTMLColoring; } }
        public string ErrorOutputPrefix { get; set; }

        public string FormatSQLTree(Node sqlTreeDoc)
        {
            //thread-safe - each call to FormatSQLTree() gets its own independent state object
            TSqlStandardFormattingState state = new TSqlStandardFormattingState(Options.HTMLColoring, Options.IndentString, Options.SpacesPerTab, Options.MaxLineWidth, 0);

            if (sqlTreeDoc.Name == SqlStructureConstants.ENAME_SQL_ROOT && sqlTreeDoc.GetAttributeValue(SqlStructureConstants.ANAME_ERRORFOUND) == "1")
                state.AddOutputContent(ErrorOutputPrefix);

            ProcessSqlNodeList(sqlTreeDoc.Children, state);

            WhiteSpace_BreakAsExpected(state);

            //someone forgot to close a "[noformat]" or "[minify]" region... we'll assume that's ok
            if (state.SpecialRegionActive == SpecialRegionType.NoFormat)
            {
                Node? skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, sqlTreeDoc);
                if (skippedXml != null)
                {
                    TSqlIdentityFormatter tempFormatter = new TSqlIdentityFormatter(Options.HTMLColoring);
                    state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
                }
            }
            else if (state.SpecialRegionActive == SpecialRegionType.Minify)
            {
                Node? skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, sqlTreeDoc);
                if (skippedXml != null)
                {
                    TSqlObfuscatingFormatter tempFormatter = new TSqlObfuscatingFormatter();
                    if (HTMLFormatted)
                        state.AddOutputContentRaw(Utils.HtmlEncode(tempFormatter.FormatSQLTree(skippedXml)));
                    else
                        state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
                }
            }
            string output = state.DumpOutput();

            // Post-processing passes for SELECT column formatting.
            if (!Options.HTMLColoring)
            {
                if (Options.ColumnAlwaysHasAlias)
                    output = EnsureColumnAliases(output);
                if (Options.ColumnAliasStyle == ColumnAliasStyle.EqualSign)
                    output = RewriteAliasesToEqualSign(output);
                if (Options.AlignColumnDefinitions)
                    output = AlignSelectColumns(output);
                if (Options.AlignTableJoins)
                    output = AlignFromJoinClauses(output);
            }

            return output;
        }

        private void ProcessSqlNodeList(IEnumerable<Node> rootList, TSqlStandardFormattingState state)
        {
            foreach (Node contentElement in rootList)
                ProcessSqlNode(contentElement, state);
        }

        // ----------------------------------------------------------------
        // Helpers for column-alignment features
        // ----------------------------------------------------------------

        /// <summary>
        /// Returns the indent-adjusted text length of a single token node as it would be emitted,
        /// without actually writing to any output.  Used for pre-measurement passes.
        /// Only measures leaf content tokens — whitespace and structural nodes return 0.
        /// </summary>
        /// <summary>
        /// Returns a helper that knows whether a node is inside a DDL column list
        /// (i.e. a DDL_PARENS or DDLDETAIL_PARENS context).
        /// </summary>
        private static bool IsInsideDdlDetailParens(Node node)
        {
            var p = node.Parent;
            while (p != null)
            {
                if (p.Name == SqlStructureConstants.ENAME_DDL_PARENS
                    || p.Name == SqlStructureConstants.ENAME_DDLDETAIL_PARENS)
                    return true;
                if (p.Name == SqlStructureConstants.ENAME_SQL_ROOT ||
                    p.Name == SqlStructureConstants.ENAME_SQL_STATEMENT)
                    return false;
                p = p.Parent;
            }
            return false;
        }

        // ----------------------------------------------------------------
        // Post-processing helpers for output-string transformations
        // ----------------------------------------------------------------

        // ----------------------------------------------------------------
        // AlignFromJoinClauses: align FROM/JOIN table names, aliases, and ON
        // ----------------------------------------------------------------

        /// <summary>
        /// Scans the formatted output for FROM/JOIN blocks and rewrites them so:
        ///   1. All table names (keyword + tablename) end at the same tab stop.
        ///   2. Every line has an explicit AS alias (uses table base-name if none).
        ///   3. AS and alias are vertically aligned across all FROM/JOIN lines.
        ///   4. ON is vertically aligned at the next tab stop past the longest alias.
        ///   5. Multi-condition ON clauses that exceed 100 chars wrap to the next line,
        ///      aligned under the ON keyword.
        /// </summary>
        private string AlignFromJoinClauses(string output)
        {
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int indentSize = Options.SpacesPerTab > 0 ? Options.SpacesPerTab : 4;

            // Find contiguous FROM/JOIN blocks and align each one.
            int i = 0;
            while (i < lines.Length)
            {
                string trimmed = lines[i].TrimStart();
                string upper = trimmed.ToUpperInvariant();
                if (IsFromOrJoinLine(upper))
                {
                    int blockStart = i;
                    while (i < lines.Length)
                    {
                        string t = lines[i].TrimStart().ToUpperInvariant();
                        if (i > blockStart && !IsFromOrJoinLine(t) && !IsJoinContinuationLine(t))
                            break;
                        i++;
                    }
                    AlignFromJoinBlock(lines, blockStart, i, indentSize);
                    continue;
                }
                i++;
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static bool IsFromOrJoinLine(string trimmedUpper)
        {
            return trimmedUpper.StartsWith("FROM ") || trimmedUpper == "FROM"
                || trimmedUpper.StartsWith("INNER JOIN ") || trimmedUpper.StartsWith("LEFT JOIN ")
                || trimmedUpper.StartsWith("RIGHT JOIN ") || trimmedUpper.StartsWith("FULL JOIN ")
                || trimmedUpper.StartsWith("FULL OUTER JOIN ") || trimmedUpper.StartsWith("LEFT OUTER JOIN ")
                || trimmedUpper.StartsWith("RIGHT OUTER JOIN ") || trimmedUpper.StartsWith("CROSS JOIN ")
                || trimmedUpper.StartsWith("JOIN ");
        }

        private static bool IsJoinContinuationLine(string trimmedUpper)
        {
            // Lines that are AND/OR continuation of a multi-condition ON clause.
            return trimmedUpper.StartsWith("AND ") || trimmedUpper.StartsWith("OR ");
        }

        /// <summary>
        /// Parses one FROM/JOIN block (lines[start..end)) and rewrites all lines
        /// with aligned table names, aligned AS aliases, and aligned ON conditions.
        /// </summary>
        private void AlignFromJoinBlock(string[] lines, int start, int end, int indentSize)
        {
            // Keyword tokens that we inject — respect UppercaseKeywords setting.
            bool uc = Options.UppercaseKeywords;
            string KW_AS  = uc ? "AS"  : "as";
            string KW_ON  = uc ? "ON"  : "on";
            string KW_AND = uc ? "AND" : "and";

            // ---- Pass 1: parse each FROM/JOIN line --------------------------
            // For each line extract: indent, keyword (e.g. "FROM", "INNER JOIN"), table, alias, on-clause.
            var items = new List<JoinLineItem>();

            for (int i = start; i < end; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                string trimmedUpper = trimmed.ToUpperInvariant();

                // Skip continuation AND/OR lines — we'll re-attach them later.
                if (IsJoinContinuationLine(trimmedUpper))
                    continue;

                string indent = line.Substring(0, line.Length - trimmed.Length);
                ParseFromJoinLine(trimmed, out string keyword, out string table, out string alias, out string onClause);

                items.Add(new JoinLineItem
                {
                    LineIndex = i,
                    Indent = indent,
                    Keyword = keyword,
                    Table = table,
                    Alias = alias,
                    OnClause = onClause,
                });
            }

            if (items.Count == 0) return;

            // ---- Pass 2: ensure every item has an alias ---------------------
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Alias))
                    item.Alias = BaseTableName(item.Table);
            }

            // ---- Pass 3: compute column positions --------------------------
            // Col A: end of keyword+table — target = next tab stop after max(keyword.Length + 1 + table.Length)
            int maxKwTableLen = items.Max(it => it.Keyword.Length + 1 + it.Table.Length);
            int targetTableCol = ((maxKwTableLen / indentSize) + 1) * indentSize;

            // Col B: end of AS+alias — target = next tab stop after max(alias.Length) + "AS ".Length
            int maxAliasLen = items.Max(it => it.Alias.Length);
            // "AS alias" — the AS itself is 3 chars ("AS "). Target col for ON = next tab stop after "AS " + maxAlias.
            int asAliasLen = 3 + maxAliasLen; // "AS " + alias
            int targetOnCol = targetTableCol + ((asAliasLen / indentSize) + 1) * indentSize;

            // ---- Pass 4: rewrite lines -------------------------------------
            int itemIdx = 0;
            for (int i = start; i < end; i++)
            {
                string trimmedUpper = lines[i].TrimStart().ToUpperInvariant();
                if (IsJoinContinuationLine(trimmedUpper))
                {
                    // Re-indent continuation lines to align with ON position.
                    string contIndent = items[itemIdx > 0 ? itemIdx - 1 : 0].Indent
                        + new string(' ', targetOnCol);
                    lines[i] = contIndent + lines[i].TrimStart();
                    continue;
                }

                var item = items[itemIdx++];
                string kwTable = item.Keyword + " " + item.Table;
                int kwTableLen = kwTable.Length;
                string tablepad = new string(' ', targetTableCol - kwTableLen);

                string aliasPad = new string(' ', maxAliasLen - item.Alias.Length);
                string asPart = KW_AS + " " + item.Alias + aliasPad;

                string newLine;
                if (string.IsNullOrEmpty(item.OnClause))
                {
                    // FROM line — no ON clause
                    newLine = item.Indent + kwTable + tablepad + asPart;
                }
                else
                {
                    string onFull = item.Indent + kwTable + tablepad + asPart + " " + KW_ON + " " + item.OnClause;
                    // Check if it fits within 100 chars; if not, check further if multi-condition
                    if (onFull.Length > 100 && item.OnClause.IndexOf(" AND ", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Split conditions and wrap extras.
                        string onBase = item.Indent + kwTable + tablepad + asPart + " " + KW_ON + " ";
                        // AND continuation lines align WITH the ON keyword (same column as ON, not after it)
                        string onIndent = new string(' ', targetOnCol);
                        var conditions = SplitOnConditions(item.OnClause);
                        string firstCond = conditions[0];
                        var rest = conditions.Skip(1).Select(c => item.Indent + onIndent + KW_AND + " " + c);
                        newLine = onBase + firstCond + (rest.Any() ? Environment.NewLine + string.Join(Environment.NewLine, rest) : "");
                    }
                    else
                    {
                        newLine = onFull;
                    }
                }
                lines[i] = newLine;
            }
        }

        private sealed class JoinLineItem
        {
            public int LineIndex { get; set; }
            public string Indent { get; set; } = "";
            public string Keyword { get; set; } = "";
            public string Table { get; set; } = "";
            public string Alias { get; set; } = "";
            public string OnClause { get; set; } = "";
        }

        /// <summary>
        /// Parses a single FROM/JOIN trimmed line into its components.
        /// </summary>
        private static void ParseFromJoinLine(string trimmed, out string keyword, out string table, out string alias, out string onClause)
        {
            // Keywords: FROM, JOIN, INNER JOIN, LEFT JOIN, LEFT OUTER JOIN, RIGHT JOIN,
            //           RIGHT OUTER JOIN, FULL JOIN, FULL OUTER JOIN, CROSS JOIN
            string[] keywords = new string[] {
                "LEFT OUTER JOIN", "RIGHT OUTER JOIN", "FULL OUTER JOIN",
                "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN",
                "JOIN", "FROM"
            };

            keyword = "";
            string rest = trimmed;
            string upper = trimmed.ToUpperInvariant();
            foreach (string kw in keywords)
            {
                if (upper.StartsWith(kw + " ") || upper == kw)
                {
                    keyword = trimmed.Substring(0, kw.Length); // preserve casing
                    rest = trimmed.Substring(kw.Length).TrimStart();
                    break;
                }
            }

            if (string.IsNullOrEmpty(keyword))
            {
                // Fallback: take first word as keyword
                int sp = trimmed.IndexOf(' ');
                keyword = sp >= 0 ? trimmed.Substring(0, sp) : trimmed;
                rest = sp >= 0 ? trimmed.Substring(sp + 1).TrimStart() : "";
            }

            // Now rest = "tableName [AS] [alias] [ON ...]" or "tableName [AS] [alias]"
            // Split on " ON " (case-insensitive, top-level)
            int onPos = FindTokenOutsideParens(rest, " ON ");
            onClause = onPos >= 0 ? rest.Substring(onPos + 4).Trim() : "";
            string tableAndAlias = onPos >= 0 ? rest.Substring(0, onPos).TrimEnd() : rest.TrimEnd();

            // Parse table [AS] alias from tableAndAlias
            ParseTableAndAlias(tableAndAlias, out table, out alias);
        }

        private static void ParseTableAndAlias(string tableAndAlias, out string table, out string alias)
        {
            // Tokens: tableName [AS] [alias]
            // Could be: schema.Table, [schema].[Table], tableName, tableName alias, tableName AS alias
            string upper = tableAndAlias.ToUpperInvariant();
            int asPos = -1;
            // Look for " AS " (with spaces)
            int tryAs = upper.IndexOf(" AS ");
            if (tryAs >= 0)
            {
                table = tableAndAlias.Substring(0, tryAs).Trim();
                alias = tableAndAlias.Substring(tryAs + 4).Trim();
                return;
            }
            // No AS keyword — try two-token split
            var parts = tableAndAlias.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                table = parts[0];
                alias = parts[parts.Length - 1];
            }
            else
            {
                table = tableAndAlias.Trim();
                alias = "";
            }
        }

        /// <summary>
        /// Returns the base name of a (possibly schema-qualified) table name.
        /// e.g. "dbo.Employees" → "Employees", "[dbo].[Employees]" → "Employees"
        /// </summary>
        private static string BaseTableName(string table)
        {
            // Strip schema prefix: take the last dot-segment, remove brackets
            int dot = table.LastIndexOf('.');
            string name = dot >= 0 ? table.Substring(dot + 1) : table;
            return name.Trim('[', ']');
        }

        /// <summary>
        /// Finds the position of a multi-char token in <paramref name="s"/> outside parentheses.
        /// Case-insensitive.
        /// </summary>
        private static int FindTokenOutsideParens(string s, string token)
        {
            int depth = 0;
            string upper = s.ToUpperInvariant();
            string tokenUpper = token.ToUpperInvariant();
            for (int i = 0; i <= s.Length - token.Length; i++)
            {
                char c = s[i];
                if (c == '(') { depth++; continue; }
                if (c == ')') { depth--; continue; }
                if (depth == 0 && upper.Substring(i, token.Length) == tokenUpper)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Splits an ON clause on top-level " AND " separators.
        /// </summary>
        private static List<string> SplitOnConditions(string onClause)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;
            string upper = onClause.ToUpperInvariant();
            for (int i = 0; i <= onClause.Length - 5; i++)
            {
                if (onClause[i] == '(') { depth++; continue; }
                if (onClause[i] == ')') { depth--; continue; }
                if (depth == 0 && upper.Substring(i, 5) == " AND ")
                {
                    result.Add(onClause.Substring(start, i - start).Trim());
                    start = i + 5;
                    i += 4; // skip " AND "
                }
            }
            result.Add(onClause.Substring(start).Trim());
            return result;
        }

        // ----------------------------------------------------------------
        // EnsureColumnAliases: add AS alias to every SELECT column that lacks one
        // ----------------------------------------------------------------

        /// <summary>
        /// Walks the formatted output and ensures every SELECT-list column has an explicit alias.
        /// - Simple column references (optionally table-qualified, bracket-quoted) → base column name.
        /// - Complex expressions (functions, arithmetic, CASE, wildcards) → ColumnAlias_N.
        /// N resets to 1 for each new query (each new top-level SELECT block).
        /// Columns that already have a top-level AS alias are left unchanged.
        /// </summary>
        private string EnsureColumnAliases(string output)
        {
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool inSelectList = false;
            int autoAliasCounter = 0;
            int beginDepth = 0; // tracks BEGIN/END nesting depth
            int parenDepth = 0; // tracks open-paren depth inside a SELECT column expression

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                string trimmedUpper = trimmed.ToUpperInvariant();

                // Track BEGIN/END depth to avoid misidentifying block body lines as SELECT columns.
                // A bare BEGIN (not BEGIN TRANSACTION) increments depth; END decrements.
                if (trimmedUpper == "BEGIN" || trimmedUpper.StartsWith("BEGIN ") || trimmedUpper.StartsWith("BEGIN\t"))
                {
                    // Only count BEGIN that starts a block — not BEGIN TRANSACTION / BEGIN TRAN
                    string afterBegin = trimmedUpper.Length > 5 ? trimmedUpper.Substring(6).TrimStart() : "";
                    if (!afterBegin.StartsWith("TRAN") && !afterBegin.StartsWith("DISTRIBUTED"))
                        beginDepth++;
                    if (inSelectList) { inSelectList = false; parenDepth = 0; } // BEGIN always ends a SELECT list
                    continue;
                }
                if (trimmedUpper == "END" || trimmedUpper.StartsWith("END ") || trimmedUpper.StartsWith("END\t")
                    || trimmedUpper.StartsWith("END;") || trimmedUpper.StartsWith("END,") || trimmedUpper.StartsWith("END)"))
                {
                    if (beginDepth > 0) beginDepth--;
                    if (inSelectList) { inSelectList = false; parenDepth = 0; }
                    continue;
                }

                // If inside a BEGIN/END block, don't process as SELECT column lines.
                if (beginDepth > 0)
                {
                    // Nested SELECT inside a block: handle separately.
                    // Allow SELECT detection to reset inSelectList for nested queries.
                    if (trimmedUpper.StartsWith("SELECT ") || trimmedUpper == "SELECT")
                    {
                        inSelectList = true;
                        autoAliasCounter = 0;
                        parenDepth = 0;
                        int selectEnd = trimmed.IndexOf(' ');
                        if (selectEnd >= 0)
                        {
                            string afterSelect = trimmed.Substring(selectEnd + 1).TrimStart();
                            var (modPrefix1, colPart1, modOnly1) = StripSelectModifierPrefix(afterSelect);
                            if (!modOnly1)
                            {
                                string indent = line.Substring(0, line.Length - trimmed.Length);
                                bool hadTrailingComma1 = colPart1.EndsWith(",");
                                string colExpr1 = hadTrailingComma1 ? colPart1.Substring(0, colPart1.Length - 1).TrimEnd() : colPart1;
                                var (rewritten, newCounter) = EnsureAlias(colExpr1, autoAliasCounter);
                                autoAliasCounter = newCounter;
                                string finalCol1 = rewritten + (hadTrailingComma1 ? "," : "");
                                if (finalCol1 != colPart1)
                                    lines[i] = indent + "SELECT " + modPrefix1 + finalCol1;
                                // Track open parens from the inline column
                                parenDepth += CountNetParens(rewritten != colExpr1 ? rewritten : colExpr1);
                            }
                        }
                        continue;
                    }
                    // Non-SELECT lines inside a block: if we're tracking a SELECT list, process them;
                    // otherwise skip.
                    if (!inSelectList) continue;
                }

                // --- Detect start of SELECT list ---
                if (trimmedUpper.StartsWith("SELECT ") || trimmedUpper == "SELECT")
                {
                    inSelectList = true;
                    autoAliasCounter = 0; // reset counter per query
                    parenDepth = 0;

                    // The SELECT line itself may have first column inline.
                    int selectEnd = trimmed.IndexOf(' ');
                    if (selectEnd >= 0)
                    {
                        string afterSelect = trimmed.Substring(selectEnd + 1).TrimStart();
                        // Strip SELECT modifiers (TOP N / DISTINCT) before alias processing
                        var (modPrefix1, colPart1, modOnly1) = StripSelectModifierPrefix(afterSelect);
                        if (!modOnly1)
                        {
                            string indent = line.Substring(0, line.Length - trimmed.Length);
                            bool hadTrailingComma1 = colPart1.EndsWith(",");
                            string colExpr1 = hadTrailingComma1 ? colPart1.Substring(0, colPart1.Length - 1).TrimEnd() : colPart1;
                            var (rewritten, newCounter) = EnsureAlias(colExpr1, autoAliasCounter);
                            autoAliasCounter = newCounter;
                            string finalCol1 = rewritten + (hadTrailingComma1 ? "," : "");
                            if (finalCol1 != colPart1)
                                lines[i] = indent + "SELECT " + modPrefix1 + finalCol1;
                            parenDepth += CountNetParens(rewritten != colExpr1 ? rewritten : colExpr1);
                        }
                    }
                    continue;
                }

                // --- Detect end of SELECT list ---
                if (inSelectList)
                {
                    // If we're inside an unbalanced open-paren (multi-line expression continuation),
                    // just track paren depth and skip alias processing for this line.
                    if (parenDepth > 0)
                    {
                        parenDepth += CountNetParens(trimmed);
                        if (parenDepth < 0) parenDepth = 0;
                        continue;
                    }

                    bool startsWithComma = trimmed.StartsWith(",");
                    if (!startsWithComma && IsClauseStartLine(trimmedUpper))
                    {
                        inSelectList = false;
                        continue;
                    }

                    if (startsWithComma)
                    {
                        // Leading-comma style: ,expr  or  ,expr AS alias
                        string afterComma = trimmed.Substring(1).TrimStart();
                        int netParens = CountNetParens(afterComma);
                        // Only alias if the expression is balanced (not a multi-line expression start)
                        if (netParens <= 0)
                        {
                            var (rewritten, newCounter) = EnsureAlias(afterComma, autoAliasCounter);
                            autoAliasCounter = newCounter;
                            if (rewritten != afterComma)
                            {
                                string indent = line.Substring(0, line.Length - trimmed.Length);
                                lines[i] = indent + "," + rewritten;
                            }
                        }
                        // Track net parens for multi-line expression detection
                        parenDepth += netParens;
                        if (parenDepth < 0) parenDepth = 0;
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        bool hadTrailingComma = trimmed.EndsWith(",");
                        var (modPrefix2, colExpr2, modOnly2) = StripSelectModifierPrefix(trimmed.TrimEnd(','));
                        if (modOnly2)
                            continue; // pure TOP N or DISTINCT line — not a column, leave as-is

                        int netParens2 = CountNetParens(colExpr2);
                        if (netParens2 <= 0)
                        {
                            var (rewritten, newCounter) = EnsureAlias(colExpr2, autoAliasCounter);
                            autoAliasCounter = newCounter;
                            string indent2 = line.Substring(0, line.Length - trimmed.Length);
                            if (rewritten != colExpr2 || modPrefix2.Length > 0)
                                lines[i] = indent2 + modPrefix2 + rewritten + (hadTrailingComma ? "," : "");
                            parenDepth += netParens2;
                        }
                        else
                        {
                            parenDepth += netParens2;
                        }
                        if (parenDepth < 0) parenDepth = 0;
                    }
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Counts net open parens in a string (open minus close), ignoring parens inside string literals.
        /// Used to detect multi-line expression continuation.
        /// </summary>
        private static int CountNetParens(string s)
        {
            int depth = 0;
            bool inSingleQuote = false;
            bool inBracket = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inSingleQuote)
                {
                    if (c == '\'' && (i + 1 < s.Length && s[i + 1] == '\'')) { i++; continue; } // escaped quote
                    if (c == '\'') inSingleQuote = false;
                    continue;
                }
                if (inBracket)
                {
                    if (c == ']') inBracket = false;
                    continue;
                }
                switch (c)
                {
                    case '\'' : inSingleQuote = true; break;
                    case '[': inBracket = true; break;
                    case '(': depth++; break;
                    case ')': depth--; break;
                }
            }
            return depth;
        }

        /// <summary>
        /// Returns a copy of <paramref name="col"/> with an AS alias appended if none exists.
        /// Also returns the updated autoAliasCounter.
        /// - Leaves unchanged if a top-level AS alias is already present.
        /// - Leaves unchanged if col is a wildcard (* or t.*).
        /// - Simple column ref (e.g. col, t.col, [col], t.[col]) → appends AS &lt;baseName&gt;.
        /// - Complex expression → appends AS ColumnAlias_N (counter incremented).
        /// </summary>
        private static (string result, int counter) EnsureAlias(string col, int counter)
        {
            string trimmed = col.Trim();

            // Empty or comment-only — skip
            if (string.IsNullOrEmpty(trimmed)
                || (trimmed.StartsWith("/*") && trimmed.IndexOf("*/") == trimmed.Length - 2)
                || trimmed.StartsWith("--"))
                return (col, counter);

            // Already has AS alias?
            if (FindAsOutsideParens(trimmed) >= 0)
                return (col, counter);

            // Already has EqualSign-style alias (alias = expr) from input SQL — leave alone
            // Pattern: simple-identifier = anything (not starting with @, which is a var assignment handled below)
            if (!trimmed.StartsWith("@"))
            {
                int eqPos = FindEqualSignOutsideParens(trimmed);
                if (eqPos > 0)
                {
                    string lhs = trimmed.Substring(0, eqPos).Trim();
                    if (IsSimpleColumnRef(lhs))
                        return (col, counter);
                }
            }

            // Variable assignment SELECT @var = expr — not a column alias, leave alone
            if (trimmed.StartsWith("@") && trimmed.IndexOf('=') >= 0)
                return (col, counter);

            // Wildcard — skip
            if (trimmed == "*" || trimmed.EndsWith(".*"))
                return (col, counter);

            // Determine alias
            string alias;
            if (IsSimpleColumnRef(trimmed))
            {
                alias = ExtractColumnBaseName(trimmed);
            }
            else
            {
                counter++;
                alias = "ColumnAlias_" + counter;
            }

            return (trimmed + " AS " + alias, counter);
        }

        /// <summary>
        /// Returns true if <paramref name="expr"/> is a bare column reference:
        /// an identifier (optionally bracket-quoted, optionally table- or schema-qualified)
        /// with no parentheses, spaces, or arithmetic operators.
        /// </summary>
        private static bool IsSimpleColumnRef(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return false;
            // Must not contain parens (function calls), spaces (multi-token expressions),
            // or arithmetic operators at the top level.
            if (expr.IndexOfAny(new[] { '(', ')', ' ', '\t', '+', '-', '/', '%' }) >= 0)
                return false;
            // Allow * only as part of dereference (already handled above for wildcards)
            if (expr.IndexOf('*') >= 0) return false;
            // Must look like: word, word.word, [word], [word].[word], schema.table.col, etc.
            // Split on '.' and check each segment is a valid identifier (possibly bracket-quoted).
            foreach (string segment in expr.Split('.'))
            {
                string s = segment.Trim('[', ']');
                if (string.IsNullOrEmpty(s)) return false;
            }
            return true;
        }

        /// <summary>
        /// Returns the base column name from a possibly schema/table-qualified reference.
        /// E.g. "dbo.Employees.FirstName" → "FirstName", "[MyCol]" → "MyCol".
        /// </summary>
        private static string ExtractColumnBaseName(string expr)
        {
            int dot = expr.LastIndexOf('.');
            string name = dot >= 0 ? expr.Substring(dot + 1) : expr;
            return name.Trim('[', ']');
        }


        /// Only handles lines that are between a SELECT keyword line and the next clause keyword.
        /// </summary>
        private string RewriteAliasesToEqualSign(string output)
        {
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool inSelectList = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                string trimmedUpper = trimmed.ToUpperInvariant();

                // Detect start of SELECT column list
                if (trimmedUpper.StartsWith("SELECT ") || trimmedUpper == "SELECT")
                {
                    inSelectList = true;
                    int selectEnd = trimmed.IndexOf(' ');
                    if (selectEnd >= 0)
                    {
                        string afterSelect = trimmed.Substring(selectEnd + 1).TrimStart();
                        // Strip SELECT modifiers (TOP N / DISTINCT) before rewriting
                        var (modPfx1, colPart1, modOnly1) = StripSelectModifierPrefix(afterSelect);
                        if (!modOnly1)
                        {
                            string indent = line.Substring(0, line.Length - trimmed.Length);
                            bool hadTrailingComma1 = colPart1.EndsWith(",");
                            string colExpr1 = hadTrailingComma1 ? colPart1.Substring(0, colPart1.Length - 1).TrimEnd() : colPart1;
                            string rewritten = TryRewriteColumnLine(colExpr1);
                            string finalCol1 = rewritten + (hadTrailingComma1 ? "," : "");
                            if (finalCol1 != colPart1)
                                lines[i] = indent + "SELECT " + modPfx1 + finalCol1;
                        }
                    }
                    continue;
                }

                // Detect end of SELECT list
                if (inSelectList)
                {
                    bool startsWithComma = trimmed.StartsWith(",");
                    if (!startsWithComma && IsClauseStartLine(trimmedUpper))
                    {
                        inSelectList = false;
                        continue;
                    }

                    if (startsWithComma)
                    {
                        // Leading comma style: ,expr AS alias
                        string afterComma = trimmed.Substring(1).TrimStart();
                        string rewritten = TryRewriteColumnLine(afterComma);
                        if (rewritten != afterComma)
                        {
                            string indent = line.Substring(0, line.Length - trimmed.Length);
                            lines[i] = indent + "," + rewritten;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        bool hadTrailingComma = trimmed.EndsWith(",");
                        var (modPfx2, colExpr2, modOnly2) = StripSelectModifierPrefix(trimmed.TrimEnd(','));
                        if (modOnly2)
                            continue; // pure TOP N or DISTINCT line — not a column, leave as-is

                        string rewritten = TryRewriteColumnLine(colExpr2);
                        string indent2 = line.Substring(0, line.Length - trimmed.Length);
                        if (rewritten != colExpr2 || modPfx2.Length > 0)
                            lines[i] = indent2 + modPfx2 + rewritten + (hadTrailingComma ? "," : "");
                    }
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static bool IsClauseStartLine(string trimmedUpper)
        {
            return trimmedUpper.StartsWith("FROM ") || trimmedUpper == "FROM"
                || trimmedUpper.StartsWith("WHERE ") || trimmedUpper == "WHERE"
                || trimmedUpper.StartsWith("GROUP ") || trimmedUpper == "GROUP"
                || trimmedUpper.StartsWith("ORDER ") || trimmedUpper == "ORDER"
                || trimmedUpper.StartsWith("HAVING ") || trimmedUpper == "HAVING"
                || trimmedUpper.StartsWith("UNION ") || trimmedUpper.StartsWith("INTERSECT ")
                || trimmedUpper.StartsWith("EXCEPT ")
                || trimmedUpper.StartsWith("INTO ")
                || trimmedUpper.StartsWith(")") // subquery end
                // CASE sub-expression keywords — not column expressions
                || trimmedUpper.StartsWith("WHEN ") || trimmedUpper == "WHEN"
                || trimmedUpper.StartsWith("THEN ") || trimmedUpper == "THEN"
                || trimmedUpper.StartsWith("ELSE ") || trimmedUpper == "ELSE"
                || trimmedUpper == "END" || trimmedUpper.StartsWith("END ")
                || trimmedUpper.StartsWith("END,") || trimmedUpper.StartsWith("END)")
                ;
        }

        /// <summary>
        /// Strips a TOP N or DISTINCT prefix from a SELECT column expression.
        /// Returns (prefix, remainder, modifierOnly) where:
        /// - prefix: the stripped modifier text (e.g. "TOP (1000) ") — empty if none
        /// - remainder: the column expression after the modifier
        /// - modifierOnly: true when the line contains only the modifier with no column following
        /// </summary>
        private static bool IsSelectModifierKeyword(string kwUpper) =>
            kwUpper is "DISTINCT" or "TOP" or "PERCENT" or "WITH" or "TIES";

        private static (string prefix, string remainder, bool modifierOnly) StripSelectModifierPrefix(string expr)
        {
            string trimmed = expr.TrimStart();
            int leadingSpaces = expr.Length - trimmed.Length;
            string accumulatedPrefix = expr.Substring(0, leadingSpaces);

            // Loop: keep stripping TOP and DISTINCT modifiers until neither remains.
            while (true)
            {
                string upper = trimmed.ToUpperInvariant();

                // DISTINCT
                if (upper.StartsWith("DISTINCT "))
                {
                    // Preserve original casing from the input string
                    string modToken = trimmed.Substring(0, "DISTINCT".Length);
                    string rest = trimmed.Substring("DISTINCT ".Length).TrimStart();
                    accumulatedPrefix += modToken + " ";
                    trimmed = rest;
                    continue;
                }
                if (upper == "DISTINCT")
                {
                    string modToken = trimmed.Substring(0, "DISTINCT".Length);
                    return (accumulatedPrefix + modToken, "", true);
                }

                // Bare "TOP" with no argument — modifier-only (incomplete TOP clause)
                if (upper == "TOP")
                    return (accumulatedPrefix + trimmed, "", true);

                // TOP (N) or TOP N
                if (upper.StartsWith("TOP ") || upper.StartsWith("TOP("))
                {
                    // Preserve original casing of "TOP"
                    string topWord = trimmed.Substring(0, 3); // "TOP" in original case
                    int pos = trimmed.IndexOf(' '); // index of space after "TOP"
                    string afterTop = trimmed.Substring(pos).TrimStart();
                    string rest;
                    string topToken;
                    if (afterTop.StartsWith("("))
                    {
                        int close = afterTop.IndexOf(')');
                        topToken = afterTop.Substring(0, close + 1);
                        rest = close >= 0 ? afterTop.Substring(close + 1).TrimStart() : "";
                    }
                    else
                    {
                        int spaceAfterN = afterTop.IndexOf(' ');
                        topToken = spaceAfterN >= 0 ? afterTop.Substring(0, spaceAfterN) : afterTop;
                        rest = spaceAfterN >= 0 ? afterTop.Substring(spaceAfterN + 1).TrimStart() : "";
                    }
                    string topPrefix = topWord + " " + topToken + (string.IsNullOrWhiteSpace(rest) ? "" : " ");
                    // Handle PERCENT / WITH TIES (these are terminal — no further modifier stripping)
                    string restUpper = rest.ToUpperInvariant();
                    if (restUpper.StartsWith("PERCENT ")) { topPrefix += "PERCENT "; rest = rest.Substring("PERCENT ".Length).TrimStart(); }
                    else if (restUpper == "PERCENT") return (accumulatedPrefix + topPrefix + "PERCENT", "", true);
                    restUpper = rest.ToUpperInvariant();
                    if (restUpper.StartsWith("WITH TIES")) return (accumulatedPrefix + topPrefix + "WITH TIES", "", true);
                    accumulatedPrefix += topPrefix;
                    trimmed = rest;
                    continue;
                }

                // No more modifiers — done.
                break;
            }

            return (accumulatedPrefix, trimmed, string.IsNullOrWhiteSpace(trimmed));
        }

        /// <summary>
        /// Rewrites a single column expression "expr AS alias" to "alias = expr".
        /// Returns the original string if no " AS " is found at the top level.
        /// </summary>
        private static string TryRewriteColumnLine(string col)
        {
            int asPos = FindAsOutsideParens(col);
            if (asPos < 0) return col;
            string expr = col.Substring(0, asPos).TrimEnd();
            string alias = col.Substring(asPos + 4).Trim();
            if (string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(expr)) return col;
            return alias + " = " + expr;
        }

        /// <summary>
        /// Finds the position of a top-level " AS " (case-insensitive) in a string,
        /// ignoring occurrences inside parentheses or string literals.
        /// Returns -1 if not found.
        /// </summary>
        private static int FindAsOutsideParens(string s)
        {
            int depth = 0;
            bool inString = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (c == '\'') inString = false;
                    continue;
                }
                if (c == '\'')
                {
                    inString = true;
                    continue;
                }
                if (c == '(') { depth++; continue; }
                if (c == ')') { depth--; continue; }
                if (depth == 0 && i + 4 <= s.Length)
                {
                    string sub = s.Substring(i, 4);
                    if (sub.Equals(" AS ", StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Finds the position of a top-level standalone '=' outside parentheses and string literals.
        /// Excludes !=, >=, &lt;= compound operators. Returns -1 if not found.
        /// </summary>
        private static int FindEqualSignOutsideParens(string s)
        {
            int depth = 0;
            bool inString = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inString) { if (c == '\'') inString = false; continue; }
                if (c == '\'') { inString = true; continue; }
                if (c == '(') { depth++; continue; }
                if (c == ')') { depth--; continue; }
                if (depth == 0 && c == '=')
                {
                    if (i > 0 && (s[i - 1] == '!' || s[i - 1] == '>' || s[i - 1] == '<')) continue;
                    if (i + 1 < s.Length && s[i + 1] == '=') continue;
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Pads column expressions in SELECT lists so that AS keywords align vertically.
        /// Works on the full formatted output, only affecting SELECT column list lines.
        /// </summary>
        private string AlignSelectColumns(string output)
        {
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // Find runs of SELECT column lines and align each run.
            int i = 0;
            while (i < lines.Length)
            {
                string trimmed = lines[i].TrimStart().ToUpperInvariant();
                if (trimmed.StartsWith("SELECT ") || trimmed == "SELECT")
                {
                    // Collect the range of lines that are SELECT column list lines.
                    int start = i;
                    // The SELECT line itself may have the first column inline.
                    // Column lines continue until we hit a non-column line.
                    while (i < lines.Length)
                    {
                        string t = lines[i].TrimStart();
                        string tu = t.ToUpperInvariant();
                        if (i > start && !t.StartsWith(",") && IsClauseStartLine(tu))
                            break;
                        i++;
                    }
                    // lines[start..i) is the SELECT block.
                    // Dispatch based on ColumnAliasStyle
                    if (Options.ColumnAliasStyle == ColumnAliasStyle.EqualSign)
                        AlignBlockEqualSign(lines, start, i);
                    else
                        AlignBlockAsKeywords(lines, start, i);
                    continue; // don't increment i again
                }
                i++;
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void AlignBlockAsKeywords(string[] lines, int start, int end)
        {
            // For each line in the block, find the position of " AS " (outside parens).
            // Measure the max expression width and pad.
            var items = new List<(int lineIdx, string indent, string comma, string expr, string alias)>();

            for (int i = start; i < end; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                string indent = line.Substring(0, line.Length - trimmed.Length);

                // Handle SELECT keyword on first line
                string content;
                string prefix;
                if (trimmed.ToUpperInvariant().StartsWith("SELECT "))
                {
                    content = trimmed.Substring(7).TrimStart(); // after "SELECT "
                    prefix = indent + "SELECT ";
                }
                else if (trimmed.StartsWith(","))
                {
                    content = trimmed.Substring(1).TrimStart();
                    prefix = indent + ",";
                }
                else
                {
                    content = trimmed;
                    prefix = indent;
                }

                int asPos = FindAsOutsideParens(content);
                if (asPos >= 0)
                {
                    string expr = content.Substring(0, asPos).TrimEnd();
                    string alias = content.Substring(asPos + 4).Trim();
                    items.Add((i, prefix, "", expr, alias));
                }
            }

            if (items.Count < 2) return;

            int maxExprLen = items.Max(it => it.expr.Length);

            foreach (var (lineIdx, prefix, _, expr, alias) in items)
            {
                string padding = new string(' ', maxExprLen - expr.Length);
                lines[lineIdx] = prefix + expr + padding + " AS " + alias;
            }
        }

        private void AlignBlockEqualSign(string[] lines, int start, int end)
        {
            // For lines in alias = expr format, align the = signs to a tab stop column.
            // Lines look like: indent + [SELECT | ,] + alias + = + expr
            var items = new List<(int lineIdx, string prefix, string alias, string expr)>();

            for (int i = start; i < end; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                string indent = line.Substring(0, line.Length - trimmed.Length);

                // Handle SELECT keyword on first line
                string content;
                string prefix;
                if (trimmed.ToUpperInvariant().StartsWith("SELECT "))
                {
                    content = trimmed.Substring(7).TrimStart(); // after "SELECT "
                    prefix = indent + "SELECT ";
                }
                else if (trimmed.StartsWith(","))
                {
                    content = trimmed.Substring(1).TrimStart();
                    prefix = indent + ",";
                }
                else
                {
                    content = trimmed;
                    prefix = indent;
                }

                int eqPos = FindEqualSignOutsideParens(content);
                if (eqPos >= 0)
                {
                    string alias = content.Substring(0, eqPos).TrimEnd();
                    string expr = content.Substring(eqPos + 1).TrimStart();
                    items.Add((i, prefix, alias, expr));
                }
            }

            if (items.Count < 2) return;

            int maxAliasLen = items.Max(it => it.alias.Length);
            int targetCol = ((maxAliasLen / Options.SpacesPerTab) + 1) * Options.SpacesPerTab;

            foreach (var (lineIdx, prefix, alias, expr) in items)
            {
                int padding = targetCol - alias.Length;
                lines[lineIdx] = prefix + alias + new string(' ', padding) + "= " + expr;
            }
        }

/// <summary>
        /// Aligns DDL column definitions so that column names, data types, and nullability
        /// are in vertical columns.  Input is the formatted content of a DDLDETAIL_PARENS block
        /// (without the surrounding parens).
        /// Each row: indent + [comma] + colname + datatype + [constraint...]
        /// We align: colname and datatype into two columns.
        /// </summary>
        private string AlignDdlColumns(string ddlContent)
        {
            var lines = ddlContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // Each "column definition" line has: indent + optional-comma + name + type + rest
            // We identify lines that look like a column definition (first token is an identifier, second is a data type).
            // For simplicity: lines that aren't blank and don't start with a keyword that's a constraint.
            var colLines = new List<int>();
            var colNames = new List<string>();
            var afterNames = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Strip indent and optional comma
                int pos = 0;
                while (pos < line.Length && (line[pos] == '\t' || line[pos] == ' ')) pos++;
                if (pos >= line.Length) continue;

                bool hasComma = line[pos] == ',';
                if (hasComma) { pos++; while (pos < line.Length && line[pos] == ' ') pos++; }

                // First token: identifier (column name) or bracket-quoted
                string firstToken = ExtractFirstToken(line, pos, out int afterFirst);
                if (string.IsNullOrEmpty(firstToken)) continue;

                // Skip constraint lines (CONSTRAINT, PRIMARY, UNIQUE, etc.)
                string upper = firstToken.TrimStart('[').TrimEnd(']').ToUpperInvariant();
                if (upper == "CONSTRAINT" || upper == "PRIMARY" || upper == "UNIQUE"
                    || upper == "CHECK" || upper == "FOREIGN" || upper == "DEFAULT"
                    || upper == ")") continue;

                colLines.Add(i);
                int indentAndComma = pos;
                colNames.Add(line.Substring(0, afterFirst)); // full prefix including indent+comma+name
                afterNames.Add(line.Substring(afterFirst));
            }

            if (colLines.Count < 2) return ddlContent;

            // Measure max name length (just the name token width, excluding indent+comma)
            int maxNameLen = 0;
            for (int i = 0; i < colLines.Count; i++)
            {
                string fullPrefix = colNames[i];
                // Find just the name part (after last space before the name)
                int nameStart = fullPrefix.Length - 1;
                while (nameStart > 0 && fullPrefix[nameStart - 1] != ' ' && fullPrefix[nameStart - 1] != ','
                    && fullPrefix[nameStart - 1] != '\t')
                    nameStart--;
                string namePart = fullPrefix.Substring(nameStart);
                maxNameLen = Math.Max(maxNameLen, namePart.TrimEnd().Length);
            }

            for (int i = 0; i < colLines.Count; i++)
            {
                string fullPrefix = colNames[i];
                int nameStart = fullPrefix.Length - 1;
                while (nameStart > 0 && fullPrefix[nameStart - 1] != ' ' && fullPrefix[nameStart - 1] != ','
                    && fullPrefix[nameStart - 1] != '\t')
                    nameStart--;
                string beforeName = fullPrefix.Substring(0, nameStart);
                string namePart = fullPrefix.Substring(nameStart).TrimEnd();
                string padding = new string(' ', Math.Max(1, maxNameLen - namePart.Length + 1));
                lines[colLines[i]] = beforeName + namePart + padding + afterNames[i].TrimStart();
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string ExtractFirstToken(string line, int startPos, out int endPos)
        {
            endPos = startPos;
            if (startPos >= line.Length) return string.Empty;

            if (line[startPos] == '[')
            {
                int end = line.IndexOf(']', startPos);
                if (end < 0) { endPos = line.Length; return line.Substring(startPos); }
                endPos = end + 1;
                while (endPos < line.Length && line[endPos] == ' ') endPos++;
                return line.Substring(startPos, end - startPos + 1);
            }

            int tokenEnd = startPos;
            while (tokenEnd < line.Length && line[tokenEnd] != ' ' && line[tokenEnd] != '\t')
                tokenEnd++;
            string token = line.Substring(startPos, tokenEnd - startPos);
            endPos = tokenEnd;
            while (endPos < line.Length && line[endPos] == ' ') endPos++;
            return token;
        }

        private void ProcessSqlNode(Node contentElement, TSqlStandardFormattingState state)
        {
            int initialIndent = state.IndentLevel;

            if (contentElement.GetAttributeValue(SqlStructureConstants.ANAME_HASERROR) == "1")
                state.OpenClass(SqlHtmlConstants.CLASS_ERRORHIGHLIGHT);

            switch (contentElement.Name)
            {
                case SqlStructureConstants.ENAME_SQL_STATEMENT:
                    WhiteSpace_SeparateStatements(contentElement, state);
                    state.ResetKeywords();
                    ProcessSqlNodeList(contentElement.Children, state);
                    state.StatementBreakExpected = true;
                    break;

                case SqlStructureConstants.ENAME_SQL_CLAUSE:
                    state.UnIndentInitialBreak = true;
                    ProcessSqlNodeList(contentElement.Children, state.IncrementIndent());
                    state.DecrementIndent();
                    if (Options.NewClauseLineBreaks > 0)
                        state.BreakExpected = true;
                    if (Options.NewClauseLineBreaks > 1)
                        state.AdditionalBreaksExpected = Options.NewClauseLineBreaks - 1;
                    break;

                case SqlStructureConstants.ENAME_SET_OPERATOR_CLAUSE:
                    state.DecrementIndent();
                    state.WhiteSpace_BreakToNextLine(); //this is the one already recommended by the start of the clause
                    state.WhiteSpace_BreakToNextLine(); //this is the one we additionally want to apply
                    ProcessSqlNodeList(contentElement.Children, state.IncrementIndent());
                    state.BreakExpected = true;
                    state.AdditionalBreaksExpected = 1;
                    break;

                case SqlStructureConstants.ENAME_BATCH_SEPARATOR:
                    //newline regardless of whether previous element recommended a break or not.
                    state.WhiteSpace_BreakToNextLine();
                    ProcessSqlNodeList(contentElement.Children, state);
                    state.BreakExpected = true;
                    break;

                case SqlStructureConstants.ENAME_DDL_PROCEDURAL_BLOCK:
                case SqlStructureConstants.ENAME_DDL_OTHER_BLOCK:
                case SqlStructureConstants.ENAME_DDL_DECLARE_BLOCK:
                case SqlStructureConstants.ENAME_CURSOR_DECLARATION:
                case SqlStructureConstants.ENAME_BEGIN_TRANSACTION:
                case SqlStructureConstants.ENAME_SAVE_TRANSACTION:
                case SqlStructureConstants.ENAME_COMMIT_TRANSACTION:
                case SqlStructureConstants.ENAME_ROLLBACK_TRANSACTION:
                case SqlStructureConstants.ENAME_CONTAINER_OPEN:
                case SqlStructureConstants.ENAME_CONTAINER_CLOSE:
                case SqlStructureConstants.ENAME_WHILE_LOOP:
                case SqlStructureConstants.ENAME_IF_STATEMENT:
                case SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT:
                case SqlStructureConstants.ENAME_CTE_WITH_CLAUSE:
                case SqlStructureConstants.ENAME_PERMISSIONS_BLOCK:
                case SqlStructureConstants.ENAME_PERMISSIONS_DETAIL:
                case SqlStructureConstants.ENAME_MERGE_CLAUSE:
                case SqlStructureConstants.ENAME_MERGE_TARGET:
                    ProcessSqlNodeList(contentElement.Children, state);
                    break;

                case SqlStructureConstants.ENAME_SELECTIONTARGET:
                    ProcessSqlNodeList(contentElement.Children, state);
                    break;

                case SqlStructureConstants.ENAME_CASE_INPUT:
                case SqlStructureConstants.ENAME_BOOLEAN_EXPRESSION:
                case SqlStructureConstants.ENAME_BETWEEN_LOWERBOUND:
                case SqlStructureConstants.ENAME_BETWEEN_UPPERBOUND:
                    WhiteSpace_SeparateWords(state);
                    ProcessSqlNodeList(contentElement.Children, state);
                    break;

                case SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT:
                case SqlStructureConstants.ENAME_CONTAINER_MULTISTATEMENT:
                case SqlStructureConstants.ENAME_MERGE_ACTION:

                    bool singleStatementIsIf = false;
                    foreach (Node statement in contentElement.ChildrenByName(SqlStructureConstants.ENAME_SQL_STATEMENT))
                        foreach (Node clause in statement.ChildrenByName(SqlStructureConstants.ENAME_SQL_CLAUSE))
                            foreach (Node ifStatement in clause.ChildrenByName(SqlStructureConstants.ENAME_IF_STATEMENT))
                                singleStatementIsIf = true;

					if (singleStatementIsIf && contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_ELSE_CLAUSE))
					{
						//artificially decrement indent and skip new statement break for "ELSE IF" constructs
						state.DecrementIndent();
					}
					else
					{
						state.BreakExpected = true;
					}
                    ProcessSqlNodeList(contentElement.Children, state);
					if (singleStatementIsIf && contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_ELSE_CLAUSE))
					{
						//bring indent back to symmetrical level
						state.IncrementIndent();
					}

					state.StatementBreakExpected = false; //the responsibility for breaking will be with the OUTER statement; there should be no consequence propagating out from statements in this container;
                    state.UnIndentInitialBreak = false; //if there was no word spacing after the last content statement's clause starter, doesn't mean the unIndent should propagate to the following content!
                    break;

                case SqlStructureConstants.ENAME_PERMISSIONS_TARGET:
                case SqlStructureConstants.ENAME_PERMISSIONS_RECIPIENT:
                case SqlStructureConstants.ENAME_DDL_WITH_CLAUSE:
                case SqlStructureConstants.ENAME_MERGE_CONDITION:
                case SqlStructureConstants.ENAME_MERGE_THEN:
                    state.BreakExpected = true;
                    state.UnIndentInitialBreak = true;
                    ProcessSqlNodeList(contentElement.Children, state.IncrementIndent());
                    state.DecrementIndent();
                    break;

                case SqlStructureConstants.ENAME_JOIN_ON_SECTION:
                    if (Options.BreakJoinOnSections)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state);
                    if (Options.BreakJoinOnSections)
                        state.IncrementIndent();
                    if (Options.IndentJoinOnClause)
                    {
                        state.BreakExpected = true;
                        state.IncrementIndent();
                    }
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT), state);
                    if (Options.IndentJoinOnClause)
                        state.DecrementIndent();
                    if (Options.BreakJoinOnSections)
                        state.DecrementIndent();
                    break;

                case SqlStructureConstants.ENAME_CTE_ALIAS:
                    state.UnIndentInitialBreak = true;
                    ProcessSqlNodeList(contentElement.Children, state);
                    break;

                case SqlStructureConstants.ENAME_ELSE_CLAUSE:
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state.DecrementIndent());
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT), state.IncrementIndent());
                    break;

                case SqlStructureConstants.ENAME_DDL_AS_BLOCK:
                case SqlStructureConstants.ENAME_CURSOR_FOR_BLOCK:
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state.DecrementIndent());
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT), state);
                    state.IncrementIndent();
                    break;

                case SqlStructureConstants.ENAME_TRIGGER_CONDITION:
                    state.DecrementIndent();
                    state.WhiteSpace_BreakToNextLine();
                    ProcessSqlNodeList(contentElement.Children, state.IncrementIndent());
                    break;

                case SqlStructureConstants.ENAME_CURSOR_FOR_OPTIONS:
                case SqlStructureConstants.ENAME_CTE_AS_BLOCK:
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state.DecrementIndent());
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT), state.IncrementIndent());
                    break;

                case SqlStructureConstants.ENAME_DDL_RETURNS:
                case SqlStructureConstants.ENAME_MERGE_USING:
                case SqlStructureConstants.ENAME_MERGE_WHEN:
                    state.BreakExpected = true;
                    state.UnIndentInitialBreak = true;
                    ProcessSqlNodeList(contentElement.Children, state);
                    break;

                case SqlStructureConstants.ENAME_BETWEEN_CONDITION:
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state);
                    state.IncrementIndent();
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_BETWEEN_LOWERBOUND), state.IncrementIndent());
                    if (Options.ExpandBetweenConditions)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_CLOSE), state.DecrementIndent());
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_BETWEEN_UPPERBOUND), state.IncrementIndent());
                    state.DecrementIndent();
                    state.DecrementIndent();
                    break;

                case SqlStructureConstants.ENAME_DDLDETAIL_PARENS:
                case SqlStructureConstants.ENAME_FUNCTION_PARENS:
					//simply process sub-nodes - don't add space or expect any linebreaks (but respect linebreaks if necessary)
                    state.WordSeparatorExpected = false;
                    WhiteSpace_BreakAsExpected(state);
                    state.AddOutputContent(FormatOperator("("), SqlHtmlConstants.CLASS_OPERATOR);
                    ProcessSqlNodeList(contentElement.Children, state.IncrementIndent());
                    state.DecrementIndent();
                    WhiteSpace_BreakAsExpected(state);
                    state.AddOutputContent(FormatOperator(")"), SqlHtmlConstants.CLASS_OPERATOR);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_DDL_PARENS:
                case SqlStructureConstants.ENAME_EXPRESSION_PARENS:
                case SqlStructureConstants.ENAME_SELECTIONTARGET_PARENS:
				case SqlStructureConstants.ENAME_IN_PARENS:
                    // End modifier zone unless this is the TOP (N) argument — EXPRESSION_PARENS
                    // immediately after TOP stays on the SELECT line.
                    if (state.InSelectModifierZone)
                    {
                        if (contentElement.Name == SqlStructureConstants.ENAME_EXPRESSION_PARENS
                            && state.TopArgumentExpected)
                        {
                            state.TopArgumentExpected = false;
                        }
                        else
                        {
                            state.InSelectModifierZone = false;
                            state.TopArgumentExpected = false;
                            state.BreakExpected = true;
                        }
                    }
					WhiteSpace_SeparateWords(state);
					if (contentElement.Name.Equals(SqlStructureConstants.ENAME_EXPRESSION_PARENS) || contentElement.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS))
                        state.IncrementIndent();
                    state.AddOutputContent(FormatOperator("("), SqlHtmlConstants.CLASS_OPERATOR);
                    TSqlStandardFormattingState innerState = new TSqlStandardFormattingState(state);
                    ProcessSqlNodeList(contentElement.Children, innerState);
                    //if there was a linebreak in the parens content, or if it wanted one to follow, then put linebreaks before and after.
                    if (innerState.BreakExpected || innerState.OutputContainsLineBreak)
                    {
                        if (!innerState.StartsWithBreak)
                            state.WhiteSpace_BreakToNextLine();
                        // Apply DDL column alignment post-processing if enabled.
                        if (Options.AlignColumnDefinitionsInDDL
                            && contentElement.Name == SqlStructureConstants.ENAME_DDL_PARENS)
                        {
                            state.AddOutputContentRaw(AlignDdlColumns(innerState.DumpOutput()));
                        }
                        else
                        {
                            state.Assimilate(innerState);
                        }
                        state.WhiteSpace_BreakToNextLine();
                    }
                    else
                    {
                        state.Assimilate(innerState);
                    }
                    state.AddOutputContent(FormatOperator(")"), SqlHtmlConstants.CLASS_OPERATOR);
                    if (contentElement.Name.Equals(SqlStructureConstants.ENAME_EXPRESSION_PARENS) || contentElement.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS))
                        state.DecrementIndent();
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_BEGIN_END_BLOCK:
                case SqlStructureConstants.ENAME_TRY_BLOCK:
                case SqlStructureConstants.ENAME_CATCH_BLOCK:
                    if (contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_CLAUSE)
                        && contentElement.Parent.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_STATEMENT)
                        && contentElement.Parent.Parent.Parent.Name.Equals(SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT)
                        )
                        state.DecrementIndent();
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state);
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_MULTISTATEMENT), state);
                    state.DecrementIndent();
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_CLOSE), state);
                    state.IncrementIndent();
                    if (contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_CLAUSE)
                        && contentElement.Parent.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_STATEMENT)
                        && contentElement.Parent.Parent.Parent.Name.Equals(SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT)
                        )
                        state.IncrementIndent();
                    break;

                case SqlStructureConstants.ENAME_CASE_STATEMENT:
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state);
                    state.IncrementIndent();
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CASE_INPUT), state);
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CASE_WHEN), state);
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CASE_ELSE), state);
                    if (Options.ExpandCaseStatements)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_CLOSE), state);
                    state.DecrementIndent();
                    break;

                case SqlStructureConstants.ENAME_CASE_WHEN:
                case SqlStructureConstants.ENAME_CASE_THEN:
                case SqlStructureConstants.ENAME_CASE_ELSE:
                    if (Options.ExpandCaseStatements)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state);
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_GENERALCONTENT), state.IncrementIndent());
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CASE_THEN), state);
                    state.DecrementIndent();
                    break;

                case SqlStructureConstants.ENAME_AND_OPERATOR:
                case SqlStructureConstants.ENAME_OR_OPERATOR:
                    if (Options.ExpandBooleanExpressions)
                        state.BreakExpected = true;
                    if (Options.IndentWhereAndOrConditions)
                        state.IncrementIndent();
                    ProcessSqlNode(contentElement.ChildByName(SqlStructureConstants.ENAME_OTHERKEYWORD), state);
                    if (Options.IndentWhereAndOrConditions)
                        state.DecrementIndent();
                    break;

                case SqlStructureConstants.ENAME_COMMENT_MULTILINE:
                    if (state.SpecialRegionActive == SpecialRegionType.NoFormat && contentElement.TextValue.ToUpperInvariant().Contains("[/NOFORMAT]"))
                    {
                        Node? skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlIdentityFormatter tempFormatter = new TSqlIdentityFormatter(Options.HTMLColoring);
                            state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
                            state.WordSeparatorExpected = false;
                            state.BreakExpected = false;
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }
                    else if (state.SpecialRegionActive == SpecialRegionType.Minify && contentElement.TextValue.ToUpperInvariant().Contains("[/MINIFY]"))
                    {
                        Node? skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlObfuscatingFormatter tempFormatter = new TSqlObfuscatingFormatter();
                            if (HTMLFormatted)
                                state.AddOutputContentRaw(Utils.HtmlEncode(tempFormatter.FormatSQLTree(skippedXml)));
                            else
                                state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
                            state.WordSeparatorExpected = false;
                            state.BreakExpected = false;
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }

                    WhiteSpace_SeparateComment(contentElement, state);
                    state.AddOutputContent("/*" + contentElement.TextValue + "*/", SqlHtmlConstants.CLASS_COMMENT);
                    if (contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_STATEMENT)
                        || (contentElement.NextSibling() != null
                            && contentElement.NextSibling().Name.Equals(SqlStructureConstants.ENAME_WHITESPACE)
                            && Regex.IsMatch(contentElement.NextSibling().TextValue, @"(\r|\n)+")
                            )
                        )
                        //if this block comment is at the start or end of a statement, or if it was followed by a 
                        // linebreak before any following content, then break here.
                        state.BreakExpected = true;
                    else
                    {
                        state.WordSeparatorExpected = true;
                    }

                    if (state.SpecialRegionActive == null && contentElement.TextValue.ToUpperInvariant().Contains("[NOFORMAT]"))
                    {
                        //state.AddOutputLineBreak();
                        state.SpecialRegionActive = SpecialRegionType.NoFormat;
                        state.RegionStartNode = contentElement;
                    }
                    else if (state.SpecialRegionActive == null && contentElement.TextValue.ToUpperInvariant().Contains("[MINIFY]"))
                    {
                        //state.AddOutputLineBreak();
                        state.SpecialRegionActive = SpecialRegionType.Minify;
                        state.RegionStartNode = contentElement;
                    }
                    break;

                case SqlStructureConstants.ENAME_COMMENT_SINGLELINE:
                case SqlStructureConstants.ENAME_COMMENT_SINGLELINE_CSTYLE:
                    if (state.SpecialRegionActive == SpecialRegionType.NoFormat && contentElement.TextValue.ToUpperInvariant().Contains("[/NOFORMAT]"))
                    {
                        Node? skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlIdentityFormatter tempFormatter = new TSqlIdentityFormatter(Options.HTMLColoring);
                            state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
                            state.WordSeparatorExpected = false;
                            state.BreakExpected = false;
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }
                    else if (state.SpecialRegionActive == SpecialRegionType.Minify && contentElement.TextValue.ToUpperInvariant().Contains("[/MINIFY]"))
                    {
                        Node? skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlObfuscatingFormatter tempFormatter = new TSqlObfuscatingFormatter();
                            if (HTMLFormatted)
                                state.AddOutputContentRaw(Utils.HtmlEncode(tempFormatter.FormatSQLTree(skippedXml)));
                            else
                                state.AddOutputContentRaw(tempFormatter.FormatSQLTree(skippedXml));
                            state.WordSeparatorExpected = false;
                            state.BreakExpected = false;
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }

                    WhiteSpace_SeparateComment(contentElement, state);
                    state.AddOutputContent((contentElement.Name == SqlStructureConstants.ENAME_COMMENT_SINGLELINE ? "--" : "//") + contentElement.TextValue.Replace("\r", "").Replace("\n", ""), SqlHtmlConstants.CLASS_COMMENT);
                    state.BreakExpected = true;
                    state.SourceBreakPending = true;

                    if (state.SpecialRegionActive == null && contentElement.TextValue.ToUpperInvariant().Contains("[NOFORMAT]"))
                    {
                        state.AddOutputLineBreak();
                        state.SpecialRegionActive = SpecialRegionType.NoFormat;
                        state.RegionStartNode = contentElement;
                    }
                    else if (state.SpecialRegionActive == null && contentElement.TextValue.ToUpperInvariant().Contains("[MINIFY]"))
                    {
                        state.AddOutputLineBreak();
                        state.SpecialRegionActive = SpecialRegionType.Minify;
                        state.RegionStartNode = contentElement;
                    }
                    break;

                case SqlStructureConstants.ENAME_STRING:
                case SqlStructureConstants.ENAME_NSTRING:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    string outValue = null;
                    if (contentElement.Name.Equals(SqlStructureConstants.ENAME_NSTRING))
                        outValue = "N'" + contentElement.TextValue.Replace("'", "''") + "'";
                    else
                        outValue = "'" + contentElement.TextValue.Replace("'", "''") + "'";
                    state.AddOutputContent(outValue, SqlHtmlConstants.CLASS_STRING);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent("[" + contentElement.TextValue.Replace("]", "]]") + "]");
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_QUOTED_STRING:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent("\"" + contentElement.TextValue.Replace("\"", "\"\"") + "\"");
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_COMMA:
                    //comma always ignores requested word spacing
                    if (Options.TrailingCommas)
                    {
                        WhiteSpace_BreakAsExpected(state);
                        state.AddOutputContent(FormatOperator(","), SqlHtmlConstants.CLASS_OPERATOR);

                        if ((Options.ExpandCommaLists
								&& !(contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_DDLDETAIL_PARENS)
									|| contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_FUNCTION_PARENS)
									|| contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
									)
								)
							|| (Options.ExpandInLists
								&& contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
								)
							)
                            state.BreakExpected = true;
                        else
                            state.WordSeparatorExpected = true;
                    }
                    else
                    {
                        if ((Options.ExpandCommaLists
								&& !(contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_DDLDETAIL_PARENS)
									|| contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_FUNCTION_PARENS)
									|| contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
									)
								)
							|| (Options.ExpandInLists
								&& contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
								)
							)
                        {
                            state.WhiteSpace_BreakToNextLine();
                            state.AddOutputContent(FormatOperator(","), SqlHtmlConstants.CLASS_OPERATOR);
                            if (Options.SpaceAfterExpandedComma)
                                state.WordSeparatorExpected = true;
                        }
                        else
                        {
                            WhiteSpace_BreakAsExpected(state);
                            state.AddOutputContent(FormatOperator(","), SqlHtmlConstants.CLASS_OPERATOR);
                            state.WordSeparatorExpected = true;
                        }

                    }
                    break;

                case SqlStructureConstants.ENAME_PERIOD:
                case SqlStructureConstants.ENAME_SEMICOLON:
                case SqlStructureConstants.ENAME_SCOPERESOLUTIONOPERATOR:
                    //always ignores requested word spacing, and doesn't request a following space either.
                    state.WordSeparatorExpected = false;
                    WhiteSpace_BreakAsExpected(state);
                    state.AddOutputContent(FormatOperator(contentElement.TextValue), SqlHtmlConstants.CLASS_OPERATOR);
                    break;

                case SqlStructureConstants.ENAME_ASTERISK:
                case SqlStructureConstants.ENAME_EQUALSSIGN:
                case SqlStructureConstants.ENAME_ALPHAOPERATOR:
                case SqlStructureConstants.ENAME_OTHEROPERATOR:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent(FormatOperator(contentElement.TextValue), SqlHtmlConstants.CLASS_OPERATOR);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_COMPOUNDKEYWORD:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.SetRecentKeyword(contentElement.GetAttributeValue(SqlStructureConstants.ANAME_SIMPLETEXT));
                    state.AddOutputContent(FormatKeyword(contentElement.GetAttributeValue(SqlStructureConstants.ANAME_SIMPLETEXT)), SqlHtmlConstants.CLASS_KEYWORD);
                    state.WordSeparatorExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByNames(SqlStructureConstants.ENAMELIST_COMMENT), state.IncrementIndent());
                    state.DecrementIndent();
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_OTHERKEYWORD:
                case SqlStructureConstants.ENAME_DATATYPE_KEYWORD:
                {
                    string kwUpper = contentElement.TextValue.ToUpperInvariant();

                    // DDLConstraintsOnNewLine: force a line break before constraint-starting keywords
                    // inside a DDL column list (DDL_PARENS context), but only when the keyword is
                    // NOT the first content item on the line (skip table-level constraints that
                    // already start on their own comma-separated line).
                    if (Options.DDLConstraintsOnNewLine
                        && (kwUpper == "CONSTRAINT" || kwUpper == "CHECK"
                            || kwUpper == "DEFAULT" || kwUpper == "REFERENCES")
                        && IsInsideDdlDetailParens(contentElement))
                    {
                        // Don't add extra break if the nearest preceding non-whitespace sibling is a comma
                        // (that means this constraint is already on its own comma-separated line).
                        Node prev = contentElement.PreviousSibling();
                        while (prev != null && prev.Name == SqlStructureConstants.ENAME_WHITESPACE)
                            prev = prev.PreviousSibling();
                        if (prev == null || prev.Name != SqlStructureConstants.ENAME_COMMA)
                            state.BreakExpected = true;
                    }

                    // If we're in the SELECT modifier zone and this keyword is NOT a modifier
                    // (DISTINCT/TOP/PERCENT/WITH/TIES), end the zone and arm the break so this
                    // token becomes the first column on a new line.
                    if (state.InSelectModifierZone && !IsSelectModifierKeyword(kwUpper))
                    {
                        state.InSelectModifierZone = false;
                        state.TopArgumentExpected = false;
                        state.BreakExpected = true;
                    }

                    WhiteSpace_SeparateWords(state);
                    state.SetRecentKeyword(contentElement.TextValue);
                    state.AddOutputContent(FormatKeyword(contentElement.TextValue), SqlHtmlConstants.CLASS_KEYWORD);
                    state.WordSeparatorExpected = true;

                    if (state.InSelectModifierZone && kwUpper == "TOP")
                        state.TopArgumentExpected = true;

                    // When SelectFirstColumnOnNewLine is on and this is a SELECT keyword, enter the
                    // modifier zone: suppress breaks for DISTINCT/TOP/etc. and arm one for the first
                    // real column instead.
                    if (Options.SelectFirstColumnOnNewLine
                        && Options.ExpandCommaLists
                        && kwUpper == "SELECT")
                        state.InSelectModifierZone = true;

                    break;
                }

                case SqlStructureConstants.ENAME_PSEUDONAME:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent(FormatKeyword(contentElement.TextValue), SqlHtmlConstants.CLASS_KEYWORD);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_FUNCTION_KEYWORD:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.SetRecentKeyword(contentElement.TextValue);
                    state.AddOutputContent(FormatKeyword(contentElement.TextValue), SqlHtmlConstants.CLASS_FUNCTION);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_OTHERNODE:
                case SqlStructureConstants.ENAME_MONETARY_VALUE:
                case SqlStructureConstants.ENAME_LABEL:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent(contentElement.TextValue);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_NUMBER_VALUE:
                    // In modifier zone: a bare number is the TOP N count — stay on SELECT line.
                    // Anything else (rare literal-number first column) gets the break.
                    if (state.InSelectModifierZone)
                    {
                        if (state.TopArgumentExpected)
                            state.TopArgumentExpected = false;
                        else
                        {
                            state.InSelectModifierZone = false;
                            state.BreakExpected = true;
                        }
                    }
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent(contentElement.TextValue.ToLowerInvariant());
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_BINARY_VALUE:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent("0x");
                    state.AddOutputContent(contentElement.TextValue.Substring(2).ToUpperInvariant());
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_WHITESPACE:
                    //take note if it's a line-breaking space, but don't DO anything here
                    if (Regex.IsMatch(contentElement.TextValue, @"(\r|\n)+"))
                        state.SourceBreakPending = true;
                    break;
                default:
                    throw new Exception("Unrecognized element in SQL Xml!");
            }

            if (contentElement.GetAttributeValue(SqlStructureConstants.ANAME_HASERROR) == "1")
                state.CloseClass();

            if (initialIndent != state.IndentLevel)
                throw new Exception("Messed up the indenting!! Check code/stack or panic!");
        }


        private string FormatKeyword(string keyword)
        {
            string outputKeyword;
            if (!KeywordMapping.TryGetValue(keyword.ToUpperInvariant(), out outputKeyword))
                outputKeyword = keyword;

            if (Options.UppercaseKeywords)
                return outputKeyword.ToUpperInvariant();
            else
                return outputKeyword.ToLowerInvariant();
        }

        private string FormatOperator(string operatorValue)
        {
            if (Options.UppercaseKeywords)
                return operatorValue.ToUpperInvariant();
            else
                return operatorValue.ToLowerInvariant();
        }

        private void WhiteSpace_SeparateStatements(Node contentElement, TSqlStandardFormattingState state)
        {
            if (state.StatementBreakExpected)
            {
                //check whether this is a DECLARE/SET clause with similar precedent, and therefore exempt from double-linebreak.
                Node thisClauseStarter = FirstSemanticElementChild(contentElement);
				if (!(thisClauseStarter != null
					&& thisClauseStarter.Name.Equals(SqlStructureConstants.ENAME_OTHERKEYWORD)
					&& state.GetRecentKeyword() != null
					&& ((thisClauseStarter.TextValue.ToUpperInvariant().Equals("SET")
							&& state.GetRecentKeyword().Equals("SET")
							)
						|| (thisClauseStarter.TextValue.ToUpperInvariant().Equals("DECLARE")
							&& state.GetRecentKeyword().Equals("DECLARE")
							)
						|| (thisClauseStarter.TextValue.ToUpperInvariant().Equals("PRINT")
							&& state.GetRecentKeyword().Equals("PRINT")
							)
						)
					))
				{
					for (int i = Options.NewStatementLineBreaks; i > 0; i--)
						state.AddOutputLineBreak();
				}
				else
				{
					for (int i = Options.NewClauseLineBreaks; i > 0; i--)
						state.AddOutputLineBreak();
				}

                state.Indent(state.IndentLevel);
                state.BreakExpected = false;
				state.AdditionalBreaksExpected = 0;
                state.SourceBreakPending = false;
                state.StatementBreakExpected = false;
                state.WordSeparatorExpected = false;
            }
        }

        private Node FirstSemanticElementChild(Node contentElement)
        {
            Node target = null;
            while (contentElement != null)
            {
                target = contentElement.ChildrenExcludingNames(SqlStructureConstants.ENAMELIST_NONCONTENT).FirstOrDefault();

                if (target != null && SqlStructureConstants.ENAMELIST_NONSEMANTICCONTENT.Contains(target.Name))
                    contentElement = target;
                else
                    contentElement = null;
            }

            return target;
        }

        private void WhiteSpace_SeparateWords(TSqlStandardFormattingState state)
        {
            if (state.BreakExpected || state.AdditionalBreaksExpected > 0)
            {
                bool wasUnIndent = state.UnIndentInitialBreak;
                if (wasUnIndent) state.DecrementIndent();
                WhiteSpace_BreakAsExpected(state);
                if (wasUnIndent) state.IncrementIndent();
            }
            else if (state.WordSeparatorExpected)
            {
                state.AddOutputSpace();
            }
            state.UnIndentInitialBreak = false;
            state.SourceBreakPending = false;
            state.WordSeparatorExpected = false;
        }

        private void WhiteSpace_SeparateComment(Node contentElement, TSqlStandardFormattingState state)
        {
            if (state.CurrentLineHasContent && state.SourceBreakPending)
            {
                state.BreakExpected = true;
                WhiteSpace_BreakAsExpected(state);
            }
            else if (state.WordSeparatorExpected)
                state.AddOutputSpace();
            state.SourceBreakPending = false;
            state.WordSeparatorExpected = false;
        }

        private void WhiteSpace_BreakAsExpected(TSqlStandardFormattingState state)
        {
            if (state.BreakExpected)
                state.WhiteSpace_BreakToNextLine();
            while (state.AdditionalBreaksExpected > 0)
            {
                state.WhiteSpace_BreakToNextLine();
                state.AdditionalBreaksExpected--;
            }
        }

        class TSqlStandardFormattingState : BaseFormatterState
        {
            //normal constructor
            public TSqlStandardFormattingState(bool htmlOutput, string indentString, int spacesPerTab, int maxLineWidth, int initialIndentLevel)
                : base(htmlOutput)
            {
                IndentLevel = initialIndentLevel;
                HtmlOutput = htmlOutput;
                IndentString = indentString;
				MaxLineWidth = maxLineWidth;

                int tabCount = indentString.Split('\t').Length - 1;
                int tabExtraCharacters = tabCount * (spacesPerTab - 1);
                IndentLength = indentString.Length + tabExtraCharacters;
            }

            //special "we want isolated state, but inheriting existing conditions" constructor
            public TSqlStandardFormattingState(TSqlStandardFormattingState sourceState)
                : base(sourceState.HtmlOutput)
            {
                IndentLevel = sourceState.IndentLevel;
                HtmlOutput = sourceState.HtmlOutput;
                IndentString = sourceState.IndentString;
                IndentLength = sourceState.IndentLength;
                MaxLineWidth = sourceState.MaxLineWidth;
				//TODO: find a way out of the cross-dependent wrapping maze...
                //CurrentLineLength = sourceState.CurrentLineLength;
                CurrentLineLength = IndentLevel * IndentLength;
                CurrentLineHasContent = sourceState.CurrentLineHasContent;
            }

            private string IndentString { get; set; }
            private int IndentLength { get; set; }
            private int MaxLineWidth { get; set; }

            public bool StatementBreakExpected { get; set; }
            public bool BreakExpected { get; set; }
            public bool InSelectModifierZone { get; set; }
            public bool TopArgumentExpected { get; set; }
            public bool WordSeparatorExpected { get; set; }
            public bool SourceBreakPending { get; set; }
            public int AdditionalBreaksExpected { get; set; }

            public bool UnIndentInitialBreak { get; set; }
            public int IndentLevel { get; private set; }
            public int CurrentLineLength { get; private set; }
            public bool CurrentLineHasContent { get; private set; }

            public SpecialRegionType? SpecialRegionActive { get; set; }
            public Node RegionStartNode { get; set; }

            private static Regex _startsWithBreakChecker = new Regex(@"^\s*(\r|\n)", RegexOptions.None);
            public bool StartsWithBreak
            {
                get
                {
                    return _startsWithBreakChecker.IsMatch(_outBuilder.ToString());
                }
            }

            public override void AddOutputContent(string content)
            {
                if (SpecialRegionActive == null)
                    AddOutputContent(content, null);
            }

            public override void AddOutputContent(string content, string htmlClassName)
            {
                if (CurrentLineHasContent && (content.Length + CurrentLineLength > MaxLineWidth))
                    WhiteSpace_BreakToNextLine();

                if (SpecialRegionActive == null)
                    base.AddOutputContent(content, htmlClassName);

                CurrentLineHasContent = true;
                CurrentLineLength += content.Length;
            }

            public override void AddOutputLineBreak()
            {
#if DEBUG
                //hints for debugging line-width issues:
                //_outBuilder.Append(" (" + CurrentLineLength.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
#endif

                //if linebreaks are added directly in the content (eg in comments or strings), they 
                // won't be accounted for here - that's ok.
                if (SpecialRegionActive == null)
                    base.AddOutputLineBreak();
                CurrentLineLength = 0;
                CurrentLineHasContent = false;
            }

            internal void AddOutputSpace()
            {
                if (SpecialRegionActive == null)
                    _outBuilder.Append(" ");
            }

            public void Indent(int indentLevel)
            {
                for (int i = 0; i < indentLevel; i++)
                {
                    if (SpecialRegionActive == null)
                        base.AddOutputContent(IndentString, ""); //that is, add the indent as HTMLEncoded content if necessary, but no weird linebreak-adding
                    CurrentLineLength += IndentLength;
                }
            }

            internal void WhiteSpace_BreakToNextLine()
            {
                AddOutputLineBreak();
                Indent(IndentLevel);
                BreakExpected = false;
                SourceBreakPending = false;
                WordSeparatorExpected = false;
            }

            //for linebreak detection, use actual string content rather than counting "AddOutputLineBreak()" calls,
            // because we also want to detect the content of strings and comments.
#if SIMPLIFIEDFW
            private static Regex _lineBreakMatcher = new Regex(@"(\r|\n)+");
#else
            private static Regex _lineBreakMatcher = new Regex(@"(\r|\n)+", RegexOptions.Compiled);
#endif

            public bool OutputContainsLineBreak { get { return _lineBreakMatcher.IsMatch(_outBuilder.ToString()); } }

            public void Assimilate(TSqlStandardFormattingState partialState)
            {
                //TODO: find a way out of the cross-dependent wrapping maze...
                CurrentLineLength = CurrentLineLength + partialState.CurrentLineLength;
                CurrentLineHasContent = CurrentLineHasContent || partialState.CurrentLineHasContent;
                if (SpecialRegionActive == null)
                    _outBuilder.Append(partialState.DumpOutput());
            }


            private Dictionary<int, string> _mostRecentKeywordsAtEachLevel = new Dictionary<int, string>();

            public TSqlStandardFormattingState IncrementIndent()
            {
                IndentLevel++;
                return this;
            }

            public TSqlStandardFormattingState DecrementIndent()
            {
                IndentLevel--;
                return this;
            }

            public void SetRecentKeyword(string ElementName)
            {
                if (!_mostRecentKeywordsAtEachLevel.ContainsKey(IndentLevel))
                    _mostRecentKeywordsAtEachLevel.Add(IndentLevel, ElementName.ToUpperInvariant());
            }

            public string GetRecentKeyword()
            {
                string keywordFound = null;
                int? keywordFoundAt = null;
                foreach (int key in _mostRecentKeywordsAtEachLevel.Keys)
                {
                    if ((keywordFoundAt == null || keywordFoundAt.Value > key) && key >= IndentLevel)
                    {
                        keywordFoundAt = key;
                        keywordFound = _mostRecentKeywordsAtEachLevel[key];
                    }
                }
                return keywordFound;
            }

            public void ResetKeywords()
            {
                List<int> descendentLevelKeys = new List<int>();
                foreach (int key in _mostRecentKeywordsAtEachLevel.Keys)
                    if (key >= IndentLevel)
                        descendentLevelKeys.Add(key);
                foreach (int key in descendentLevelKeys)
                    _mostRecentKeywordsAtEachLevel.Remove(key);
            }
        }

        public enum SpecialRegionType
        {
            NoFormat = 1,
            Minify = 2
        }
    }
}
