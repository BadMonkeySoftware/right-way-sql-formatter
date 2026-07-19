/*
Right Way SQL Formatter - modernized T-SQL formatter
Tests: max-line-width handling must never split a single token.
Licensed under GNU AGPL v3
*/

using System.Text.RegularExpressions;
using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class LineWidthTests
    {
        private static string Format(string sql, TSqlStandardFormatterOptions options)
        {
            var manager = new SqlFormattingManager(new TSqlStandardFormatter(options));
            bool errors = false;
            string output = manager.Format(sql, ref errors);
            Assert.That(errors, Is.False, "input must parse cleanly");
            return output;
        }

        [Test]
        public void LongBinaryLiteralIsNeverSplit()
        {
            // Real-world case (sp_HumanEvents, DarlingData installers): multi-KB varbinary
            // literals far beyond MaxLineWidth. Splitting one changes the SQL's meaning.
            string hex = string.Concat(System.Linq.Enumerable.Repeat("43005200", 300)); // 2400 chars
            string sql = "SELECT a = 1, b = 0x" + hex + ", c = 2";

            string output = Format(sql, new TSqlStandardFormatterOptions()); // default MaxLineWidth=999

            Match literal = Regex.Match(output, @"0x[0-9A-Fa-f]+");
            Assert.That(literal.Success, Is.True);
            Assert.That(literal.Value.Length, Is.EqualTo(2 + hex.Length),
                "binary literal must survive formatting as one contiguous token");
        }

        [Test]
        public void LongBinaryLiteralFormattingIsIdempotent()
        {
            string hex = string.Concat(System.Linq.Enumerable.Repeat("AB", 1500)); // 3000 chars
            string sql = "INSERT #t (v) SELECT 0x" + hex + ";";
            var options = new TSqlStandardFormatterOptions();

            string pass1 = Format(sql, options);
            string pass2 = Format(pass1, options);

            Assert.That(pass2, Is.EqualTo(pass1), "reformatting formatted output must be stable");
        }

        [Test]
        public void MultiLineStringLiteralWrapIsIdempotentAcrossCrlfNormalization()
        {
            // Root cause of heavy-profile idempotency drift (first-responder-kit sp_kill,
            // maintenance-solution, DarlingData): the SELECT-align text pass rejoined
            // lines with Environment.NewLine, silently rewriting the CRLF *inside* a
            // multi-line string literal to LF. That both (a) altered the literal's value
            // and (b) shrank its measured width, so the core wrap flipped between passes
            // (pass 1 saw CRLF interiors, pass 2 saw LF) and oscillated forever. The
            // post-passes now preserve each line's original terminator. Input uses CRLF
            // interiors deliberately.
            var options = new TSqlStandardFormatterOptions
            {
                MaxLineWidth = 65,
                AlignColumnDefinitions = true,
                ColumnAliasStyle = ColumnAliasStyle.EqualSign,
                TrailingCommas = true,
            };
            string sql =
                "SELECT\r\n"
                + "  bigdynamicsqlcolumn = N'line one of the first chunk here,\r\n"
                + "line two continues onward,\r\nthree' + N'second chunk begins now,\r\n"
                + "and keeps going for a while,\r\nend' + N'third chunk near the edge,\r\n"
                + "more text,\r\nfin' + @tail,\r\n"
                + "  another = b\r\n"
                + "FROM t;\r\n";

            string pass1 = Format(sql, options);
            string pass2 = Format(pass1, options);

            Assert.That(pass2, Is.EqualTo(pass1),
                "wrap around a multi-line literal must reach a fixed point regardless of interior newline style");
            // Correctness half: the align pass must not clobber CRLF inside the literal
            // (the dynamic-SQL text the user pastes back). The source's interior newlines
            // are CRLF, so at least some must survive.
            Assert.That(pass1, Does.Contain("first chunk here,\r\n"),
                "multi-line string literal interior newlines must be preserved verbatim, not normalized");
        }

        [Test]
        public void TokensLongerThanMaxLineWidthSurviveTinyWidth()
        {
            // Adversarial: width far smaller than the tokens themselves.
            var options = new TSqlStandardFormatterOptions { MaxLineWidth = 20 };
            string sql = "SELECT LongColumnNameThatExceedsTheWidth = 0xABCDEF0123456789ABCDEF0123456789, "
                + "s = 'a string literal well beyond twenty characters', "
                + "q = [a bracketed identifier beyond the width]"
                + " FROM dbo.SomeVeryLongTableNameIndeed";

            string output = Format(sql, options);

            Assert.That(Regex.Match(output, @"0x[0-9A-Fa-f]+").Value.Length, Is.EqualTo(34));
            Assert.That(output, Does.Contain("'a string literal well beyond twenty characters'"));
            Assert.That(output, Does.Contain("[a bracketed identifier beyond the width]"));
        }
    }
}
