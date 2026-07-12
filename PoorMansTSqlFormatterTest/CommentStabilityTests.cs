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
        public void BlockCommentAfterTrailingSingleLineCommentIsIdempotent()
        {
            // Real-world case (sp_AllNightLog_Setup): a /*block*/ comment following a
            // trailing --comment, separated by a blank line. Formatting collapses the
            // blank line (DECLARE-run exemption); the reparse must still attach the
            // block comment to the following statement or its indent drifts every pass.
            string sql = string.Join("\n",
                "DECLARE @a INT; --first",
                "",
                "/*block comment*/",
                "DECLARE @b INT; --second");

            string pass1 = Format(sql);
            string pass2 = Format(pass1);
            string pass3 = Format(pass2);

            Assert.That(pass2, Is.EqualTo(pass1), "pass 2 must equal pass 1");
            Assert.That(pass3, Is.EqualTo(pass2), "pass 3 must equal pass 2");
        }

        [Test]
        public void IndentedBlockCommentAfterSingleLineCommentIsIdempotent()
        {
            // Same as above but with indentation before the block comment (the shape
            // inside a proc body) - indentation whitespace between the two comments
            // must not defeat the line-break detection.
            string sql = string.Join("\n",
                "IF @x = 1",
                "BEGIN",
                "    DECLARE @a INT = 1; --used for things",
                "",
                "    /*these variables control the loop*/",
                "    DECLARE @b INT = 2; --loop counter",
                "END;");

            string pass1 = Format(sql);
            string pass2 = Format(pass1);

            Assert.That(pass2, Is.EqualTo(pass1), "formatting must be idempotent");
        }

        [Test]
        public void FileStartingWithBatchSeparatorIsIdempotent()
        {
            // Real-world case (tsqlt build scripts): a file whose first statement is GO
            // (possibly indented). The implicit empty leading statement must not render
            // a stray blank line that grows/shifts on reformat.
            string sql = "  GO\n  EXEC #x;\n  GO\n";

            string pass1 = Format(sql);
            string pass2 = Format(pass1);

            Assert.That(pass2, Is.EqualTo(pass1), "formatting must be idempotent");
            Assert.That(pass1, Does.StartWith("GO"), "no leading blank line before the first GO");
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
