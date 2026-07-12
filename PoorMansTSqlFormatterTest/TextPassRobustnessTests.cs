/*
Right Way SQL Formatter - modernized T-SQL formatter
Tests: the text-based post-processing passes (align/alias rewriting) must never
corrupt string literals, comments, or statement terminators.
Licensed under GNU AGPL v3
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class TextPassRobustnessTests
    {
        private const string ALIGN_EQUALS = "AlignColumnDefinitions=True,ColumnAliasStyle=EqualSign";

        private static string Format(string sql, string configString)
        {
            var manager = new SqlFormattingManager(new TSqlStandardFormatter(new TSqlStandardFormatterOptions(configString)));
            bool errors = false;
            string output = manager.Format(sql, ref errors);
            Assert.That(errors, Is.False, "input must parse cleanly");
            return output;
        }

        [Test]
        public void DynamicSqlStringContentIsNeverRewritten()
        {
            // sp_WhoIsActive-style dynamic SQL: the string interior contains SELECT and
            // TOP(@i) with no space - it is DATA and must pass through byte-identical.
            string sql = string.Join("\n",
                "DECLARE @sql NVARCHAR(MAX);",
                "SET @sql = N'",
                "SELECT TOP(@i)",
                "    session_id AS s,",
                "    login_name AS l",
                "FROM sys.dm_exec_sessions';",
                "EXEC sys.sp_executesql @sql;");

            string output = Format(sql, ALIGN_EQUALS);

            Assert.That(output, Does.Contain("SELECT TOP(@i)"),
                "string interior must not be reformatted or crash the TOP parser");
            Assert.That(output, Does.Contain("session_id AS s,"),
                "aliases inside the string literal must not be rewritten to = style");
        }

        [Test]
        public void TrailingSemicolonSurvivesEqualsRewrite()
        {
            // 'SELECT expr AS alias;' must become 'alias = expr;' - never 'alias; = expr'
            string output = Format(
                "CREATE VIEW v AS SELECT CAST('Windows' AS NVARCHAR(256)) AS host_platform;",
                "ColumnAliasStyle=EqualSign");

            Assert.That(output, Does.Contain("host_platform = CAST('Windows' AS NVARCHAR(256));"));
            Assert.That(output, Does.Not.Contain("host_platform; ="));
        }

        [Test]
        public void TrailingSemicolonSurvivesEnsureAlias()
        {
            string output = Format("SELECT foo; SELECT bar;", "ColumnAlwaysHasAlias=True");

            Assert.That(output, Does.Contain("foo AS foo;"));
            Assert.That(output, Does.Not.Contain("foo; AS"));
        }

        [Test]
        public void CommentContainingAsIsNotTreatedAsAlias()
        {
            // The block comment contains ' as ' - the rewrite passes must not split on it.
            string sql = "INSERT INTO #t (c)\nVALUES (93);/* same drive as data - history */\nSELECT x AS y FROM t;";

            string pass1 = Format(sql, ALIGN_EQUALS);
            string pass2 = Format(pass1, ALIGN_EQUALS);

            Assert.That(pass1, Does.Contain("/* same drive as data - history */"),
                "comment must survive intact");
            Assert.That(pass2, Is.EqualTo(pass1), "formatting must be idempotent");
        }

        [Test]
        public void AlignEqualsProfileIsIdempotentOnMixedContent()
        {
            string sql = string.Join("\n",
                "SELECT a.Col1 AS First, a.Col2 AS SecondOne, a.Col3",
                "FROM SomeTable a",
                "WHERE a.Active = 1;");

            string pass1 = Format(sql, ALIGN_EQUALS);
            string pass2 = Format(pass1, ALIGN_EQUALS);

            Assert.That(pass2, Is.EqualTo(pass1));
        }
    }
}
