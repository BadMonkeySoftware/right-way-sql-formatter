/*
Right Way SQL Formatter - modernized T-SQL formatter
Based on Poor Man's T-SQL Formatter by Tao Klerks
Licensed under GNU AGPL v3
*/

using System.Collections.Generic;
using PoorMansTSqlFormatterLib.Interfaces;
using PoorMansTSqlFormatterLib.ParseStructure;

namespace PoorMansTSqlFormatterLib
{
    /// <summary>
    /// Produces human-readable descriptions of parse errors from a parsed SQL tree
    /// (nodes flagged with hasError="1") and the source token list (unfinished tokens).
    /// Purely observational: never mutates the tree, so formatting output is unaffected
    /// unless a caller chooses to surface these descriptions.
    /// </summary>
    public static class ParseErrorAnalyzer
    {
        private const int MAX_DESCRIPTIONS = 10;
        private const int MAX_TOKEN_TEXT_LENGTH = 40;

        /// <summary>
        /// Returns a list of plain-English error descriptions for the given parse result.
        /// Empty list when the tree has no errorFound flag.
        /// </summary>
        public static IList<string> GetErrorDescriptions(Node sqlTree, ITokenList? tokenList)
        {
            var descriptions = new List<string>();

            if (sqlTree == null || sqlTree.GetAttributeValue(SqlStructureConstants.ANAME_ERRORFOUND) != "1")
                return descriptions;

            // 1. Unfinished token at end of input (tokenizer-level: unclosed string/comment/bracket)
            if (tokenList != null && tokenList.HasUnfinishedToken && tokenList.Count > 0)
            {
                string? unfinished = DescribeUnfinishedToken(tokenList[tokenList.Count - 1]);
                if (unfinished != null)
                    descriptions.Add(unfinished);
            }

            // 2. Nodes the parser flagged as errors. Token-level flags (the unexpected token
            //    itself) are precise; container-level flags are a side effect of the same
            //    error, so only fall back to them when no token was identified.
            var tokenDescriptions = new List<string>();
            var containerDescriptions = new List<string>();
            CollectNodeErrors(sqlTree, tokenDescriptions, containerDescriptions);

            foreach (string d in tokenDescriptions)
            {
                if (descriptions.Count >= MAX_DESCRIPTIONS) break;
                if (!descriptions.Contains(d)) descriptions.Add(d);
            }
            if (tokenDescriptions.Count == 0)
            {
                foreach (string d in containerDescriptions)
                {
                    if (descriptions.Count >= MAX_DESCRIPTIONS) break;
                    if (!descriptions.Contains(d)) descriptions.Add(d);
                }
            }

            // 3. Nothing specific found, but the tree is flagged — typically an incomplete
            //    statement at end of input (e.g. unclosed parenthesis or block).
            if (descriptions.Count == 0)
                descriptions.Add("Statement incomplete at end of input (possibly an unclosed parenthesis, BEGIN/END block, or CASE expression)");

            return descriptions;
        }

        private static void CollectNodeErrors(Node node, List<string> tokenDescriptions, List<string> containerDescriptions)
        {
            if (tokenDescriptions.Count >= MAX_DESCRIPTIONS)
                return;

            if (node.GetAttributeValue(SqlStructureConstants.ANAME_HASERROR) == "1")
            {
                var (description, isToken) = DescribeErrorNode(node);
                if (description != null)
                {
                    List<string> target = isToken ? tokenDescriptions : containerDescriptions;
                    if (!target.Contains(description))
                        target.Add(description);
                }
            }

            foreach (Node child in node.Children)
            {
                if (tokenDescriptions.Count >= MAX_DESCRIPTIONS)
                    return;
                CollectNodeErrors(child, tokenDescriptions, containerDescriptions);
            }
        }

        private static (string? description, bool isToken) DescribeErrorNode(Node node)
        {
            // Leaf tokens with text: report the offending token itself.
            string? text = node.TextValue;
            if (string.IsNullOrWhiteSpace(text))
                text = node.GetAttributeValue(SqlStructureConstants.ANAME_SIMPLETEXT);

            if (!string.IsNullOrWhiteSpace(text))
            {
                string cleaned = CleanTokenText(text!);
                switch (node.Name)
                {
                    case SqlStructureConstants.ENAME_OTHERKEYWORD:
                    case SqlStructureConstants.ENAME_COMPOUNDKEYWORD:
                        return ("Unexpected or misplaced keyword '" + cleaned + "'", true);
                    case SqlStructureConstants.ENAME_OTHEROPERATOR:
                        return ("Unexpected operator '" + cleaned + "'", true);
                    default:
                        return ("Unexpected token '" + cleaned + "'", true);
                }
            }

            // Containers flagged with an error (e.g. parsing ended inside this structure).
            string? firstText = FirstDescendantText(node);
            if (firstText != null)
                return ("Incomplete or invalid structure starting near '" + CleanTokenText(firstText) + "'", false);
            return ("Incomplete or invalid '" + node.Name + "' structure", false);
        }

        private static string? DescribeUnfinishedToken(IToken lastToken)
        {
            switch (lastToken.Type)
            {
                case SqlTokenType.MultiLineComment:
                    return "Unclosed block comment (missing comment-end marker)";
                case SqlTokenType.String:
                case SqlTokenType.NationalString:
                    return "Unclosed string literal (missing closing single-quote)";
                case SqlTokenType.QuotedString:
                    return "Unclosed quoted identifier/string (missing closing double-quote)";
                case SqlTokenType.BracketQuotedName:
                    return "Unclosed bracket-quoted identifier (missing ']')";
                default:
                    return "Unfinished token at end of input: '" + CleanTokenText(lastToken.Value) + "'";
            }
        }

        private static string? FirstDescendantText(Node node)
        {
            foreach (Node child in node.Children)
            {
                if (child.Name == SqlStructureConstants.ENAME_WHITESPACE)
                    continue;
                if (!string.IsNullOrWhiteSpace(child.TextValue))
                    return child.TextValue;
                string? fromChild = FirstDescendantText(child);
                if (fromChild != null)
                    return fromChild;
            }
            return null;
        }

        /// <summary>
        /// Makes token text safe for inclusion in a single-line SQL comment:
        /// collapses newlines/tabs to spaces and truncates long values.
        /// </summary>
        private static string CleanTokenText(string text)
        {
            string cleaned = text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            while (cleaned.Contains("  "))
                cleaned = cleaned.Replace("  ", " ");
            if (cleaned.Length > MAX_TOKEN_TEXT_LENGTH)
                cleaned = cleaned.Substring(0, MAX_TOKEN_TEXT_LENGTH) + "...";
            return cleaned;
        }
    }
}
