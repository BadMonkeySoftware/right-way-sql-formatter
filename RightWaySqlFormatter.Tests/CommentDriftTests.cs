/*
Right Way SQL Formatter - modernized T-SQL formatter
Regression tests for comment-position drift: the align passes used to re-pad
comments a little more on every pass (unbounded rightward ratchet), or shift a
comment's spacing, so format(format(x)) != format(x). Each case asserts a fixed
point (pass1 == pass2 == pass3) AND that the comment text comes out byte-identical.
Licensed under GNU AGPL v3.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class CommentDriftTests
    {
        private const string Heavy =
            "ExpandBetweenConditions=false,ExpandBooleanExpressions=false,ExpandCaseStatements=false,"
            + "ExpandInLists=false,UppercaseKeywords=false,TrailingCommas=True,AlignTableJoins=True,"
            + "ColumnAlwaysHasAlias=True,SelectFirstColumnOnNewLine=True,AlignColumnDefinitions=True,"
            + "AlignColumnDefinitionsInDDL=True,ColumnAliasStyle=EqualSign,IndentWhereAndOrConditions=True,"
            + "MaxLineWidth=200,CompactRaiserror=True,CompactSingleStatementBlocks=True";

        private static (string p1, string p2, string p3) FormatThrice(string sql, string config)
        {
            var mgr = new SqlFormattingManager(new TSqlStandardFormatter(new TSqlStandardFormatterOptions(config)));
            bool err = false;
            string p1 = mgr.Format(sql, ref err);
            string p2 = mgr.Format(p1, ref err);
            string p3 = mgr.Format(p2, ref err);
            return (p1, p2, p3);
        }

        private static void AssertStable(string sql, string config, string? commentMustSurvive = null)
        {
            var (p1, p2, p3) = FormatThrice(sql, config);
            Assert.That(p2, Is.EqualTo(p1), "format is not idempotent (pass2 != pass1):\n--pass1--\n" + p1 + "\n--pass2--\n" + p2);
            Assert.That(p3, Is.EqualTo(p2), "format did not reach a fixed point by pass3");
            if (commentMustSurvive != null)
                Assert.That(p1, Does.Contain(commentMustSurvive), "comment text was altered by the align pass");
        }

        [Test]
        public void CommentOnlyLineInsideCreateTable_NotPadded()
        {
            // The /* ˅˅˅˅˅ */ pointer comments must not gain padding each pass (the ratchet).
            AssertStable(
                "create table dbo.t (\n"
                + "    id            integer primary key,\n"
                + "    /*            arrow Pay attention to this */\n"
                + "    account_id    bigint not null,\n"
                + "    creation_date datetime not null\n"
                + ");",
                Heavy,
                "/*            arrow Pay attention to this */");
        }

        [Test]
        public void DashCommentLinesInParamList_NotPadded()
        {
            // -- comment lines interleaved with aligned proc parameters must not be padded.
            AssertStable(
                "create proc dbo.p (\n"
                + "    --~ some doc\n"
                + "    --Filters here\n"
                + "    @filter int = 0,\n"
                + "    @filter_type varchar(10) = 'x'\n"
                + ") as select 1 c;",
                Heavy,
                "--Filters here");
        }

        [Test]
        public void MultiLineBlockCommentInsideCreateTable_InteriorNotPadded()
        {
            // interior lines of a multi-line /* ... */ must be left byte-identical.
            AssertStable(
                "create table dbo.t (\n"
                + "    a_col         int,\n"
                + "    /*The Memory Grant columns are only supported\n"
                + "\t\t  in                            certain versions, giggle giggle.\n"
                + "\t\t*/\n"
                + "    b_column      bigint,\n"
                + "    c             money\n"
                + ");",
                Heavy,
                "in                            certain versions");
        }

        [Test]
        public void InlineDashCommentAfterSemicolon_SpacingStable()
        {
            // "expr; --comment" (aliased) must not drift the space before -- (core uses ";--").
            AssertStable("SELECT @n1 a, @n2 b, @n3; --exec test case", Heavy);
        }
    }
}
