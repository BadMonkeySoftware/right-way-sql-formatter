/*
Right Way SQL Formatter - modernized T-SQL formatter
ScriptDom-based validation oracle (TEST-ONLY dependency - never shipped).

Microsoft.SqlServer.TransactSql.ScriptDom is the official T-SQL parser (used by
SSDT/DacFx). We use it as ground truth: if the INPUT parses clean under the real
grammar, the formatter's OUTPUT must parse clean too - any new parse error means
the formatter corrupted the SQL. This catches semantic breakage our own forgiving
parser cannot judge (it is the parser whose output is being tested).

Licensed under GNU AGPL v3.
*/

using System.Collections.Generic;
using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace PoorMansTSqlFormatterTests
{
    internal static class ScriptDomOracle
    {
        /// <summary>
        /// Parses with the current-generation T-SQL grammar and returns human-readable
        /// parse errors ("line X, col Y: message"); empty list = clean parse.
        /// </summary>
        public static IList<string> GetParseErrors(string sql)
        {
            var parser = new TSql170Parser(initialQuotedIdentifiers: true);
            IList<ParseError> errors;
            using (var reader = new StringReader(sql))
                parser.Parse(reader, out errors);

            var result = new List<string>();
            foreach (ParseError error in errors)
                result.Add($"line {error.Line}, col {error.Column}: {error.Message}");
            return result;
        }

        public static bool ParsesClean(string sql) => GetParseErrors(sql).Count == 0;
    }
}
