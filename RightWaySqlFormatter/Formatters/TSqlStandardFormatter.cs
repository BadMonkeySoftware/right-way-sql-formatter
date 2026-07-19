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
                        state.AddOutputContentRaw(Utils.HtmlEncode(tempFormatter.FormatSQLTree(skippedXml))!);
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
                output = WrapOverflowingAliasLiterals(output);
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
        /// <summary>
        /// For each output line, true when the line STARTS inside an open single-quoted
        /// string literal (multi-line dynamic SQL) or an open /* block comment */.
        /// The text-based post-processing passes must leave such lines untouched:
        /// "SELECT ..." inside a dynamic-SQL string is DATA, and rewriting it corrupts
        /// the SQL's meaning. Bracket-quoted identifiers and -- comments are handled
        /// within each line; block comments nest, per T-SQL rules.
        /// </summary>
        private static bool[] ComputeLinesInsideStringOrComment(IList<string> lines)
        {
            bool[] mask = new bool[lines.Count];
            bool inString = false;
            int commentDepth = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                mask[i] = inString || commentDepth > 0;
                string line = lines[i];
                for (int j = 0; j < line.Length; j++)
                {
                    char c = line[j];
                    char next = j + 1 < line.Length ? line[j + 1] : '\0';
                    if (inString)
                    {
                        if (c == '\'' && next == '\'') { j++; continue; } //escaped quote
                        if (c == '\'') inString = false;
                    }
                    else if (commentDepth > 0)
                    {
                        if (c == '*' && next == '/') { commentDepth--; j++; }
                        else if (c == '/' && next == '*') { commentDepth++; j++; }
                    }
                    else
                    {
                        if (c == '\'') inString = true;
                        else if (c == '[') { while (j + 1 < line.Length && line[j + 1] != ']') j++; if (j + 1 < line.Length) j++; }
                        else if (c == '-' && next == '-') break; //rest of line is comment
                        else if (c == '/' && next == '*') { commentDepth++; j++; }
                    }
                }
            }
            return mask;
        }

        /// <summary>
        /// A line is untouchable by text passes when it STARTS inside an open string/comment
        /// (interior of multi-line dynamic SQL) or ENDS inside one (the boundary line that
        /// OPENS the literal): restructuring either moves quote/comment delimiters across
        /// lines and corrupts the SQL.
        /// </summary>
        private static bool LineTouchesStringOrComment(bool[] insideMask, int i)
        {
            return insideMask[i] || (i + 1 < insideMask.Length && insideMask[i + 1]);
        }

        /// <summary>
        /// Splits into lines while remembering each line's ORIGINAL terminator
        /// (index-aligned with the returned list; final line gets ""). The text
        /// post-passes only rewrite structural (SQL) lines, never lines inside a
        /// multi-line string literal - but the old Split(...).Join(Environment.NewLine)
        /// silently rewrote the literal's interior \r\n to \n, which both altered the
        /// literal's value (dynamic-SQL text the user pastes back) and shifted its
        /// measured width, oscillating across idempotency passes. Pair with
        /// JoinLinesPreservingEndings so untouched lines keep their bytes verbatim.
        /// </summary>
        private static List<string> SplitLinesPreservingEndings(string text, out List<string> endings)
        {
            var lines = new List<string>();
            endings = new List<string>();
            // Separators match Split(new[]{"\r\n","\n"}) exactly: \r\n and lone \n,
            // but NOT a lone \r (kept as an ordinary char, as the old code did).
            int start = 0, i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '\n')
                {
                    lines.Add(text.Substring(start, i - start));
                    endings.Add("\n"); i++; start = i;
                }
                else if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    lines.Add(text.Substring(start, i - start));
                    endings.Add("\r\n"); i += 2; start = i;
                }
                else i++;
            }
            lines.Add(text.Substring(start));
            endings.Add("");
            return lines;
        }

        private static string JoinLinesPreservingEndings(IList<string> lines, IList<string> endings)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                sb.Append(lines[i]);
                // A pass that changed the line count (only EnsureColumnAliases does, and
                // it keeps `endings` in sync) still has a matching terminator; the guard
                // is a belt-and-braces fallback to the platform newline.
                sb.Append(i < endings.Count ? endings[i] : Environment.NewLine);
            }
            return sb.ToString();
        }

        private string AlignFromJoinClauses(string output)
        {
            var lines = SplitLinesPreservingEndings(output, out var lineEndings);
            int indentSize = Options.SpacesPerTab > 0 ? Options.SpacesPerTab : 4;
            bool[] insideStringOrComment = ComputeLinesInsideStringOrComment(lines);
            string[] statementContexts = ComputeStatementContexts(lines, insideStringOrComment);

            // Find contiguous FROM/JOIN blocks and align each one.
            int i = 0;
            while (i < lines.Count)
            {
                if (LineTouchesStringOrComment(insideStringOrComment, i)) { i++; continue; }
                string trimmed = lines[i].TrimStart();
                string upper = trimmed.ToUpperInvariant();
                // FROM keywords appear in many statements that are NOT table-source clauses
                // (FETCH ... FROM cursor, BULK INSERT ... FROM 'file', BEGIN DIALOG ... FROM
                // SERVICE, REVOKE ... FROM principal, BACKUP/RESTORE ... FROM DISK, ...).
                // Rewriting those corrupts or even DELETES content — skip such statements
                // entirely. DELETE is special: its target FROM cannot take an AS alias, so
                // the block may be aligned but never gains invented aliases.
                string stmtContext = statementContexts[i];
                if (IsNonTableSourceStatementContext(stmtContext)) { i++; continue; }
                // DELETE's target FROM cannot take an AS alias. The statement context
                // catches "DELETE ..." at statement start, but a CTE-prefixed
                // "WITH x AS (...) DELETE FROM x" hides it — so also check whether the
                // immediately preceding content line is a bare DELETE [TOP (n)].
                bool allowAddAliases = !stmtContext.StartsWith("DELETE")
                    && !PrecedingLineIsBareDelete(lines, insideStringOrComment, i);
                if (IsFromOrJoinLine(upper))
                {
                    int blockStart = i;
                    bool expectFirstCond = false;
                    while (i < lines.Count)
                    {
                        if (LineTouchesStringOrComment(insideStringOrComment, i))
                            break;
                        string t = lines[i].TrimStart().ToUpperInvariant();
                        bool isFromJoin = IsFromOrJoinLine(t);
                        bool isContinuation = IsJoinContinuationLine(t);

                        if (i > blockStart && !isFromJoin && !isContinuation && !expectFirstCond)
                            break;

                        if (expectFirstCond)
                            expectFirstCond = false;
                        else if (isFromJoin)
                            expectFirstCond = t.TrimEnd().EndsWith(" ON");

                        i++;
                    }
                    var blockLines = lines.GetRange(blockStart, i - blockStart);
                    var newBlock = AlignFromJoinBlock(blockLines, indentSize, allowAddAliases);
                    // keep lineEndings index-aligned: FROM/JOIN blocks are structural
                    // (never inside a literal), so new interior lines get the platform
                    // newline; the block's terminal ending is preserved (handles EOF).
                    string blockTailEnding = lineEndings[i - 1];
                    lines.RemoveRange(blockStart, i - blockStart);
                    lineEndings.RemoveRange(blockStart, i - blockStart);
                    lines.InsertRange(blockStart, newBlock);
                    var newEndings = new List<string>();
                    for (int k = 0; k < newBlock.Count; k++)
                        newEndings.Add(k == newBlock.Count - 1 ? blockTailEnding : Environment.NewLine);
                    lineEndings.InsertRange(blockStart, newEndings);
                    i = blockStart + newBlock.Count;
                    //line indices shifted; keep the in-string/in-comment mask and the
                    // statement-context map in sync
                    if (newBlock.Count != blockLines.Count)
                    {
                        insideStringOrComment = ComputeLinesInsideStringOrComment(lines);
                        statementContexts = ComputeStatementContexts(lines, insideStringOrComment);
                    }
                    continue;
                }
                i++;
            }

            return JoinLinesPreservingEndings(lines, lineEndings);
        }

        /// <summary>
        /// Computes, for each line, the leading keyword pair of the statement it belongs to
        /// (e.g. "SELECT", "BULK INSERT", "BEGIN DIALOG"). Statement boundaries follow the
        /// same text-level conventions as the other post-passes: blank lines, GO, and a
        /// line-terminating semicolon end a statement.
        /// </summary>
        private static string[] ComputeStatementContexts(List<string> lines, bool[] insideStringOrComment)
        {
            var contexts = new string[lines.Count];
            string current = "";
            bool newStatement = true;
            for (int i = 0; i < lines.Count; i++)
            {
                if (LineTouchesStringOrComment(insideStringOrComment, i)) { contexts[i] = current; continue; }
                string t = lines[i].Trim();
                if (t.Length == 0) { current = ""; newStatement = true; contexts[i] = current; continue; }
                string up = t.ToUpperInvariant();
                if (up == "GO" || up.StartsWith("GO ") || up.StartsWith("GO;"))
                {
                    current = ""; newStatement = true; contexts[i] = current; continue;
                }
                if (newStatement && !up.StartsWith("--") && !up.StartsWith("/*"))
                {
                    var words = up.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    current = words.Length >= 2 ? words[0] + " " + words[1] : (words.Length == 1 ? words[0] : "");
                    newStatement = false;
                }
                contexts[i] = current;
                if (up.EndsWith(";"))
                    newStatement = true;
            }
            return contexts;
        }

        /// <summary>
        /// Returns true when the nearest preceding content line is a bare DELETE
        /// (optionally with TOP (n)) — meaning the current FROM introduces the DELETE
        /// target, which cannot take an invented alias.
        /// </summary>
        private static bool PrecedingLineIsBareDelete(List<string> lines, bool[] insideStringOrComment, int i)
        {
            for (int p = i - 1; p >= 0; p--)
            {
                if (LineTouchesStringOrComment(insideStringOrComment, p)) return false;
                string t = lines[p].Trim();
                if (t.Length == 0) return false; // blank = statement boundary
                string up = t.ToUpperInvariant();
                if (up.StartsWith("--")) continue;
                return up == "DELETE" || (up.StartsWith("DELETE TOP") && !up.Contains(" FROM "));
            }
            return false;
        }

        /// <summary>
        /// Statements whose FROM keyword introduces something other than a query table
        /// source; their FROM lines must never be align-rewritten or given aliases.
        /// </summary>
        private static bool IsNonTableSourceStatementContext(string context)
        {
            if (string.IsNullOrEmpty(context)) return false;
            int sp = context.IndexOf(' ');
            string first = sp >= 0 ? context.Substring(0, sp) : context;
            switch (first)
            {
                case "FETCH":       // FETCH NEXT FROM cursor
                case "BULK":        // BULK INSERT ... FROM 'file'
                case "BACKUP":      // BACKUP ... FROM/TO DISK
                case "RESTORE":     // RESTORE ... FROM DISK
                case "GRANT":
                case "REVOKE":      // REVOKE ... FROM principal [CASCADE]
                case "DENY":
                case "OPEN":
                case "CLOSE":
                case "DEALLOCATE":
                case "RECEIVE":     // RECEIVE ... FROM queue (Service Broker)
                case "SEND":
                case "GET":         // GET CONVERSATION GROUP ... FROM queue
                    return true;
                case "BEGIN":
                    return context.StartsWith("BEGIN DIALOG"); // BEGIN DIALOG ... FROM SERVICE
                case "CREATE":
                case "ALTER":
                    // CREATE/ALTER USER ... FROM LOGIN, CREATE LOGIN ... FROM CERTIFICATE/
                    // WINDOWS/ASYMMETRIC KEY, CREATE ASSEMBLY ... FROM <bits/path>: the FROM
                    // is a DDL source, not a table source (never gets an AS alias). CREATE
                    // VIEW/PROCEDURE/FUNCTION bodies DO contain real SELECT...FROM, so only
                    // these specific principal/assembly subtypes are excluded.
                    return context.StartsWith(first + " USER")
                        || context.StartsWith(first + " LOGIN")
                        || context.StartsWith(first + " ASSEMBLY");
                default:
                    return false;
            }
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
        /// Rewrites a FROM/JOIN block with aligned table names, AS aliases, ON conditions, and = signs.
        /// When a JOIN line ends with a trailing ON (IndentJoinOnClause mode), the first condition
        /// is pulled up to be inline with ON; subsequent AND/OR conditions are aligned under ON.
        /// Returns the new lines for the block (may be fewer than input due to pulled-up conditions).
        /// </summary>
        private List<string> AlignFromJoinBlock(List<string> blockLines, int indentSize, bool allowAddAliases = true)
        {
            // Keyword tokens that we inject — respect UppercaseKeywords setting.
            bool uc = Options.UppercaseKeywords;
            string KW_AS  = uc ? "AS"  : "as";
            string KW_ON  = uc ? "ON"  : "on";
            string KW_AND = uc ? "AND" : "and";

            // ---- Pass 1: parse each FROM/JOIN line and collect ON conditions ----
            var items = new List<JoinLineItem>();
            bool captureFirstCond = false;
            bool captureAndOrConds = false;

            for (int i = 0; i < blockLines.Count; i++)
            {
                string line = blockLines[i];
                string trimmed = line.TrimStart();
                string trimmedUpper = trimmed.ToUpperInvariant();

                if (IsJoinContinuationLine(trimmedUpper))
                {
                    // Collect AND/OR conditions for the current HasTrailingOn item.
                    if (captureAndOrConds && items.Count > 0)
                    {
                        int sp = trimmed.IndexOf(' ');
                        if (sp >= 0) items[items.Count - 1].Conditions.Add(trimmed.Substring(sp + 1));
                    }
                    continue;
                }

                // Capture first condition after a HasTrailingOn JOIN.
                if (captureFirstCond && !IsFromOrJoinLine(trimmedUpper))
                {
                    var last = items[items.Count - 1];
                    last.FirstCondition = trimmed;
                    last.Conditions.Add(trimmed);
                    captureFirstCond = false;
                    captureAndOrConds = true;
                    continue;
                }
                captureFirstCond = false;
                captureAndOrConds = false;

                string indent = line.Substring(0, line.Length - trimmed.Length);
                ParseFromJoinLine(trimmed, out string keyword, out string table, out string alias, out string onClause, out bool hasTrailingOn);

                var newItem = new JoinLineItem
                {
                    Indent = indent,
                    Keyword = keyword,
                    Table = table,
                    Alias = alias,
                    OnClause = onClause,
                    HasTrailingOn = hasTrailingOn,
                };

                // For inline ON, collect individual conditions from the clause.
                if (!hasTrailingOn && !string.IsNullOrEmpty(onClause))
                    newItem.Conditions.AddRange(SplitOnConditions(onClause));

                items.Add(newItem);
                captureFirstCond = hasTrailingOn;
            }

            if (items.Count == 0) return blockLines;

            // ---- Pass 2: ensure every item has an alias ---------------------
            if (Options.AlignTableJoinsAddAliases && allowAddAliases)
            {
                foreach (var item in items)
                {
                    if (!string.IsNullOrEmpty(item.Alias)) continue;
                    string tbl = item.Table;
                    // Only invent an alias for plain (possibly schema-qualified) table names.
                    // Skip: derived tables "(", string sources ('file', N'svc'), table
                    // variables (@t — the derived alias "@t" would be illegal), names with
                    // parens (TVF calls, legacy "(nolock)" hints), bracket-quoted parts
                    // (derivation is unsafe), and names carrying a statement terminator.
                    if (string.IsNullOrEmpty(tbl)) continue;
                    char c0 = tbl[0];
                    if (c0 == '(' || c0 == '\'' || c0 == '@' || tbl.StartsWith("N'")
                        || tbl.IndexOf('(') >= 0 || tbl.IndexOf('[') >= 0
                        || tbl.EndsWith(";"))
                        continue;
                    string baseName = BaseTableName(tbl);
                    // Reserved words / keyword-list names would need brackets as an alias —
                    // not worth inventing; leave the table unaliased instead.
                    if (!IsHarmlessUnbracketableName(baseName)) continue;
                    item.Alias = baseName;
                }
            }

            // ---- Pass 3: compute column positions --------------------------
            int maxKwTableLen = items.Max(it => it.Keyword.Length + 1 + it.Table.Length);
            int targetTableCol = ((maxKwTableLen / indentSize) + 1) * indentSize;

            int maxAliasLen = items.Max(it => it.Alias.Length);
            int asAliasLen = 3 + maxAliasLen; // "AS " + alias
            int targetOnCol = targetTableCol + ((asAliasLen / indentSize) + 1) * indentSize;
            // Padding before ON so that ON starts at exactly targetOnCol for every JOIN line.
            // Guaranteed >= 1 because targetOnCol rounds up past targetTableCol + asAliasLen.
            int onPad = targetOnCol - targetTableCol - asAliasLen;

            // Compute = alignment: tab-stop after the longest LHS across all conditions in the block.
            int maxCondLHSLen = 0;
            foreach (var item in items)
                foreach (var cond in item.Conditions)
                    if (TrySplitAtEq(cond, out string lhs, out _))
                        maxCondLHSLen = Math.Max(maxCondLHSLen, lhs.Length);
            int targetEqOffset = maxCondLHSLen > 0 ? ((maxCondLHSLen / indentSize) + 1) * indentSize : 0;

            // ---- Pass 4: produce new lines ---------------------------------
            var result = new List<string>();
            int itemIdx = 0;
            int condIdx = 0;
            bool insideIndentedOn = false;
            bool firstCondConsumed = false;

            for (int i = 0; i < blockLines.Count; i++)
            {
                string trimmedUpper = blockLines[i].TrimStart().ToUpperInvariant();
                bool isFromJoin = IsFromOrJoinLine(trimmedUpper);

                if (insideIndentedOn)
                {
                    if (isFromJoin)
                    {
                        // Next JOIN/FROM line — exit indented-ON mode and fall through.
                        insideIndentedOn = false;
                        firstCondConsumed = false;
                    }
                    else if (!firstCondConsumed)
                    {
                        // Original first-condition line — already pulled up to the JOIN line above; skip it.
                        firstCondConsumed = true;
                        continue;
                    }
                    else if (IsJoinContinuationLine(trimmedUpper))
                    {
                        // AND/OR continuation: align under ON keyword with = alignment.
                        var prevItem = items[itemIdx - 1];
                        string andIndent = prevItem.Indent + new string(' ', targetOnCol);
                        string condText = condIdx < prevItem.Conditions.Count
                            ? ApplyEqPad(prevItem.Conditions[condIdx++], targetEqOffset)
                            : blockLines[i].TrimStart().Substring(blockLines[i].TrimStart().IndexOf(' ') + 1);
                        result.Add(andIndent + KW_AND + " " + condText);
                        continue;
                    }
                    else
                    {
                        result.Add(blockLines[i]);
                        continue;
                    }
                }

                if (!isFromJoin && IsJoinContinuationLine(trimmedUpper))
                {
                    // Inline-ON overflow continuation (non-IndentJoinOnClause wrapping).
                    string contIndent = items[itemIdx > 0 ? itemIdx - 1 : 0].Indent + new string(' ', targetOnCol);
                    result.Add(contIndent + blockLines[i].TrimStart());
                    continue;
                }

                if (!isFromJoin)
                {
                    result.Add(blockLines[i]);
                    continue;
                }

                var curItem = items[itemIdx++];
                condIdx = 0;
                string kwTable = curItem.Keyword + " " + curItem.Table;
                string tablepad = new string(' ', targetTableCol - kwTable.Length);
                string aliasPad = new string(' ', maxAliasLen - curItem.Alias.Length);
                //no alias (AlignTableJoinsAddAliases=false): pad so ON stays aligned
                string asPart = string.IsNullOrEmpty(curItem.Alias)
                    ? new string(' ', KW_AS.Length + 1 + maxAliasLen)
                    : KW_AS + " " + curItem.Alias + aliasPad;
                string onPrefix = new string(' ', onPad) + KW_ON + "  ";

                if (curItem.HasTrailingOn)
                {
                    // Pull first condition inline with = alignment.
                    string cond0 = curItem.Conditions.Count > 0
                        ? ApplyEqPad(curItem.Conditions[0], targetEqOffset)
                        : curItem.FirstCondition;
                    result.Add(curItem.Indent + kwTable + tablepad + asPart + onPrefix + cond0);
                    condIdx = 1;
                    insideIndentedOn = true;
                    firstCondConsumed = false;
                }
                else if (string.IsNullOrEmpty(curItem.OnClause))
                {
                    // No ON follows on this line — the alignment padding (and empty alias
                    // slot) would become trailing whitespace; trim it.
                    result.Add((curItem.Indent + kwTable + tablepad + asPart).TrimEnd());
                }
                else
                {
                    string onBase = curItem.Indent + kwTable + tablepad + asPart + onPrefix;
                    string onFull = onBase + curItem.OnClause;
                    if (onFull.Length > 100 && curItem.Conditions.Count > 1)
                    {
                        // Wrap onto separate lines with = alignment.
                        string onIndent = new string(' ', targetOnCol);
                        result.Add(onBase + ApplyEqPad(curItem.Conditions[0], targetEqOffset));
                        for (int c = 1; c < curItem.Conditions.Count; c++)
                            result.Add(curItem.Indent + onIndent + KW_AND + " " + ApplyEqPad(curItem.Conditions[c], targetEqOffset));
                    }
                    else
                    {
                        // Single condition or short line — apply = alignment inline.
                        string aligned = curItem.Conditions.Count > 0
                            ? ApplyEqPad(curItem.Conditions[0], targetEqOffset)
                            : curItem.OnClause;
                        result.Add(onBase + aligned);
                    }
                }
            }

            return result;
        }

        private sealed class JoinLineItem
        {
            public string Indent { get; set; } = "";
            public string Keyword { get; set; } = "";
            public string Table { get; set; } = "";
            public string Alias { get; set; } = "";
            public string OnClause { get; set; } = "";
            public string FirstCondition { get; set; } = "";
            public List<string> Conditions { get; set; } = new List<string>();
            public bool HasTrailingOn { get; set; }
        }

        /// <summary>
        /// Splits "lhs = rhs" at the first standalone = (not part of !=, &lt;=, &gt;=, &lt;&gt;).
        /// Returns false if no such = is found.
        /// </summary>
        private static bool TrySplitAtEq(string condition, out string lhs, out string rhs)
        {
            for (int i = 1; i < condition.Length - 2; i++)
            {
                if (condition[i] == ' ' && condition[i + 1] == '=' && condition[i + 2] == ' ')
                {
                    char prev = condition[i - 1];
                    if (prev != '!' && prev != '<' && prev != '>' && prev != '=')
                    {
                        lhs = condition.Substring(0, i);
                        rhs = condition.Substring(i + 3);
                        return true;
                    }
                }
            }
            lhs = condition;
            rhs = "";
            return false;
        }

        /// <summary>
        /// Rewrites "lhs = rhs" so that = starts at targetOffset characters from the start of the expression.
        /// Conditions without = are returned unchanged.
        /// </summary>
        private static string ApplyEqPad(string condition, int targetOffset)
        {
            if (targetOffset <= 0 || !TrySplitAtEq(condition, out string lhs, out string rhs))
                return condition;
            int pad = Math.Max(1, targetOffset - lhs.Length);
            return lhs + new string(' ', pad) + "= " + rhs;
        }

        /// <summary>
        /// Parses a single FROM/JOIN trimmed line into its components.
        /// hasTrailingOn is true when IndentJoinOnClause mode emits "JOIN table alias ON" with no condition on this line.
        /// </summary>
        private static void ParseFromJoinLine(string trimmed, out string keyword, out string table, out string alias, out string onClause, out bool hasTrailingOn)
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

            // Detect trailing " ON" with no condition (IndentJoinOnClause mode).
            string restTrimmed = rest.TrimEnd();
            string restUpper = restTrimmed.ToUpperInvariant();
            if (restUpper.EndsWith(" ON"))
            {
                hasTrailingOn = true;
                onClause = "";
                string tableAndAlias = restTrimmed.Substring(0, restTrimmed.Length - 3).TrimEnd();
                ParseTableAndAlias(tableAndAlias, out table, out alias);
                return;
            }

            hasTrailingOn = false;
            // Now rest = "tableName [AS] [alias] [ON ...]" or "tableName [AS] [alias]"
            // Split on " ON " (case-insensitive, top-level)
            int onPos = FindTokenOutsideParens(rest, " ON ");
            onClause = onPos >= 0 ? rest.Substring(onPos + 4).Trim() : "";
            string tableAndAlias2 = onPos >= 0 ? rest.Substring(0, onPos).TrimEnd() : rest.TrimEnd();

            // Parse table [AS] alias from tableAndAlias
            ParseTableAndAlias(tableAndAlias2, out table, out alias);
        }

        private static void ParseTableAndAlias(string tableAndAlias, out string table, out string alias)
        {
            // Tokens: tableName [AS] [alias]
            // Could be: schema.Table, [schema].[Table], tableName, tableName alias, tableName AS alias,
            // or a table-valued function call: dbo.f(@a, @b) alias — the call's argument list
            // contains spaces and commas, so all splitting must be paren-aware or content
            // between the first and last token would be silently DROPPED.
            tableAndAlias = tableAndAlias.Trim();

            // Look for " AS " (with spaces) outside any parens (CAST(x AS y) can appear in TVF args)
            int tryAs = FindTokenOutsideParens(tableAndAlias, " AS ");
            if (tryAs >= 0)
            {
                table = tableAndAlias.Substring(0, tryAs).Trim();
                alias = tableAndAlias.Substring(tryAs + 4).Trim();
                return;
            }
            // No AS keyword — split at the LAST whitespace that sits at paren depth 0;
            // everything before it is the table expression, the final token is the alias.
            int depth = 0;
            int lastTopLevelSpace = -1;
            for (int i = 0; i < tableAndAlias.Length; i++)
            {
                char c = tableAndAlias[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (depth == 0 && (c == ' ' || c == '\t')) lastTopLevelSpace = i;
            }
            if (lastTopLevelSpace > 0 && lastTopLevelSpace < tableAndAlias.Length - 1)
            {
                table = tableAndAlias.Substring(0, lastTopLevelSpace).TrimEnd();
                alias = tableAndAlias.Substring(lastTopLevelSpace + 1).Trim();
                // "t (nolock)" — a legacy hint is not an alias; keep it glued to the table
                // (writing "t as (nolock)" would be invalid SQL).
                if (alias.StartsWith("("))
                {
                    table = tableAndAlias;
                    alias = "";
                }
            }
            else
            {
                table = tableAndAlias;
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
        // WrapOverflowingAliasLiterals: reach a wrap fixed point for post-pass-lengthened lines
        // ----------------------------------------------------------------

        // Matches a line of the form  <indent><lhs> = <lone-literal>[,;]  where <lhs> contains
        // no quote (so the '=' is the alias separator, never one inside a string) and the value
        // is a single string/binary literal. Group 3 is the literal; group 4 an optional
        // trailing separator.
        private static readonly Regex _aliasLiteralLine = new Regex(
            @"^(\s*)([^']*?)\s=\s(N?'(?:[^']|'')*'|0[xX][0-9A-Fa-f]+)([,;]?)\s*$",
            RegexOptions.None);

        /// <summary>
        /// The alias/align post-passes assemble `alias = value` lines AFTER the core tree walk
        /// has already made its max-line-width wrap decisions, so a line the passes lengthen
        /// (by inserting `ColumnAlias_N = `) can end up past MaxLineWidth without the wrap that
        /// the NEXT format pass would apply — the raw→formatted idempotency transient. When the
        /// value is a single literal token, the core would break immediately before it (the
        /// literal is unbreakable), putting it on its own line at the column indent. This pass
        /// reproduces that break here, so pass 1 already emits pass 2's layout. Padding and
        /// word-separator spaces are NOT part of the core's length accounting, so the threshold
        /// is measured from the trimmed LHS exactly as the core measures it.
        /// </summary>
        private string WrapOverflowingAliasLiterals(string output)
        {
            if (Options.MaxLineWidth <= 0) return output;
            var lines = SplitLinesPreservingEndings(output, out var endings);
            bool[] insideStringOrComment = ComputeLinesInsideStringOrComment(lines);
            var outLines = new List<string>(lines.Count);
            var outEndings = new List<string>(lines.Count);
            bool changed = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var m = LineTouchesStringOrComment(insideStringOrComment, i)
                    ? System.Text.RegularExpressions.Match.Empty
                    : _aliasLiteralLine.Match(lines[i]);
                if (m.Success)
                {
                    string indent = m.Groups[1].Value;
                    string lhsTrimmed = m.Groups[2].Value.TrimEnd();
                    string literal = m.Groups[3].Value;
                    // Core length just before the literal token: indent + LHS tokens + the '='
                    // operator. Separator spaces around '=' and any alignment padding are not
                    // counted by the core, so exclude them here too.
                    int coreLenBeforeValue = indent.Length + lhsTrimmed.Length + 1;
                    if (literal.Length + coreLenBeforeValue > Options.MaxLineWidth)
                    {
                        // Split: line 1 keeps the prefix through "=" (alignment padding before
                        // "=" preserved). The separator space before the wrapped value is dropped
                        // — the core strips it at a width break too (see AddOutputContent), so
                        // keeping it would disagree with the next pass (trailing-space drift).
                        outLines.Add(lines[i].Substring(0, m.Groups[3].Index).TrimEnd());
                        outEndings.Add(Environment.NewLine);
                        outLines.Add(indent + literal + m.Groups[4].Value);
                        outEndings.Add(endings[i]);
                        changed = true;
                        continue;
                    }
                }
                outLines.Add(lines[i]);
                outEndings.Add(endings[i]);
            }
            return changed ? JoinLinesPreservingEndings(outLines, outEndings) : output;
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
            var lines = SplitLinesPreservingEndings(output, out var lineEndings);
            bool inSelectList = false;
            int autoAliasCounter = 0;
            int beginDepth = 0; // tracks BEGIN/END nesting depth
            int parenDepth = 0; // tracks open-paren depth inside a SELECT column expression
            int caseDepth = 0;  // tracks open CASE...END depth — CASE arms wrapped onto
                                // their own lines (AND/THEN/ELSE fragments) are expression
                                // continuations, never new columns
            bool[] insideStringOrComment = ComputeLinesInsideStringOrComment(lines);

            for (int i = 0; i < lines.Count; i++)
            {
                //never touch lines inside OR opening a multi-line string literal / block comment
                if (LineTouchesStringOrComment(insideStringOrComment, i)) { inSelectList = false; parenDepth = 0; caseDepth = 0; continue; }
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
                    if (inSelectList) { inSelectList = false; parenDepth = 0; caseDepth = 0; } // BEGIN always ends a SELECT list
                    continue;
                }
                if (trimmedUpper == "END" || trimmedUpper.StartsWith("END ") || trimmedUpper.StartsWith("END\t")
                    || trimmedUpper.StartsWith("END;") || trimmedUpper.StartsWith("END,") || trimmedUpper.StartsWith("END)"))
                {
                    if (beginDepth > 0) beginDepth--;
                    if (inSelectList) { inSelectList = false; parenDepth = 0; caseDepth = 0; }
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
                        caseDepth = 0;
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
                                int netParens1 = CountNetParens(colExpr1);
                                //only alias when the inline expression is balanced (not the start
                                // of a multi-line expression such as ROW_NUMBER() OVER ( )
                                if (netParens1 <= 0 && CountNetCase(colExpr1) <= 0)
                                {
                                    var (rewritten, newCounter) = EnsureAlias(colExpr1, autoAliasCounter);
                                    autoAliasCounter = newCounter;
                                    string finalCol1 = rewritten + (hadTrailingComma1 ? "," : "");
                                    if (finalCol1 != colPart1)
                                        lines[i] = indent + "SELECT " + modPrefix1 + finalCol1;
                                }
                                parenDepth += netParens1;
                                if (parenDepth < 0) parenDepth = 0;
                                caseDepth += CountNetCase(colExpr1);
                                if (caseDepth < 0) caseDepth = 0;
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
                    caseDepth = 0;

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
                            int netParens1 = CountNetParens(colExpr1);
                            //only alias when the inline expression is balanced (not the start
                            // of a multi-line expression such as ROW_NUMBER() OVER ( )
                            if (netParens1 <= 0 && CountNetCase(colExpr1) <= 0)
                            {
                                var (rewritten, newCounter) = EnsureAlias(colExpr1, autoAliasCounter);
                                autoAliasCounter = newCounter;
                                string finalCol1 = rewritten + (hadTrailingComma1 ? "," : "");
                                if (finalCol1 != colPart1)
                                    lines[i] = indent + "SELECT " + modPrefix1 + finalCol1;
                            }
                            parenDepth += netParens1;
                            if (parenDepth < 0) parenDepth = 0;
                        }
                    }
                    continue;
                }

                // --- Detect end of SELECT list ---
                if (inSelectList)
                {
                    //the formatter never emits a blank line INSIDE a column list - a blank
                    // line means the statement ended (guards against the tracker running on
                    // into OPEN/DECLARE/etc. and column-izing arbitrary statements)
                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        inSelectList = false;
                        parenDepth = 0;
                        caseDepth = 0;
                        continue;
                    }
                    // If we're inside an unbalanced open-paren or open CASE (multi-line
                    // expression continuation), just track depths and skip alias processing.
                    if (parenDepth > 0 || caseDepth > 0)
                    {
                        parenDepth += CountNetParens(trimmed);
                        if (parenDepth < 0) parenDepth = 0;
                        caseDepth += CountNetCase(trimmed);
                        if (caseDepth < 0) caseDepth = 0;
                        continue;
                    }

                    bool startsWithComma = trimmed.StartsWith(",");
                    // The previous column line ended with a trailing binary operator (an
                    // alias RHS wrapped: "FieldList =" then "'...'", or a concat "... +"
                    // then "N'...'"). This line is that wrapped operand, not a new column.
                    if (!startsWithComma && !IsClauseStartLine(trimmedUpper)
                        && PrevColumnLineEndsOpen(lines, i))
                    {
                        parenDepth += CountNetParens(trimmed);
                        if (parenDepth < 0) parenDepth = 0;
                        caseDepth += CountNetCase(trimmed);
                        if (caseDepth < 0) caseDepth = 0;
                        continue;
                    }
                    // Width-wrapped continuations of a taller expression (boolean AND/OR,
                    // CASE-arm keywords, operator-leading fragments) are not new columns.
                    if (!startsWithComma
                        && (trimmedUpper.StartsWith("AND ") || trimmedUpper.StartsWith("OR ")
                            || trimmedUpper.StartsWith("THEN ") || trimmedUpper.StartsWith("WHEN ")
                            || trimmedUpper.StartsWith("ELSE ") || trimmedUpper.StartsWith("AS ")
                            || "+-*/%=<>".IndexOf(trimmed[0]) >= 0
                            // a line opening with '(' (but not a scalar subquery) is a wrapped
                            // operand — e.g. a function call split "quotename \n (ObjectName)"
                            || (trimmed[0] == '(' && !trimmedUpper.StartsWith("(SELECT"))))
                    {
                        parenDepth += CountNetParens(trimmed);
                        if (parenDepth < 0) parenDepth = 0;
                        caseDepth += CountNetCase(trimmed);
                        if (caseDepth < 0) caseDepth = 0;
                        continue;
                    }
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
                        int netCase = CountNetCase(afterComma);
                        // Only alias if the expression is balanced (not a multi-line expression start)
                        // and its own AS-alias isn't waiting on the next line (wrapped "... \n AS x").
                        if (netParens <= 0 && netCase <= 0 && !NextColumnLineIsAsAlias(lines, i))
                        {
                            var (rewritten, newCounter) = EnsureAlias(afterComma, autoAliasCounter);
                            autoAliasCounter = newCounter;
                            if (rewritten != afterComma)
                            {
                                string indent = line.Substring(0, line.Length - trimmed.Length);
                                lines[i] = indent + "," + rewritten;
                            }
                        }
                        // Track net parens/CASE for multi-line expression detection
                        parenDepth += netParens;
                        if (parenDepth < 0) parenDepth = 0;
                        caseDepth += netCase;
                        if (caseDepth < 0) caseDepth = 0;
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        bool hadTrailingComma = trimmed.EndsWith(",");
                        var (modPrefix2, colExpr2, modOnly2) = StripSelectModifierPrefix(trimmed.TrimEnd(','));
                        if (modOnly2)
                            continue; // pure TOP N or DISTINCT line — not a column, leave as-is

                        int netParens2 = CountNetParens(colExpr2);
                        int netCase2 = CountNetCase(colExpr2);
                        if (netParens2 <= 0 && netCase2 <= 0 && !NextColumnLineIsAsAlias(lines, i))
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
                        caseDepth += netCase2;
                        if (caseDepth < 0) caseDepth = 0;
                    }
                }
            }

            return JoinLinesPreservingEndings(lines, lineEndings);
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
                if (c == '-' && i + 1 < s.Length && s[i + 1] == '-') break; //-- comment
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    int close = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (close < 0) break; //comment continues past end of line
                    i = close + 1;
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

        // Counts unmatched CASE keywords minus END keywords at the top paren level (outside
        // strings/brackets/parens). Positive means the expression opens a CASE that continues
        // on subsequent lines; used to skip EnsureAlias on the opening CASE token.
        private static int CountNetCase(string s)
        {
            int net = 0;
            int parenDepth = 0;
            bool inSingleQuote = false;
            bool inBracket = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inSingleQuote)
                {
                    if (c == '\'' && i + 1 < s.Length && s[i + 1] == '\'') { i++; continue; }
                    if (c == '\'') inSingleQuote = false;
                    continue;
                }
                if (inBracket) { if (c == ']') inBracket = false; continue; }
                if (c == '\'') { inSingleQuote = true; continue; }
                if (c == '[') { inBracket = true; continue; }
                if (c == '-' && i + 1 < s.Length && s[i + 1] == '-') break; //-- comment
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    int close = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (close < 0) break; //comment continues past end of line
                    i = close + 1;
                    continue;
                }
                if (c == '(') { parenDepth++; continue; }
                if (c == ')') { parenDepth--; continue; }
                if (parenDepth > 0) continue;
                bool prevIsWord = i > 0 && (char.IsLetterOrDigit(s[i - 1]) || s[i - 1] == '_');
                if (prevIsWord) continue;
                if (i + 4 <= s.Length
                    && string.Compare(s, i, "CASE", 0, 4, StringComparison.OrdinalIgnoreCase) == 0
                    && (i + 4 >= s.Length || !(char.IsLetterOrDigit(s[i + 4]) || s[i + 4] == '_')))
                {
                    net++;
                    i += 3;
                }
                else if (i + 3 <= s.Length
                    && string.Compare(s, i, "END", 0, 3, StringComparison.OrdinalIgnoreCase) == 0
                    && (i + 3 >= s.Length || !(char.IsLetterOrDigit(s[i + 3]) || s[i + 3] == '_')))
                {
                    net--;
                    i += 2;
                }
            }
            return net;
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

            //peel a trailing -- comment off before analysis: anything appended after it
            // would be swallowed by the comment ("u.Id -- note AS ColumnAlias_1"), and the
            // invisible alias would compound on every re-format. The alias goes BEFORE the
            // comment; the comment is re-attached at the end.
            string trailingComment = "";
            int commentStart = FindLineCommentStart(trimmed);
            if (commentStart >= 0)
            {
                trailingComment = " " + trimmed.Substring(commentStart);
                trimmed = trimmed.Substring(0, commentStart).TrimEnd();
                if (trimmed.Length == 0)
                    return (col, counter); //comment-only line
            }

            //peel a trailing statement terminator off before analysis; re-appended at the
            // end so "SELECT foo;" becomes "SELECT foo AS foo;", never "foo; AS foo;"
            string suffix = "";
            while (trimmed.EndsWith(";"))
            {
                suffix = ";" + suffix;
                trimmed = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
            }

            // Empty or comment-only — skip
            if (string.IsNullOrEmpty(trimmed)
                || (trimmed.StartsWith("/*") && trimmed.IndexOf("*/") == trimmed.Length - 2)
                || trimmed.StartsWith("--"))
                return (col, counter);

            // Ends with a trailing binary operator (=, +, ...): an INCOMPLETE expression
            // whose operand wraps to the next line. Aliasing it here appends "AS X" after
            // the operator ("a + AS ColumnAlias_1") — invalid, and it masks the trailing
            // operator so the wrapped operand line then looks like a new column too. Leave
            // the whole multi-line expression un-aliased (valid SQL, deterministic).
            if (EndsWithTrailingBinaryOperator(trimmed))
                return (col, counter);

            // Already has AS alias?
            if (FindAsOutsideParens(trimmed) >= 0)
                return (col, counter);

            // Already has an EqualSign-style alias (alias = expr) in the input SQL.
            // The column IS aliased — preserve the user's chosen style, do not rewrite to AS.
            // A string literal on the LHS ('alias' = expr, the legacy alias syntax that
            // RewriteAliasesToEqualSign itself emits for AS 'alias') counts too — without
            // this, REformatting stacks a second alias in front and corrupts the column.
            if (!trimmed.StartsWith("@"))
            {
                int eqPos = FindEqualSignOutsideParens(trimmed);
                if (eqPos > 0)
                {
                    string lhs = trimmed.Substring(0, eqPos).Trim();
                    if (IsSimpleColumnRef(lhs) || IsStringLiteralAlias(lhs))
                        return (col, counter);
                }
            }

            // Variable assignment SELECT @var = expr — not a column alias, leave alone
            if (trimmed.StartsWith("@") && trimmed.IndexOf('=') >= 0)
                return (col, counter);

            // Wildcard — skip (a trailing /* ... */ comment doesn't make it aliasable)
            string wildcardCheck = trimmed;
            int wbc = wildcardCheck.LastIndexOf("/*", System.StringComparison.Ordinal);
            if (wbc >= 0 && wildcardCheck.IndexOf("*/", wbc, System.StringComparison.Ordinal) == wildcardCheck.Length - 2)
                wildcardCheck = wildcardCheck.Substring(0, wbc).TrimEnd();
            if (wildcardCheck == "*" || wildcardCheck.EndsWith(".*"))
                return (col, counter);

            // AS-less bracketed alias (expr [My Alias]) — the column IS aliased.
            if (EndsWithBracketedAlias(trimmed))
                return (col, counter);

            // AS-less plain alias (expr alias) — the column IS aliased.
            if (EndsWithPlainAlias(trimmed))
                return (col, counter);

            // AS-less string-literal alias (expr 'alias' / expr N'alias', legacy syntax).
            if (EndsWithStringLiteralAlias(trimmed))
                return (col, counter);

            // Determine alias. A bare @variable is not a reusable column name (SELECT @v
            // must alias to ColumnAlias_N, never "@v = @v"), so it takes the complex path.
            string alias;
            if (IsSimpleColumnRef(trimmed) && !trimmed.StartsWith("@"))
            {
                alias = ExtractColumnBaseName(trimmed);
                if (IsReservedAliasKeyword(alias))
                {
                    // A bare keyword column (SELECT NULL) can't alias to itself; a derived
                    // alias that collides with a reserved keyword (t.HASH -> HASH) must be
                    // bracketed to stay valid (HASH = t.HASH is a syntax error).
                    if (string.Equals(alias, trimmed, System.StringComparison.OrdinalIgnoreCase))
                    {
                        counter++;
                        alias = "ColumnAlias_" + counter;
                    }
                    else
                        alias = "[" + alias + "]";
                }
            }
            else
            {
                counter++;
                alias = "ColumnAlias_" + counter;
            }

            // A -- comment that directly follows a statement terminator uses the core's
            // ";--comment" spacing (no gap), so re-attaching " --comment" would drift to
            // ";--comment" on the next pass. Match the core when suffix ends with ';'.
            if (suffix.EndsWith(";") && trailingComment.StartsWith(" --"))
                trailingComment = trailingComment.Substring(1);
            return (trimmed + " AS " + alias + suffix + trailingComment, counter);
        }

        /// <summary>
        /// Finds the start of a trailing -- comment outside string literals and bracket
        /// identifiers; -1 when the line carries none.
        /// </summary>
        private static int FindLineCommentStart(string s)
        {
            bool inSingleQuote = false;
            bool inBracket = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inSingleQuote)
                {
                    if (c == '\'' && i + 1 < s.Length && s[i + 1] == '\'') { i++; continue; }
                    if (c == '\'') inSingleQuote = false;
                    continue;
                }
                if (inBracket) { if (c == ']') inBracket = false; continue; }
                if (c == '\'') { inSingleQuote = true; continue; }
                if (c == '[') { inBracket = true; continue; }
                if (c == '-' && i + 1 < s.Length && s[i + 1] == '-') return i;
            }
            return -1;
        }

        /// <summary>
        /// True when the expression ends in a standalone [bracketed] token that acts as an
        /// AS-less alias (Name [Test Case Name]) — as opposed to a qualified reference
        /// (t.[Col]), a lone bracketed column ([Col]), or a bracketed operand (a + [b]).
        /// </summary>
        private static bool EndsWithBracketedAlias(string expr)
        {
            if (!expr.EndsWith("]")) return false;
            int open = expr.LastIndexOf('[');
            if (open <= 0) return false; //no room for an expression before it (lone [Col])
            char before = expr[open - 1];
            if (before != ' ' && before != '\t') return false; //t.[Col] and glued forms
            string prefix = expr.Substring(0, open - 1).TrimEnd();
            if (prefix.Length == 0) return false;
            //the alias must FOLLOW a complete expression: identifier/bracket/paren/quote
            // endings qualify; a trailing operator (a + [b]) means [b] is an operand
            char last = prefix[prefix.Length - 1];
            return char.IsLetterOrDigit(last) || last == '_' || last == ']' || last == ')' || last == '\'';
        }

        // Keywords that, appearing immediately before the final identifier, mean that
        // identifier is an OPERAND of the keyword, not an AS-less alias:
        //   x COLLATE Latin1   (Latin1 is the collation)   a AND b   a IS NULL   x LIKE y
        private static readonly HashSet<string> _operandExpectingBeforeAlias = new HashSet<string>(
            new[] { "COLLATE", "AND", "OR", "NOT", "LIKE", "IN", "BETWEEN", "ESCAPE", "IS", "AS", "THEN", "WHEN", "ELSE", "OVER" },
            System.StringComparer.OrdinalIgnoreCase);

        // Keywords that terminate/are part of an expression, so a column ending in one is
        // NOT aliased (a trailing NULL/END/DEFAULT is expression content, not an alias name).
        private static readonly HashSet<string> _nonAliasTerminatorKeywords = new HashSet<string>(
            new[] { "NULL", "END", "DEFAULT" }, System.StringComparer.OrdinalIgnoreCase);

        private static bool IsReservedAliasKeyword(string name)
        {
            if (string.IsNullOrEmpty(name) || name[0] == '[') return false; // already bracketed
            return Parsers.TSqlStandardParser.KeywordList.ContainsKey(name.ToUpperInvariant());
        }

        /// <summary>
        /// True when a line's last non-comment character is a binary operator that requires a
        /// following operand (=, +, -, *, /, %, &amp;, |, ^, and comparison &lt;/&gt;). Such a
        /// line's expression continues onto the next physical line, so that next line is a
        /// wrapped operand, not a new SELECT column. A trailing ',' (complete column) and a
        /// closing quote/paren/identifier (complete expression) return false.
        /// </summary>
        private static bool EndsWithTrailingBinaryOperator(string line)
        {
            string s = line;
            int lc = FindLineCommentStart(s);
            if (lc >= 0) s = s.Substring(0, lc);
            // strip a trailing /* ... */ block comment (whole-line-tail only)
            int bc = s.LastIndexOf("/*", System.StringComparison.Ordinal);
            if (bc >= 0 && s.IndexOf("*/", bc, System.StringComparison.Ordinal) == s.Length - 2)
                s = s.Substring(0, bc);
            s = s.TrimEnd();
            if (s.Length == 0) return false;
            // '*' is excluded: a trailing '*' is almost always a wildcard (SELECT *, t.*),
            // not multiplication, and treating it as an operator makes the following FROM
            // look like a wrapped operand. A trailing '.' is a member-access split (AL. \n Col).
            if ("+-/%=<>&|^.".IndexOf(s[s.Length - 1]) >= 0)
                return true;
            // A trailing word operator (expr wrapped after AS/AND/OR/THEN/...) also means the
            // operand/alias lands on the next line: "... END AS" then the alias, "x =" then RHS.
            int w = s.LastIndexOfAny(new[] { ' ', '\t' });
            string lastWord = w >= 0 ? s.Substring(w + 1) : s;
            return _trailingWordOperators.Contains(lastWord);
        }

        private static readonly HashSet<string> _trailingWordOperators = new HashSet<string>(
            new[] { "AS", "AND", "OR", "NOT", "THEN", "WHEN", "ELSE", "LIKE", "IN", "BETWEEN", "ESCAPE", "OVER", "COLLATE" },
            System.StringComparer.OrdinalIgnoreCase);

        private static bool IsWholeLineComment(string trimmed)
        {
            return trimmed.StartsWith("--")
                || (trimmed.StartsWith("/*") && trimmed.TrimEnd().EndsWith("*/"));
        }

        /// <summary>
        /// True when the nearest PRECEDING non-comment column line left its expression open
        /// (ended with a trailing operator). Whole-line comments between a wrapped operator
        /// and its operand ("script =" / "/* note */" / "N'...'") are skipped; a blank line
        /// is a statement boundary and stops the search.
        /// </summary>
        private static bool PrevColumnLineEndsOpen(IList<string> lines, int i)
        {
            for (int k = i - 1; k >= 0; k--)
            {
                string t = lines[k].TrimStart();
                if (string.IsNullOrWhiteSpace(t)) return false;
                if (IsWholeLineComment(t)) continue;
                return EndsWithTrailingBinaryOperator(lines[k]);
            }
            return false;
        }

        /// <summary>
        /// True when the next non-comment line CONTINUES the current column's expression, so
        /// the current line is not a complete column and must not be aliased: an "AS alias"
        /// tail (already aliased on the next line), or a fragment leading with a continuation
        /// operator/keyword (+ / AND / THEN ... — a concat/boolean wrapped onto the next line).
        /// Stops at a blank line, a leading comma (new column), or a clause keyword.
        /// </summary>
        private static bool NextColumnLineIsAsAlias(IList<string> lines, int i)
        {
            for (int k = i + 1; k < lines.Count; k++)
            {
                string t = lines[k].TrimStart();
                if (string.IsNullOrWhiteSpace(t)) return false;
                if (IsWholeLineComment(t)) continue;
                if (t.StartsWith(",")) return false; // next is a new (leading-comma) column
                string tu = t.ToUpperInvariant();
                if (IsClauseStartLine(tu)) return false;
                return tu.StartsWith("AS ") || tu == "AS"
                    || tu.StartsWith("AND ") || tu.StartsWith("OR ") || tu.StartsWith("THEN ")
                    || tu.StartsWith("WHEN ") || tu.StartsWith("ELSE ")
                    || "+-*/%=<>&|^".IndexOf(t[0]) >= 0
                    || (t[0] == '(' && !tu.StartsWith("(SELECT")); // function-call / group split
            }
            return false;
        }

        /// <summary>
        /// True when the expression ends in a bare (non-bracketed) identifier that acts as
        /// an AS-less alias: "expr alias" where a complete expression is followed by a
        /// plain identifier — 'B' Id, count(1) Cnt, CASE ... END AvailabilityGroup,
        /// 0 i, 'x' COLLATE coll c1. Prepending "ColumnAlias_N =" or appending "AS x" to
        /// such a column produces invalid SQL (alias = expr alias2), which is exactly what
        /// EnsureColumnAliases did before this check existed. Conservative: any doubt →
        /// treat as an operand (return false) rather than risk hiding a real missing alias.
        /// </summary>
        private static bool EndsWithPlainAlias(string expr)
        {
            expr = expr.TrimEnd();
            int ws = expr.LastIndexOfAny(new[] { ' ', '\t' });
            if (ws <= 0) return false; // single token — no room for "expr alias"
            string last = expr.Substring(ws + 1);
            // the alias must be a plain identifier (bracketed handled by EndsWithBracketedAlias),
            // must not start with @ (variables aren't aliases), and must not be one of the few
            // keywords that TERMINATE/are part of an expression (NULL/END/DEFAULT) - those mean
            // the column is un-aliased. Function-name-ish keywords (VALUE, HASH, TYPE_NAME) can
            // legitimately be AS-less aliases, so they are not excluded here.
            if (last.Length == 0 || last[0] == '@'
                || !System.Text.RegularExpressions.Regex.IsMatch(last, @"^[A-Za-z_#][A-Za-z0-9_$#]*$"))
                return false;
            if (_nonAliasTerminatorKeywords.Contains(last))
                return false;
            string prefix = expr.Substring(0, ws).TrimEnd();
            if (prefix.Length == 0) return false;
            // the alias must FOLLOW a complete expression: identifier/bracket/paren/quote/digit
            char lastPrefixChar = prefix[prefix.Length - 1];
            if (!(char.IsLetterOrDigit(lastPrefixChar) || lastPrefixChar == '_'
                  || lastPrefixChar == ']' || lastPrefixChar == ')' || lastPrefixChar == '\''))
                return false;
            // the token right before the alias must not expect an operand (a COLLATE b, a AND b)
            int pw = prefix.LastIndexOfAny(new[] { ' ', '\t' });
            string beforeAlias = pw >= 0 ? prefix.Substring(pw + 1) : prefix;
            if (_operandExpectingBeforeAlias.Contains(beforeAlias))
                return false;
            return true;
        }

        /// <summary>
        /// True when the expression ends in a bare string literal acting as an AS-less alias
        /// (legacy syntax): CONVERT(...) N'sp_BlitzIndex v2.02'  →  the trailing N'...' is the
        /// column's alias, not a concat operand. Re-aliasing such a column yields invalid SQL
        /// (alias = expr 'alias'). The string is an OPERAND (return false) when preceded by an
        /// operator (a + 'b'); it is an ALIAS when preceded by a complete expression.
        /// Also recognizes the glued empty-string alias ''r / N''r (upstream #200 adjacency).
        /// </summary>
        private static bool EndsWithStringLiteralAlias(string expr)
        {
            expr = expr.TrimEnd();
            // glued empty-string alias: ''r or N''alias (no space, adjacency-preserved)
            if (System.Text.RegularExpressions.Regex.IsMatch(expr, @"^N?''[A-Za-z_#][A-Za-z0-9_$#]*$"))
                return true;
            if (!expr.EndsWith("'")) return false;
            // walk back to the opening quote of the trailing literal (skip '' escapes)
            int i = expr.Length - 2;
            while (i >= 0)
            {
                if (expr[i] == '\'')
                {
                    if (i - 1 >= 0 && expr[i - 1] == '\'') { i -= 2; continue; }
                    break;
                }
                i--;
            }
            if (i < 0) return false;
            int litStart = i;
            if (litStart - 1 >= 0 && (expr[litStart - 1] == 'N' || expr[litStart - 1] == 'n')) litStart--;
            string prefix = expr.Substring(0, litStart).TrimEnd();
            if (prefix.Length == 0) return false; // the whole column IS the string literal — no alias
            char last = prefix[prefix.Length - 1];
            // an operator/open-paren/dot before the string means it's an operand, not an alias
            if ("+-*/%=<>&|^.,(".IndexOf(last) >= 0) return false;
            return char.IsLetterOrDigit(last) || last == '_' || last == ')' || last == ']' || last == '\'';
        }

        /// <summary>
        /// Returns true if <paramref name="expr"/> is a single self-contained string literal
        /// ('alias' or N'alias' with no trailing content) — the legacy string-alias LHS form.
        /// </summary>
        private static bool IsStringLiteralAlias(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return false;
            int start = expr.StartsWith("N'") ? 2 : (expr[0] == '\'' ? 1 : -1);
            if (start < 0 || expr.Length < start + 1 || !expr.EndsWith("'")) return false;
            // Ensure the literal closes exactly at the end (embedded '' escapes allowed).
            int i = start;
            while (i < expr.Length)
            {
                if (expr[i] == '\'')
                {
                    if (i + 1 < expr.Length && expr[i + 1] == '\'') { i += 2; continue; } // escaped quote
                    return i == expr.Length - 1; // closing quote must be the final char
                }
                i++;
            }
            return false;
        }

        /// <summary>
        /// Returns true if <paramref name="expr"/> is a bare column reference:
        /// an identifier (optionally bracket-quoted, optionally table- or schema-qualified)
        /// with no parentheses, spaces, or arithmetic operators.
        /// </summary>
        private static bool IsSimpleColumnRef(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return false;
            //literals are not column references: numbers must not become their own alias
            // (SELECT 1 -> '1 = 1' is invalid SQL), and quoted strings are not identifiers
            if (char.IsDigit(expr[0]) || expr[0] == '\'' || expr.StartsWith("N'"))
                return false;
            // Scan character by character; spaces and special chars inside bracket-quoted
            // segments (e.g. [Active Flag]) are valid and must not trigger a false negative.
            // A ]] inside brackets is an escaped ] and stays in-bracket ([Definition: [x]] y]).
            bool inBracket = false;
            for (int k = 0; k < expr.Length; k++)
            {
                char c = expr[k];
                if (inBracket)
                {
                    if (c == ']')
                    {
                        if (k + 1 < expr.Length && expr[k + 1] == ']') { k++; continue; } // escaped ]]
                        inBracket = false;
                    }
                    continue;
                }
                if (c == '[') { inBracket = true; continue; }
                if (c == '(' || c == ')' || c == ' ' || c == '\t' ||
                    c == '+' || c == '-' || c == '/' || c == '%' || c == '*')
                    return false;
            }
            // Must look like: word, word.word, [word], [word].[word], schema.table.col, etc.
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
            //if the source column was bracket-quoted, the derived alias stays
            // bracket-quoted: [Some] -> alias [Some], never bare Some (SOME is a
            // reserved word; stripping the brackets produces invalid SQL)
            if (name.StartsWith("["))
                return name;
            return name.Contains(' ') ? "[" + name + "]" : name;
        }


        /// Only handles lines that are between a SELECT keyword line and the next clause keyword.
        /// </summary>
        private string RewriteAliasesToEqualSign(string output)
        {
            var lines = SplitLinesPreservingEndings(output, out var lineEndings);
            bool inSelectList = false;
            int parenDepth = 0, caseDepth = 0; // multi-line expression continuation tracking
            bool[] insideStringOrComment = ComputeLinesInsideStringOrComment(lines);

            for (int i = 0; i < lines.Count; i++)
            {
                //never touch lines inside OR opening a multi-line string literal / block comment
                if (LineTouchesStringOrComment(insideStringOrComment, i)) { inSelectList = false; parenDepth = 0; caseDepth = 0; continue; }
                string line = lines[i];
                string trimmed = line.TrimStart();
                string trimmedUpper = trimmed.ToUpperInvariant();

                // Detect start of SELECT column list
                if (trimmedUpper.StartsWith("SELECT ") || trimmedUpper == "SELECT")
                {
                    inSelectList = true;
                    parenDepth = 0;
                    caseDepth = 0;
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
                            // only rewrite a self-contained (balanced) column; a multi-line
                            // expression start would have its "AS alias" flipped onto a fragment
                            if (CountNetParens(colExpr1) <= 0 && CountNetCase(colExpr1) <= 0
                                && !EndsWithTrailingBinaryOperator(colExpr1))
                            {
                                string rewritten = TryRewriteColumnLine(colExpr1);
                                string finalCol1 = rewritten + (hadTrailingComma1 ? "," : "");
                                if (finalCol1 != colPart1)
                                    lines[i] = indent + "SELECT " + modPfx1 + finalCol1;
                            }
                            parenDepth += CountNetParens(colExpr1);
                            if (parenDepth < 0) parenDepth = 0;
                            caseDepth += CountNetCase(colExpr1);
                            if (caseDepth < 0) caseDepth = 0;
                        }
                    }
                    continue;
                }

                // Detect end of SELECT list
                if (inSelectList)
                {
                    //blank line = end of statement (see EnsureColumnAliases)
                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        inSelectList = false;
                        parenDepth = 0;
                        caseDepth = 0;
                        continue;
                    }
                    bool startsWithComma = trimmed.StartsWith(",");
                    // A continuation of a wrapped column expression (inside open parens/CASE,
                    // after a line ending in an operator, or leading with AND/OR/THEN/operator)
                    // is a FRAGMENT: rewriting its "... AS alias" tail flips the alias onto an
                    // incomplete expression ("DatabaseItem = and right(...)"). Track depths, skip.
                    bool isContinuation =
                        parenDepth > 0 || caseDepth > 0
                        || (!startsWithComma && PrevColumnLineEndsOpen(lines, i))
                        || (!startsWithComma
                            && (trimmedUpper.StartsWith("AND ") || trimmedUpper.StartsWith("OR ")
                                || trimmedUpper.StartsWith("THEN ") || trimmedUpper.StartsWith("WHEN ")
                                || trimmedUpper.StartsWith("ELSE ") || trimmedUpper.StartsWith("AS ")
                                || (trimmed.Length > 0 && "+-*/%=<>&|^".IndexOf(trimmed[0]) >= 0)
                                || (trimmed.Length > 0 && trimmed[0] == '(' && !trimmedUpper.StartsWith("(SELECT"))));
                    if (!startsWithComma && !isContinuation && IsClauseStartLine(trimmedUpper))
                    {
                        inSelectList = false;
                        continue;
                    }

                    string colBody = startsWithComma ? trimmed.Substring(1).TrimStart() : trimmed;
                    if (isContinuation)
                    {
                        parenDepth += CountNetParens(colBody);
                        if (parenDepth < 0) parenDepth = 0;
                        caseDepth += CountNetCase(colBody);
                        if (caseDepth < 0) caseDepth = 0;
                        continue;
                    }

                    if (startsWithComma)
                    {
                        // Leading comma style: ,expr AS alias
                        string afterComma = trimmed.Substring(1).TrimStart();
                        if (CountNetParens(afterComma) <= 0 && CountNetCase(afterComma) <= 0
                            && !EndsWithTrailingBinaryOperator(afterComma))
                        {
                            string rewritten = TryRewriteColumnLine(afterComma);
                            if (rewritten != afterComma)
                            {
                                string indent = line.Substring(0, line.Length - trimmed.Length);
                                lines[i] = indent + "," + rewritten;
                            }
                        }
                        parenDepth += CountNetParens(afterComma);
                        if (parenDepth < 0) parenDepth = 0;
                        caseDepth += CountNetCase(afterComma);
                        if (caseDepth < 0) caseDepth = 0;
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        bool hadTrailingComma = trimmed.EndsWith(",");
                        var (modPfx2, colExpr2, modOnly2) = StripSelectModifierPrefix(trimmed.TrimEnd(','));
                        if (modOnly2)
                            continue; // pure TOP N or DISTINCT line — not a column, leave as-is

                        if (CountNetParens(colExpr2) <= 0 && CountNetCase(colExpr2) <= 0
                            && !EndsWithTrailingBinaryOperator(colExpr2))
                        {
                            string rewritten = TryRewriteColumnLine(colExpr2);
                            string indent2 = line.Substring(0, line.Length - trimmed.Length);
                            if (rewritten != colExpr2 || modPfx2.Length > 0)
                                lines[i] = indent2 + modPfx2 + rewritten + (hadTrailingComma ? "," : "");
                        }
                        parenDepth += CountNetParens(colExpr2);
                        if (parenDepth < 0) parenDepth = 0;
                        caseDepth += CountNetCase(colExpr2);
                        if (caseDepth < 0) caseDepth = 0;
                    }
                }
            }

            return JoinLinesPreservingEndings(lines, lineEndings);
        }

        private static bool IsClauseStartLine(string trimmedUpper)
        {
            return trimmedUpper.StartsWith("FROM ") || trimmedUpper == "FROM"
                // FROM glued to its source with no space: FROM(subquery), FROM::fn_trace_gettable(...)
                || trimmedUpper.StartsWith("FROM(") || trimmedUpper.StartsWith("FROM::")
                || trimmedUpper.StartsWith("WHERE ") || trimmedUpper == "WHERE"
                || trimmedUpper.StartsWith("GROUP ") || trimmedUpper == "GROUP"
                || trimmedUpper.StartsWith("ORDER ") || trimmedUpper == "ORDER"
                || trimmedUpper.StartsWith("HAVING ") || trimmedUpper == "HAVING"
                || trimmedUpper.StartsWith("UNION ") || trimmedUpper == "UNION"
                || trimmedUpper.StartsWith("INTERSECT ") || trimmedUpper == "INTERSECT"
                || trimmedUpper.StartsWith("EXCEPT ") || trimmedUpper == "EXCEPT"
                || trimmedUpper.StartsWith("INTO ")
                //select lists also end at cursor/query options and batch separators:
                // FOR (cursor FOR READ ONLY / FOR XML / FOR UPDATE), OPTION(...) hints,
                // GO, and INSERT's VALUES keyword
                || trimmedUpper.StartsWith("FOR ") || trimmedUpper == "FOR"
                || trimmedUpper.StartsWith("OPTION ") || trimmedUpper.StartsWith("OPTION(")
                || trimmedUpper.StartsWith("GO ") || trimmedUpper == "GO"
                || trimmedUpper.StartsWith("VALUES ") || trimmedUpper == "VALUES" || trimmedUpper.StartsWith("VALUES(")
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
                    // Everything after "TOP" — do NOT assume a space exists ("TOP(@i)"
                    // reaches here with no space at all, e.g. from sp_WhoIsActive).
                    string afterTop = trimmed.Substring(3).TrimStart();
                    string rest;
                    string topToken;
                    if (afterTop.StartsWith("("))
                    {
                        // Find the MATCHING close paren — TOP arguments can nest calls,
                        // e.g. TOP (LEN(ISNULL(@s, N''))); taking the first ')' would cut
                        // the expression mid-call and alias the severed TOP as a column.
                        int depth = 0;
                        int close = -1;
                        for (int p = 0; p < afterTop.Length; p++)
                        {
                            if (afterTop[p] == '(') depth++;
                            else if (afterTop[p] == ')' && --depth == 0) { close = p; break; }
                        }
                        if (close < 0)
                            return (accumulatedPrefix + trimmed, "", true); //unterminated TOP(...) — treat as modifier-only
                        topToken = afterTop.Substring(0, close + 1);
                        rest = afterTop.Substring(close + 1).TrimStart();
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
            //peel a trailing -- comment first: "expr AS alias -- note" must become
            // "alias = expr -- note"; splitting naively leaves the comment glued to the
            // alias and the comment then swallows " = expr" entirely.
            string original = col;
            string trailingComment = "";
            int commentStart = FindLineCommentStart(col);
            if (commentStart >= 0)
            {
                trailingComment = " " + col.Substring(commentStart);
                col = col.Substring(0, commentStart).TrimEnd();
            }
            else
            {
                //also peel a whole trailing block comment ("expr AS alias, /* note */"):
                // otherwise it glues to the alias and the alias becomes "alias, /* note */"
                int bc = col.LastIndexOf("/*", System.StringComparison.Ordinal);
                if (bc >= 0 && col.IndexOf("*/", bc, System.StringComparison.Ordinal) == col.TrimEnd().Length - 2)
                {
                    trailingComment = " " + col.Substring(bc).TrimEnd();
                    col = col.Substring(0, bc).TrimEnd();
                }
            }
            int asPos = FindAsOutsideParens(col);
            if (asPos < 0) return original;
            string expr = col.Substring(0, asPos).TrimEnd();
            string alias = col.Substring(asPos + 4).Trim();
            //a trailing statement terminator / column-separator comma belongs at the END of
            // the rewritten line, not glued to the alias ("x AS a;" -> "a = x;" not "a; = x";
            // "x AS a," -> "a = x," not "a, = x")
            string suffix = "";
            while (alias.EndsWith(";") || alias.EndsWith(","))
            {
                suffix = alias[alias.Length - 1] + suffix;
                alias = alias.Substring(0, alias.Length - 1).TrimEnd();
            }
            if (string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(expr)) return original;
            // match the core's ";--comment" (no gap) so it doesn't drift on re-format
            if (suffix.EndsWith(";") && trailingComment.StartsWith(" --"))
                trailingComment = trailingComment.Substring(1);
            return alias + " = " + expr + suffix + trailingComment;
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
                //comments: '-- ...' ends the scan; '/* ... */' spans are skipped; an
                // unterminated '/*' means the rest of the line is comment content
                // (a comment like /* same drive as data */ must never match " AS ")
                if (c == '-' && i + 1 < s.Length && s[i + 1] == '-') break;
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    int close = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (close < 0) break;
                    i = close + 1;
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
        /// Finds the position of a top-level standalone '=' outside parentheses, string
        /// literals, and bracket-quoted identifiers (e.g. [Odd=Name]).
        /// Excludes !=, >=, &lt;= compound operators. Returns -1 if not found.
        /// </summary>
        private static int FindEqualSignOutsideParens(string s)
        {
            int depth = 0;
            bool inString = false;
            bool inBracket = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inString) { if (c == '\'') inString = false; continue; }
                if (inBracket) { if (c == ']') inBracket = false; continue; }
                if (c == '\'') { inString = true; continue; }
                if (c == '[') { inBracket = true; continue; }
                if (c == '-' && i + 1 < s.Length && s[i + 1] == '-') break; //-- comment
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    int close = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (close < 0) break; //comment continues past end of line
                    i = close + 1;
                    continue;
                }
                if (c == '(') { depth++; continue; }
                if (c == ')') { depth--; continue; }
                if (depth == 0 && c == '=')
                {
                    // exclude comparison !=/>=/<= and compound-assignment +=/-=/*=//=/%=/&=/|=/^=
                    // (SELECT @v += (...) must not be split into "@v + = (...)")
                    if (i > 0 && "!<>+-*/%&|^".IndexOf(s[i - 1]) >= 0) continue;
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
            var lines = SplitLinesPreservingEndings(output, out var lineEndings);
            bool[] insideStringOrComment = ComputeLinesInsideStringOrComment(lines);

            // Find runs of SELECT column lines and align each run.
            int i = 0;
            while (i < lines.Count)
            {
                //never treat lines inside or opening a multi-line string literal / block comment as SQL
                if (LineTouchesStringOrComment(insideStringOrComment, i)) { i++; continue; }
                string trimmed = lines[i].TrimStart().ToUpperInvariant();
                if (trimmed.StartsWith("SELECT ") || trimmed == "SELECT")
                {
                    // Collect the range of lines that are SELECT column list lines.
                    int start = i;
                    // The SELECT line itself may have the first column inline.
                    // Column lines continue until we hit a non-column line.
                    while (i < lines.Count)
                    {
                        if (LineTouchesStringOrComment(insideStringOrComment, i))
                            break;
                        string t = lines[i].TrimStart();
                        if (i > start && string.IsNullOrWhiteSpace(t))
                            break; //blank line = end of statement
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

            return JoinLinesPreservingEndings(lines, lineEndings);
        }

        /// <summary>
        /// Marks each line in a SELECT block [start,end) that is a CONTINUATION of a
        /// wrapped column expression rather than a column of its own: inside parens/CASE
        /// opened on an earlier line, following a line that ended with a trailing operator
        /// (=, +, AS, ...), or leading with a continuation token (AND/OR/THEN/operator).
        /// The align passes must not treat these as "alias = expr" / "expr AS alias" — doing
        /// so splices a spurious alias into the middle of a wrapped CASE/concat (invalid SQL).
        /// Returned array is indexed 0..(end-start).
        /// </summary>
        private static bool[] ComputeSelectBlockContinuationMask(IList<string> lines, int start, int end)
        {
            var mask = new bool[end - start];
            int parenDepth = 0, caseDepth = 0;
            for (int i = start; i < end; i++)
            {
                string trimmed = lines[i].TrimStart();
                string tu = trimmed.ToUpperInvariant();
                // strip the SELECT / leading-comma marker to inspect the column expression
                string content = tu.StartsWith("SELECT ") ? trimmed.Substring(7).TrimStart()
                               : trimmed.StartsWith(",") ? trimmed.Substring(1).TrimStart()
                               : trimmed;
                bool leadingContinuation =
                    !trimmed.StartsWith(",")
                    && (tu.StartsWith("AND ") || tu.StartsWith("OR ") || tu.StartsWith("THEN ")
                        || tu.StartsWith("WHEN ") || tu.StartsWith("ELSE ") || tu.StartsWith("AS ")
                        || tu == "AS" || (trimmed.Length > 0 && "+-/%=<>&|^".IndexOf(trimmed[0]) >= 0)
                        || (trimmed.Length > 0 && trimmed[0] == '(' && !tu.StartsWith("(SELECT")));
                bool prevOpen = i > start && PrevColumnLineEndsOpen(lines, i);
                mask[i - start] = parenDepth > 0 || caseDepth > 0 || prevOpen || leadingContinuation;
                parenDepth += CountNetParens(content);
                if (parenDepth < 0) parenDepth = 0;
                caseDepth += CountNetCase(content);
                if (caseDepth < 0) caseDepth = 0;
            }
            return mask;
        }

        private void AlignBlockAsKeywords(IList<string> lines, int start, int end)
        {
            bool[] continuation = ComputeSelectBlockContinuationMask(lines, start, end);
            // For each line in the block, find the position of " AS " (outside parens).
            // Measure the max expression width and pad.
            // Lines using EqualSign-style aliases (alias = expr) are preserved as-is and
            // aligned as their own group (their '=' signs line up with each other).
            var items = new List<(int lineIdx, string indent, string comma, string expr, string alias)>();
            var equalSignItems = new List<(int lineIdx, string prefix, string alias, string expr)>();

            for (int i = start; i < end; i++)
            {
                if (continuation[i - start]) continue; // wrapped-expression continuation, not a column
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
                    continue;
                }

                // EqualSign-style alias line: alias = expr (LHS must be a simple column
                // reference; @variable assignments are not aliases).
                if (!content.StartsWith("@"))
                {
                    int eqPos = FindEqualSignOutsideParens(content);
                    if (eqPos > 0)
                    {
                        string alias = content.Substring(0, eqPos).TrimEnd();
                        string expr = content.Substring(eqPos + 1).TrimStart();
                        if (IsSimpleColumnRef(alias) && expr.Length > 0)
                            equalSignItems.Add((i, prefix, alias, expr));
                    }
                }
            }

            if (items.Count >= 2)
            {
                int maxExprLen = items.Max(it => it.expr.Length);
                foreach (var (lineIdx, prefix, _, expr, alias) in items)
                {
                    string padding = new string(' ', maxExprLen - expr.Length);
                    lines[lineIdx] = prefix + expr + padding + " AS " + alias;
                }
            }

            if (equalSignItems.Count >= 2)
            {
                int maxAliasLen = equalSignItems.Max(it => it.alias.Length);
                foreach (var (lineIdx, prefix, alias, expr) in equalSignItems)
                {
                    string padding = new string(' ', maxAliasLen - alias.Length);
                    lines[lineIdx] = prefix + alias + padding + " = " + expr;
                }
            }
        }

        private void AlignBlockEqualSign(IList<string> lines, int start, int end)
        {
            // For lines in alias = expr format, align the = signs to a tab stop column.
            // Lines look like: indent + [SELECT | ,] + alias + = + expr
            var items = new List<(int lineIdx, string prefix, string alias, string expr)>();
            bool[] continuation = ComputeSelectBlockContinuationMask(lines, start, end);

            for (int i = start; i < end; i++)
            {
                if (continuation[i - start]) continue; // wrapped-expression continuation, not a column
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
                // An empty expr means the value wrapped to the next line (WrapOverflowingAliasLiterals
                // or a core width break). Emit a bare "=" — a "= " here would strand a trailing
                // space that the core strips on the next pass (trailing-space non-idempotency).
                string eq = expr.Length == 0 ? "=" : "= " + expr;
                lines[lineIdx] = prefix + alias + new string(' ', padding) + eq;
            }
        }

/// <summary>
        /// Aligns DDL column definitions so that column names, data types, and nullability
        /// are in vertical columns.  Input is the formatted content of a DDLDETAIL_PARENS block
        /// (without the surrounding parens).
        /// Each row: indent + [comma] + colname + datatype + [constraint...]
        /// We align: colname and datatype into two columns.
        /// </summary>
        /// <summary>
        /// True when a DDLParens node actually contains column/parameter DEFINITIONS
        /// (CREATE/ALTER object bodies, DECLARE @t TABLE) — the only shapes
        /// AlignColumnDefinitionsInDDL may rewrite. INSERT/VALUES/OPTION parens and CTE
        /// column-name lists share the element name but hold arbitrary content.
        /// </summary>
        private static bool IsColumnDefinitionParens(Node parensNode)
        {
            string? parentName = parensNode.Parent?.Name;
            return parentName == SqlStructureConstants.ENAME_DDL_PROCEDURAL_BLOCK
                || parentName == SqlStructureConstants.ENAME_DDL_OTHER_BLOCK
                || parentName == SqlStructureConstants.ENAME_DDL_DECLARE_BLOCK;
        }

        private string AlignDdlColumns(string ddlContent)
        {
            var lines = SplitLinesPreservingEndings(ddlContent, out var lineEndings);
            // interior lines of a multi-line /* ... */ comment must never be treated as
            // column definitions or padded (their content would ratchet rightward each pass)
            bool[] insideStringOrComment = ComputeLinesInsideStringOrComment(lines);

            // Each "column definition" line has: indent + optional-comma + name + type + rest
            // We identify lines that look like a column definition (first token is an identifier, second is a data type).
            // For simplicity: lines that aren't blank and don't start with a keyword that's a constraint.
            var colLines = new List<int>();
            var colNames = new List<string>();
            var afterNames = new List<string>();

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (LineTouchesStringOrComment(insideStringOrComment, i)) continue;

                // Strip indent and optional comma
                int pos = 0;
                while (pos < line.Length && (line[pos] == '\t' || line[pos] == ' ')) pos++;
                if (pos >= line.Length) continue;

                // comment-only lines (/* ... */, -- ...) are not column definitions and must
                // never be padded — otherwise their content ratchets rightward every pass
                if (line[pos] == '/' && pos + 1 < line.Length && line[pos + 1] == '*') continue;
                if (line[pos] == '-' && pos + 1 < line.Length && line[pos + 1] == '-') continue;

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

            return JoinLinesPreservingEndings(lines, lineEndings);
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
                    //a LEADING empty statement (the implicit statement before a GO that opens
                    // the file) renders nothing - otherwise it emits a stray blank line and
                    // files starting with a batch separator reformat differently every pass.
                    // Trailing/mid-file empty statements keep their historical rendering.
                    if (contentElement.PreviousSibling() == null
                        && !StatementHasRenderableContent(contentElement))
                        break;
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
                    //newline regardless of whether previous element recommended a break or not
                    // - unless nothing has been output yet (GO as the file's first statement),
                    // where a break would just emit a stray leading blank line.
                    if (state.CurrentLineHasContent || state.OutputContainsLineBreak)
                        state.WhiteSpace_BreakToNextLine();
                    else
                        state.BreakExpected = false;
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

					//CompactSingleStatementBlocks: render a single-statement IF/ELSE/WHILE body
					// inline on the same line when it stays single-line and fits MaxLineWidth.
					if (Options.CompactSingleStatementBlocks
						&& contentElement.Name.Equals(SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT)
						&& !singleStatementIsIf
						&& IsSmallSubtree(contentElement, 60)
						)
					{
						TSqlStandardFormattingState compactState = new TSqlStandardFormattingState(state);
						compactState.BreakExpected = false;
						compactState.StatementBreakExpected = false;
						compactState.WordSeparatorExpected = false;
						compactState.SourceBreakPending = false;
						ProcessSqlNodeList(contentElement.Children, compactState);
						if (!compactState.OutputContainsLineBreak
							&& state.CurrentLineLength + 1 + compactState.CurrentLineLength <= Options.MaxLineWidth
							)
						{
							state.WordSeparatorExpected = true;
							WhiteSpace_SeparateWords(state);
							state.Assimilate(compactState);
							state.WordSeparatorExpected = false;
							//mirror the standard exit: the OUTER statement owns statement
							// separation; anything following (e.g. ELSE) starts on a new line
							state.BreakExpected = true;
							state.StatementBreakExpected = false;
							state.UnIndentInitialBreak = false;
							break;
						}
					}

					if (singleStatementIsIf && contentElement.Parent!.Name.Equals(SqlStructureConstants.ENAME_ELSE_CLAUSE))
					{
						//artificially decrement indent and skip new statement break for "ELSE IF" constructs
						state.DecrementIndent();
					}
					else
					{
						state.BreakExpected = true;
					}
                    ProcessSqlNodeList(contentElement.Children, state);
					if (singleStatementIsIf && contentElement.Parent!.Name.Equals(SqlStructureConstants.ENAME_ELSE_CLAUSE))
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
                    //a source line-break INSIDE the (collapsed) argument list must not leak out:
                    // it would push a trailing comment after the call onto its own line, which
                    // re-attaches the comment to the next clause on reformat (indent drift /
                    // non-idempotent output). A break AFTER the call sets this flag again anyway.
                    state.SourceBreakPending = false;
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
                        // NOTE: the parser reuses ENAME_DDL_PARENS for INSERT column lists,
                        // VALUES tuples, and OPTION(...) hints (layout convenience) — those
                        // contain arbitrary expressions/literals, NOT "name type" pairs, and
                        // aligning them pads INSIDE strings and function calls. Only parens
                        // whose parent is a real DDL block hold column/parameter definitions.
                        if (Options.AlignColumnDefinitionsInDDL
                            && contentElement.Name == SqlStructureConstants.ENAME_DDL_PARENS
                            && IsColumnDefinitionParens(contentElement))
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
                    if (contentElement.Parent!.Name.Equals(SqlStructureConstants.ENAME_SQL_CLAUSE)
                        && contentElement.Parent!.Parent!.Name.Equals(SqlStructureConstants.ENAME_SQL_STATEMENT)
                        && contentElement.Parent!.Parent!.Parent!.Name.Equals(SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT)
                        )
                        state.DecrementIndent();
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_OPEN), state);
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_MULTISTATEMENT), state);
                    state.DecrementIndent();
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByName(SqlStructureConstants.ENAME_CONTAINER_CLOSE), state);
                    state.IncrementIndent();
                    if (contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_SQL_CLAUSE)
                        && contentElement.Parent!.Parent!.Name.Equals(SqlStructureConstants.ENAME_SQL_STATEMENT)
                        && contentElement.Parent!.Parent!.Parent!.Name.Equals(SqlStructureConstants.ENAME_CONTAINER_SINGLESTATEMENT)
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
                    //IndentWhereAndOrConditions promises "AND/OR onto separate lines", so it
                    // forces the break even when ExpandBooleanExpressions is disabled.
                    if (Options.ExpandBooleanExpressions || Options.IndentWhereAndOrConditions)
                        state.BreakExpected = true;
                    if (Options.IndentWhereAndOrConditions)
                        state.IncrementIndent();
                    ProcessSqlNode(contentElement.ChildByName(SqlStructureConstants.ENAME_OTHERKEYWORD)!, state);
                    if (Options.IndentWhereAndOrConditions)
                        state.DecrementIndent();
                    break;

                case SqlStructureConstants.ENAME_COMMENT_MULTILINE:
                    if (state.SpecialRegionActive == SpecialRegionType.NoFormat && contentElement.TextValue!.ToUpperInvariant().Contains("[/NOFORMAT]"))
                    {
                        Node? skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlIdentityFormatter tempFormatter = new TSqlIdentityFormatter(Options.HTMLColoring);
                            state.EmitSpecialRegionContent(tempFormatter.FormatSQLTree(skippedXml));
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }
                    else if (state.SpecialRegionActive == SpecialRegionType.Minify && contentElement.TextValue!.ToUpperInvariant().Contains("[/MINIFY]"))
                    {
                        Node? skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlObfuscatingFormatter tempFormatter = new TSqlObfuscatingFormatter();
                            if (HTMLFormatted)
                                state.EmitSpecialRegionContent(Utils.HtmlEncode(tempFormatter.FormatSQLTree(skippedXml))!);
                            else
                                state.EmitSpecialRegionContent(tempFormatter.FormatSQLTree(skippedXml));
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }

                    // A multi-line comment that started at column 0 in the source keeps
                    // column 0: indenting only its FIRST line (subsequent lines are
                    // emitted verbatim) breaks box-art banner alignment (upstream #99).
                    bool keepCommentAtColumnZero = state.SpecialRegionActive == null
                        && contentElement.TextValue != null
                        && contentElement.TextValue.Contains('\n')
                        && CommentStartsAtSourceColumnZero(contentElement);
                    if (keepCommentAtColumnZero)
                        state.OmitIndentOnFlushedBreaks = true;
                    WhiteSpace_SeparateComment(contentElement, state);
                    state.AddOutputContent("/*" + contentElement.TextValue + "*/", SqlHtmlConstants.CLASS_COMMENT);
                    state.OmitIndentOnFlushedBreaks = false;
                    if (contentElement.Parent!.Name.Equals(SqlStructureConstants.ENAME_SQL_STATEMENT)
                        || (contentElement.NextSibling() != null
                            && contentElement.NextSibling()!.Name.Equals(SqlStructureConstants.ENAME_WHITESPACE)
                            && Regex.IsMatch(contentElement.NextSibling()!.TextValue!, @"(\r|\n)+")
                            )
                        )
                        //if this block comment is at the start or end of a statement, or if it was followed by a 
                        // linebreak before any following content, then break here.
                        state.BreakExpected = true;
                    else
                    {
                        state.WordSeparatorExpected = true;
                    }

                    if (state.SpecialRegionActive == null && contentElement.TextValue!.ToUpperInvariant().Contains("[NOFORMAT]"))
                    {
                        //state.AddOutputLineBreak();
                        state.SpecialRegionActive = SpecialRegionType.NoFormat;
                        state.RegionStartNode = contentElement;
                    }
                    else if (state.SpecialRegionActive == null && contentElement.TextValue!.ToUpperInvariant().Contains("[MINIFY]"))
                    {
                        //state.AddOutputLineBreak();
                        state.SpecialRegionActive = SpecialRegionType.Minify;
                        state.RegionStartNode = contentElement;
                    }
                    break;

                case SqlStructureConstants.ENAME_COMMENT_SINGLELINE:
                case SqlStructureConstants.ENAME_COMMENT_SINGLELINE_CSTYLE:
                    if (state.SpecialRegionActive == SpecialRegionType.NoFormat && contentElement.TextValue!.ToUpperInvariant().Contains("[/NOFORMAT]"))
                    {
                        Node? skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlIdentityFormatter tempFormatter = new TSqlIdentityFormatter(Options.HTMLColoring);
                            state.EmitSpecialRegionContent(tempFormatter.FormatSQLTree(skippedXml));
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }
                    else if (state.SpecialRegionActive == SpecialRegionType.Minify && contentElement.TextValue!.ToUpperInvariant().Contains("[/MINIFY]"))
                    {
                        Node? skippedXml = NodeExtensions.ExtractStructureBetween(state.RegionStartNode, contentElement);
                        if (skippedXml != null)
                        {
                            TSqlObfuscatingFormatter tempFormatter = new TSqlObfuscatingFormatter();
                            if (HTMLFormatted)
                                state.EmitSpecialRegionContent(Utils.HtmlEncode(tempFormatter.FormatSQLTree(skippedXml))!);
                            else
                                state.EmitSpecialRegionContent(tempFormatter.FormatSQLTree(skippedXml));
                        }
                        state.SpecialRegionActive = null;
                        state.RegionStartNode = null;
                    }

                    WhiteSpace_SeparateComment(contentElement, state);
                    state.AddOutputContent((contentElement.Name == SqlStructureConstants.ENAME_COMMENT_SINGLELINE ? "--" : "//") + contentElement.TextValue!.Replace("\r", "").Replace("\n", ""), SqlHtmlConstants.CLASS_COMMENT);
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
                    // Empty string literals ('') are usually escaped quotes inside a
                    // dynamic-SQL FRAGMENT being formatted on its own. When the source
                    // had no whitespace between such a literal and a neighboring
                    // word/value token, preserve that adjacency verbatim - injecting a
                    // space would change the text the user pastes back inside the outer
                    // string (upstream #200: ''.txt'' became ''.txt ''). Keyword and
                    // comma spacing is unaffected (those are not word/value tokens).
                    bool isEmptyStringLiteral = string.IsNullOrEmpty(contentElement.TextValue);
                    if (isEmptyStringLiteral
                        && !state.BreakExpected && state.AdditionalBreaksExpected == 0
                        && IsAdjacencyPreservingToken(contentElement.PreviousSibling(), includeStrings: true))
                        state.WordSeparatorExpected = false;
                    WhiteSpace_SeparateWords(state);
                    string? outValue = null;
                    if (contentElement.Name.Equals(SqlStructureConstants.ENAME_NSTRING))
                        outValue = "N'" + contentElement.TextValue!.Replace("'", "''") + "'";
                    else
                        outValue = "'" + contentElement.TextValue!.Replace("'", "''") + "'";
                    state.AddOutputContent(outValue, SqlHtmlConstants.CLASS_STRING);
                    state.WordSeparatorExpected = !(isEmptyStringLiteral
                        && IsAdjacencyPreservingToken(contentElement.NextSibling(), includeStrings: true));
                    break;

                case SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    // RemoveHarmlessBrackets (upstream #133, opt-in): strip brackets when
                    // the name provably doesn't need them - a valid regular identifier,
                    // not in the keyword list (keeps [Order]/[definition] safe from
                    // reinterpretation and keyword-uppercasing on reformat), and not
                    // directly adjacent to a token it would merge with.
                    bool stripBrackets = Options.RemoveHarmlessBrackets
                        && IsHarmlessUnbracketableName(contentElement.TextValue!)
                        && !IsTokenMergeRisk(contentElement.PreviousSibling())
                        && !IsTokenMergeRisk(contentElement.NextSibling());
                    // Preserve source adjacency between words and bracket names
                    // ("table_[some_id]" templating - upstream #240). Keywords and
                    // commas keep their usual spacing; whitespace in the source is
                    // its own sibling node, so adjacency here means "no gap written".
                    if (!stripBrackets
                        && !state.BreakExpected && state.AdditionalBreaksExpected == 0
                        && IsAdjacencyPreservingToken(contentElement.PreviousSibling(), includeStrings: false))
                        state.WordSeparatorExpected = false;
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent(stripBrackets
                        ? contentElement.TextValue!
                        : "[" + contentElement.TextValue!.Replace("]", "]]") + "]");
                    state.WordSeparatorExpected = stripBrackets
                        || !IsAdjacencyPreservingToken(contentElement.NextSibling(), includeStrings: false);
                    break;

                case SqlStructureConstants.ENAME_QUOTED_STRING:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent("\"" + contentElement.TextValue!.Replace("\"", "\"\"") + "\"");
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_COMMA:
                    //comma always ignores requested word spacing
                    if (Options.TrailingCommas)
                    {
                        WhiteSpace_BreakAsExpected(state);
                        state.AddOutputContent(FormatOperator(","), SqlHtmlConstants.CLASS_OPERATOR);

                        if ((Options.ExpandCommaLists
								&& !(contentElement.Parent!.Name.Equals(SqlStructureConstants.ENAME_DDLDETAIL_PARENS)
									|| contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_FUNCTION_PARENS)
									|| contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
									|| IsCompactRaiserrorArgs(contentElement)
									)
								)
							|| (Options.ExpandInLists
								&& contentElement.Parent!.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
								)
							)
                            state.BreakExpected = true;
                        else
                            state.WordSeparatorExpected = true;
                    }
                    else
                    {
                        if ((Options.ExpandCommaLists
								&& !(contentElement.Parent!.Name.Equals(SqlStructureConstants.ENAME_DDLDETAIL_PARENS)
									|| contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_FUNCTION_PARENS)
									|| contentElement.Parent.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
									|| IsCompactRaiserrorArgs(contentElement)
									)
								)
							|| (Options.ExpandInLists
								&& contentElement.Parent!.Name.Equals(SqlStructureConstants.ENAME_IN_PARENS)
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
                    state.AddOutputContent(FormatOperator(contentElement.TextValue!), SqlHtmlConstants.CLASS_OPERATOR);
                    break;

                case SqlStructureConstants.ENAME_ASTERISK:
                case SqlStructureConstants.ENAME_EQUALSSIGN:
                case SqlStructureConstants.ENAME_ALPHAOPERATOR:
                case SqlStructureConstants.ENAME_OTHEROPERATOR:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent(FormatOperator(contentElement.TextValue!), SqlHtmlConstants.CLASS_OPERATOR);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_COMPOUNDKEYWORD:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.SetRecentKeyword(contentElement.GetAttributeValue(SqlStructureConstants.ANAME_SIMPLETEXT)!);
                    state.AddOutputContent(FormatKeyword(contentElement.GetAttributeValue(SqlStructureConstants.ANAME_SIMPLETEXT)!), SqlHtmlConstants.CLASS_KEYWORD);
                    state.WordSeparatorExpected = true;
                    ProcessSqlNodeList(contentElement.ChildrenByNames(SqlStructureConstants.ENAMELIST_COMMENT), state.IncrementIndent());
                    state.DecrementIndent();
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_OTHERKEYWORD:
                case SqlStructureConstants.ENAME_DATATYPE_KEYWORD:
                {
                    string kwUpper = contentElement.TextValue!.ToUpperInvariant();

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
                        Node? prev = contentElement.PreviousSibling();
                        while (prev != null && prev.Name == SqlStructureConstants.ENAME_WHITESPACE)
                            prev = prev!.PreviousSibling();
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
                    state.AddOutputContent(FormatKeyword(contentElement.TextValue!), SqlHtmlConstants.CLASS_KEYWORD);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_FUNCTION_KEYWORD:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.SetRecentKeyword(contentElement.TextValue!);
                    state.AddOutputContent(FormatKeyword(contentElement.TextValue!), SqlHtmlConstants.CLASS_FUNCTION);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_OTHERNODE:
                case SqlStructureConstants.ENAME_MONETARY_VALUE:
                case SqlStructureConstants.ENAME_LABEL:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent(contentElement.TextValue!);
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
                    state.AddOutputContent(contentElement.TextValue!.ToLowerInvariant());
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_BINARY_VALUE:
                    if (state.InSelectModifierZone) { state.InSelectModifierZone = false; state.BreakExpected = true; }
                    WhiteSpace_SeparateWords(state);
                    //single AddOutputContent call: the max-line-width logic may break BETWEEN
                    // content calls, and a binary literal must never be split across lines
                    // (whitespace inside 0x... changes the SQL's meaning).
                    state.AddOutputContent("0x" + contentElement.TextValue!.Substring(2).ToUpperInvariant());
                    state.WordSeparatorExpected = true;
                    break;

                case SqlStructureConstants.ENAME_WHITESPACE:
                    //take note if it's a line-breaking space, but don't DO anything here
                    if (Regex.IsMatch(contentElement.TextValue!, @"(\r|\n)+"))
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
            string? outputKeyword;
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
                Node? thisClauseStarter = FirstSemanticElementChild(contentElement);
				if (!(thisClauseStarter != null
					&& thisClauseStarter.Name.Equals(SqlStructureConstants.ENAME_OTHERKEYWORD)
					&& state.GetRecentKeyword() != null
					&& ((thisClauseStarter.TextValue!.ToUpperInvariant().Equals("SET")
							&& state.GetRecentKeyword()!.Equals("SET")
							)
						|| (thisClauseStarter.TextValue.ToUpperInvariant().Equals("DECLARE")
							&& state.GetRecentKeyword()!.Equals("DECLARE")
							)
						|| (thisClauseStarter.TextValue.ToUpperInvariant().Equals("PRINT")
							&& state.GetRecentKeyword()!.Equals("PRINT")
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

        /// <summary>
        /// Cheap bounded check: true when the subtree contains at most maxNodes nodes.
        /// Used to gate the speculative inline rendering of CompactSingleStatementBlocks -
        /// without a bound, the try-inline-then-fallback double rendering is exponential
        /// on nested single-statement bodies (observed: >30s on a 2MB script).
        /// </summary>
        private static bool IsSmallSubtree(Node container, int maxNodes)
        {
            int count = 0;
            return CountNodesUpTo(container, maxNodes, ref count);
        }

        private static bool CountNodesUpTo(Node node, int maxNodes, ref int count)
        {
            foreach (Node child in node.Children)
            {
                if (++count > maxNodes)
                    return false;
                if (!CountNodesUpTo(child, maxNodes, ref count))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// True when the statement contains anything that produces output: any element
        /// other than whitespace, including comments. Empty clauses are recursed into.
        /// </summary>
        private static bool StatementHasRenderableContent(Node statement)
        {
            foreach (Node child in statement.Children)
            {
                if (child.Name.Equals(SqlStructureConstants.ENAME_WHITESPACE))
                    continue;
                if (child.Name.Equals(SqlStructureConstants.ENAME_SQL_CLAUSE))
                {
                    if (StatementHasRenderableContent(child))
                        return true;
                    continue;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// True when this comma belongs to a RAISERROR(...) argument list and the
        /// CompactRaiserror option is active - such lists stay on one line.
        /// </summary>
        private bool IsCompactRaiserrorArgs(Node commaElement)
        {
            if (!Options.CompactRaiserror)
                return false;
            Node? parens = commaElement.Parent;
            if (parens == null || !parens.Name.Equals(SqlStructureConstants.ENAME_EXPRESSION_PARENS))
                return false;
            Node? prev = parens.PreviousSibling();
            while (prev != null && SqlStructureConstants.ENAMELIST_NONCONTENT.Contains(prev.Name))
                prev = prev.PreviousSibling();
            return prev != null
                && prev.Name.Equals(SqlStructureConstants.ENAME_OTHERKEYWORD)
                && "RAISERROR".Equals(prev.TextValue, StringComparison.OrdinalIgnoreCase);
        }

        private Node? FirstSemanticElementChild(Node? contentElement)
        {
            Node? target = null;
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

        /// <summary>
        /// Word/value tokens whose direct source-adjacency to an EMPTY string literal
        /// is preserved (see upstream #200). A sibling of one of these types means the
        /// source had no whitespace in between (whitespace is its own sibling node).
        /// Deliberately excludes keywords and commas so their spacing is untouched.
        /// </summary>
        private static readonly Regex _regularIdentifierRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        /// <summary>
        /// True when a bracket-quoted name could safely be written without brackets:
        /// a conservative regular-identifier shape (ASCII letter/underscore start,
        /// letters/digits/underscores only) that is not in the T-SQL keyword list.
        /// </summary>
        private static bool IsHarmlessUnbracketableName(string name)
        {
            return _regularIdentifierRegex.IsMatch(name)
                && !Parsers.TSqlStandardParser.KeywordList.ContainsKey(name.ToUpperInvariant());
        }

        /// <summary>
        /// True when the sibling is a token that an unbracketed identifier would merge
        /// with if the brackets between them disappeared (no whitespace node = direct
        /// source adjacency). Periods, commas, operators and parens are safe separators.
        /// </summary>
        private static bool IsTokenMergeRisk(Node? node)
        {
            if (node == null) return false;
            switch (node.Name)
            {
                case SqlStructureConstants.ENAME_OTHERNODE:
                case SqlStructureConstants.ENAME_NUMBER_VALUE:
                case SqlStructureConstants.ENAME_MONETARY_VALUE:
                case SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME:
                case SqlStructureConstants.ENAME_STRING:
                case SqlStructureConstants.ENAME_NSTRING:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAdjacencyPreservingToken(Node? node, bool includeStrings)
        {
            if (node == null) return false;
            switch (node.Name)
            {
                case SqlStructureConstants.ENAME_OTHERNODE:
                case SqlStructureConstants.ENAME_NUMBER_VALUE:
                case SqlStructureConstants.ENAME_MONETARY_VALUE:
                case SqlStructureConstants.ENAME_PERIOD:
                case SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME:
                    return true;
                case SqlStructureConstants.ENAME_STRING:
                case SqlStructureConstants.ENAME_NSTRING:
                    // Strings only count for the empty-string fragment scenario (#200);
                    // bracket adjacency (#240) excludes them so "[col] 'alias'" keeps
                    // its historical readable spacing.
                    return includeStrings;
                default:
                    return false;
            }
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

        /// <summary>
        /// True when the comment began at column 0 in the source: its preceding sibling
        /// is whitespace ending in a line break (no horizontal offset after it).
        /// </summary>
        private static bool CommentStartsAtSourceColumnZero(Node contentElement)
        {
            Node? prev = contentElement.PreviousSibling();
            if (prev == null || !prev.Name.Equals(SqlStructureConstants.ENAME_WHITESPACE))
                return false;
            string? ws = prev.TextValue;
            return !string.IsNullOrEmpty(ws) && (ws!.EndsWith("\n") || ws.EndsWith("\r"));
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
            public Node? RegionStartNode { get; set; }

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

            public override void AddOutputContent(string content, string? htmlClassName)
            {
                if (CurrentLineHasContent && (content.Length + CurrentLineLength > MaxLineWidth))
                {
                    if (SpecialRegionActive == null && _outBuilder.Length > 0
                        && _outBuilder[_outBuilder.Length - 1] == ' ')
                        _outBuilder.Length--;
                    WhiteSpace_BreakToNextLine();
                }

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

            /// <summary>
            /// Emits the verbatim (identity- or minify-rendered) content of a special
            /// region and synchronizes line-state bookkeeping with it. During the region,
            /// suppressed AddOutputContent calls left CurrentLineHasContent stale, so the
            /// closing marker's comment-separation logic added a line break even when the
            /// region text already ended with one - accumulating a blank line inside the
            /// region on every pass (upstream #215/#292).
            /// </summary>
            internal void EmitSpecialRegionContent(string regionContent)
            {
                AddOutputContentRaw(regionContent);
                WordSeparatorExpected = false;
                BreakExpected = false;
                // SourceBreakPending is deliberately left alone: for minified regions
                // (whose rendering strips newlines) it still reflects whether the SOURCE
                // broke the line before the closing marker.
                bool endsWithLineBreak = regionContent.EndsWith("\n") || regionContent.EndsWith("\r");
                CurrentLineHasContent = !endsWithLineBreak;
                if (endsWithLineBreak)
                    CurrentLineLength = 0;
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

            /// <summary>
            /// When set, breaks flushed by WhiteSpace_BreakToNextLine skip the indent -
            /// used to keep column-0 multi-line comments at column 0 (upstream #99).
            /// </summary>
            internal bool OmitIndentOnFlushedBreaks;

            internal void WhiteSpace_BreakToNextLine()
            {
                AddOutputLineBreak();
                if (!OmitIndentOnFlushedBreaks)
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

            public string? GetRecentKeyword()
            {
                string? keywordFound = null;
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
