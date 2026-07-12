/*
Right Way SQL Formatter - modernized T-SQL formatter
Tests: comment placement must be stable across repeated formatting.
Licensed under GNU AGPL v3
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class CommentStabilityTests
    {
        private static string Format(string sql)
        {
            var manager = new SqlFormattingManager(new TSqlStandardFormatter(new TSqlStandardFormatterOptions()));
            bool errors = false;
            string output = manager.Format(sql, ref errors);
            Assert.That(errors, Is.False, "input must parse cleanly");
            return output;
        }

        [Test]
        public void TrailingCommentAfterMultilineFunctionArgsStaysInline()
        {
            // Real-world case (DarlingData sp_IndexCleanup): a source line-break inside
            // function-call arguments must not push a trailing comment onto its own line;
            // doing so re-attaches the comment to the next clause on reformat and the
            // indent drifts between passes.
            string sql = string.Join("\n",
                "UPDATE ia",
                "SET",
                "    ia.superseded_by = N'Supersedes ' +",
                "    REPLACE",
                "    (",
                "        kdd.index_list,",
                "        ia.index_name + N', ',",
                "        N''",
                "    ) /* Remove self from list if present */",
                "FROM #index_analysis AS ia",
                "JOIN #kdd AS kdd",
                "  ON ia.scope_hash = kdd.scope_hash",
                "WHERE ia.index_name = kdd.winning_index_name",
                "OPTION(RECOMPILE);");

            string pass1 = Format(sql);
            string pass2 = Format(pass1);

            Assert.That(pass1, Does.Contain("N'') /* Remove self from list if present */"),
                "trailing comment must stay on the same line as the closing paren");
            Assert.That(pass2, Is.EqualTo(pass1), "formatting must be idempotent");
        }
    }
}
