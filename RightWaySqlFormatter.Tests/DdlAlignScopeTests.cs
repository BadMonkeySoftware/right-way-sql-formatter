/*
AlignColumnDefinitionsInDDL scope (corpus finding, 2026-07-17).

The parser reuses the DDLParens element for INSERT column lists, VALUES tuples,
and OPTION(...) hints. The DDL column-alignment pass used to rewrite ALL of
them as if they were "name type" column definitions, inserting its padding
INSIDE string literals (N'great match' -> N'great  match', growing on every
re-format) and inside function calls (CONVERT(vector(4,   float32), ...)) —
semantic corruption, and the main driver of heavy-profile non-idempotency on
the real-world corpus.

The pass must only rewrite parens whose parent is a real DDL block
(CREATE/ALTER object bodies, DECLARE @t TABLE); everything else sharing the
element name is arbitrary expression content and stays untouched.

Licensed under the GNU Affero General Public License v3 - see LICENSE.txt.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class DdlAlignScopeTests
    {
        private static string Format(string sql, bool trailingCommas = false)
        {
            var formatter = new TSqlStandardFormatter(new TSqlStandardFormatterOptions
            {
                AlignColumnDefinitionsInDDL = true,
                TrailingCommas = trailingCommas
            });
            var manager = new SqlFormattingManager(formatter);
            bool error = false;
            string result = manager.Format(sql, ref error);
            Assert.That(error, Is.False, "input should format without parse errors");
            return result;
        }

        [Test]
        public void InsertValuesStringLiteralsAreNeverPadded([Values(false, true)] bool trailingCommas)
        {
            string sql = "insert dbo.t (a, b, c) values (N'great match', N'good', convert(vector(4, float32), N'[0.5]'));";
            string output = Format(sql, trailingCommas);

            Assert.That(output, Does.Contain("N'great match'"),
                "string literal content must survive formatting untouched");
            Assert.That(output, Does.Contain("vector(4, float32)"),
                "function-call arguments must not be align-padded");
        }

        [Test]
        public void InsertValuesFormattingIsIdempotent([Values(false, true)] bool trailingCommas)
        {
            string sql = "insert dbo.t (a, b, c) values (N'great match', N'good', convert(vector(4, float32), N'[0.5]'));";
            string pass1 = Format(sql, trailingCommas);
            string pass2 = Format(pass1, trailingCommas);
            Assert.That(pass2, Is.EqualTo(pass1), "re-formatting formatted output must be a no-op");
        }

        // A computed column (col AS <expr>) whose expression wraps produces continuation
        // lines that can BEGIN with a string literal. The DDL-align pass tokenizes on
        // whitespace, so it split `N'EXEC dbo.thing @p='` at the space INSIDE the literal
        // and padded there - injecting spaces into the string and ratcheting it rightward
        // on every re-format (unbounded, non-convergent; the sp_BlitzIndex/sp_BlitzCache
        // index_definition corpus bug). A real column name never contains a quote, so such
        // lines are expression continuations, not column definitions, and must be skipped.
        private static string FormatNarrowDdl(string sql)
        {
            var formatter = new TSqlStandardFormatter(new TSqlStandardFormatterOptions
            {
                AlignColumnDefinitionsInDDL = true,
                MaxLineWidth = 70,
            });
            bool error = false;
            string result = new SqlFormattingManager(formatter).Format(sql, ref error);
            Assert.That(error, Is.False, "input should format without parse errors");
            return result;
        }

        private const string ComputedColumnWithWrappingStringExpr =
            "create table #t (col_one int null, long_column_name_here nvarchar(max) null, "
            + "computed_col as N'first part text' + N'second part more text here' + "
            + "N'EXEC dbo.thing @p=' + N'another chunk of literal text goes here' + N'trailing part');";

        [Test]
        public void ComputedColumnWrappedStringLiteralIsNeverPaddedInside()
        {
            string output = FormatNarrowDdl(ComputedColumnWithWrappingStringExpr);
            Assert.That(output, Does.Contain("N'EXEC dbo.thing @p='"),
                "a string literal starting a wrapped continuation line must keep its exact spacing");
            Assert.That(output, Does.Contain("N'trailing part'"),
                "string literal content must survive DDL alignment untouched");
        }

        [Test]
        public void ComputedColumnWrappedStringExprIsIdempotent()
        {
            string p1 = FormatNarrowDdl(ComputedColumnWithWrappingStringExpr);
            string p2 = FormatNarrowDdl(p1);
            string p3 = FormatNarrowDdl(p2);
            Assert.That(p2, Is.EqualTo(p1),
                "re-formatting must not ratchet spaces into the string literal (was unbounded drift)");
            Assert.That(p3, Is.EqualTo(p2), "fixed point by pass 3");
        }

        [Test]
        public void CreateTableColumnDefinitionsAreStillAligned()
        {
            string sql = "create table dbo.t (Id int not null, LongColumnName nvarchar(50) null);";
            string output = Format(sql);

            // Alignment pads between column name and type so the types line up:
            // the short name gains more padding than a single space.
            Assert.That(output, Does.Contain("Id             INT"),
                "genuine DDL column definitions must still be aligned");
            Assert.That(output, Does.Contain("LongColumnName NVARCHAR(50)"));
        }

        [Test]
        public void DeclareTableColumnDefinitionsAreStillAligned()
        {
            string sql = "declare @t table (Id int not null, LongColumnName nvarchar(50) null);";
            string output = Format(sql);
            Assert.That(output, Does.Contain("Id             INT"),
                "DECLARE @t TABLE definitions must still be aligned");
        }
    }
}
