/*
Right Way SQL Formatter - modernized T-SQL formatter
Regression tests for the max-line-width wrap fixed point: the alias/align post-passes
assemble `alias = value` lines AFTER the core tree walk has made its wrap decisions,
so a line the passes LENGTHEN (by inserting `ColumnAlias_N = `) could end up past
MaxLineWidth without the wrap the next format pass applies — format(format(x)) != format(x)
on the raw→formatted transition. WrapOverflowingAliasLiterals reproduces the core's break
(before an unbreakable single literal token) at assembly time so pass1 already equals pass2.
Each case asserts a fixed point (pass1 == pass2 == pass3).
Licensed under GNU AGPL v3.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class WidthWrapFixedPointTests
    {
        // Jeremy's real heavy editor profile.
        private const string Heavy =
            "ExpandBetweenConditions=false,ExpandBooleanExpressions=false,ExpandCaseStatements=false,"
            + "ExpandInLists=false,UppercaseKeywords=false,TrailingCommas=True,AlignTableJoins=True,"
            + "ColumnAlwaysHasAlias=True,SelectFirstColumnOnNewLine=True,AlignColumnDefinitions=True,"
            + "AlignColumnDefinitionsInDDL=True,ColumnAliasStyle=EqualSign,IndentWhereAndOrConditions=True,"
            + "MaxLineWidth=200,CompactRaiserror=True,CompactSingleStatementBlocks=True";

        // Minimal alias-in-equals-style profile with a small width so a single literal overflows.
        private const string NarrowEquals =
            "ColumnAlwaysHasAlias=True,ColumnAliasStyle=EqualSign,AlignColumnDefinitions=True,"
            + "SelectFirstColumnOnNewLine=True,MaxLineWidth=80";

        private static (string p1, string p2, string p3) FormatThrice(string sql, string config)
        {
            var mgr = new SqlFormattingManager(new TSqlStandardFormatter(new TSqlStandardFormatterOptions(config)));
            bool err = false;
            string p1 = mgr.Format(sql, ref err);
            string p2 = mgr.Format(p1, ref err);
            string p3 = mgr.Format(p2, ref err);
            return (p1, p2, p3);
        }

        private static void AssertFixedPoint(string sql, string config)
        {
            var (p1, p2, p3) = FormatThrice(sql, config);
            Assert.That(p2, Is.EqualTo(p1),
                "format is not idempotent (pass2 != pass1):\n--pass1--\n" + p1 + "\n--pass2--\n" + p2);
            Assert.That(p3, Is.EqualTo(p2), "format did not reach a fixed point by pass3");
        }

        [Test]
        public void LongStringLiteralAlias_WrapsToFixedPoint()
        {
            // ColumnAlias_1 = N'...huge...' cannot fit; the core would break before the literal,
            // putting it on its own line. The alias pass must produce that layout on pass 1.
            AssertFixedPoint(
                "select N'this is a fairly long string literal value that definitely exceeds the "
                + "configured maximum line width for sure yes indeed' from dbo.t",
                NarrowEquals);
        }

        [Test]
        public void LongBinaryLiteralAlias_WrapsToFixedPoint()
        {
            // A 0x... binary literal is a single unbreakable token — same wrap shape as a string
            // (this is the sp_HumanEvents ColumnAlias_2 = 0x... shape).
            AssertFixedPoint(
                "select 0x430052004500410054004500200056004900450057002000640062006F002E0043005200450041 "
                + "from dbo.t",
                NarrowEquals);
        }

        [Test]
        public void HeavyProfile_LongLiteralAlias_WrapsToFixedPoint()
        {
            // The ProtectSession/sp_BlitzBackups shape under Jeremy's real heavy profile: a bare
            // long string column becomes ColumnAlias_1 = N'...' that overflows 200 columns and
            // must wrap to its own line on pass 1 to match pass 2.
            AssertFixedPoint(
                "select N'" + new string('X', 210) + "' from dbo.t",
                Heavy);
        }

        [Test]
        public void WidthWrap_DoesNotStrandTrailingSpaceAtBreak()
        {
            // When the core wraps before a token too long to fit, the word-separator space it
            // already emitted must NOT be left stranded at end-of-line — the next pass strips it
            // (sp_QuickieStore / sp_PerfCheck / Private_ProcessTestAnnotations non-idempotency).
            string sql = "select '" + new string('A', 90) + "' + '" + new string('B', 90)
                + "' + '" + new string('C', 90) + "' from dbo.t";
            var (p1, p2, p3) = FormatThrice(sql, "MaxLineWidth=80");
            Assert.That(p2, Is.EqualTo(p1), "wrap is not idempotent (pass2 != pass1)");
            Assert.That(p3, Is.EqualTo(p2), "wrap did not reach a fixed point by pass3");
            // No code line in this input has a legitimate trailing space, so any is a wrap artifact.
            foreach (var line in p1.Replace("\r\n", "\n").Split('\n'))
                Assert.That(line, Is.EqualTo(line.TrimEnd()),
                    "a wrapped line was left with a stranded trailing space:\n[" + line + "]");
        }
    }
}
