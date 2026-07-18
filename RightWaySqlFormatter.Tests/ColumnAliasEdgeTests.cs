/*
ColumnAlwaysHasAlias edge cases (corpus findings, 2026-07-17).

1) Trailing line comments: "u.Id -- 166MB (INT)" used to get its alias appended
   AFTER the -- comment, where it is dead text — and because the alias was
   invisible to the next pass, every re-format appended another one. The alias
   must be inserted before the comment: "u.Id AS Id -- 166MB (INT)".

2) AS-less bracketed aliases: "Name [Test Case Name]" is already aliased;
   the pass used to stack "AS ColumnAlias_N" on top.

Licensed under the GNU Affero General Public License v3 - see LICENSE.txt.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class ColumnAliasEdgeTests
    {
        private static string Format(string sql)
        {
            var formatter = new TSqlStandardFormatter(new TSqlStandardFormatterOptions
            {
                ColumnAlwaysHasAlias = true
            });
            var manager = new SqlFormattingManager(formatter);
            bool error = false;
            string result = manager.Format(sql, ref error);
            Assert.That(error, Is.False, "input should format without parse errors");
            return result;
        }

        [Test]
        public void AliasGoesBeforeTrailingLineComment()
        {
            string sql = "select\n    u.Id -- 166MB (INT)\n    ,u.DisplayName -- 300MB\nfrom dbo.Users u;";
            string output = Format(sql);

            Assert.That(output, Does.Contain("u.Id AS Id -- 166MB (INT)"),
                "alias must be inserted before the trailing comment, not after it");
            Assert.That(output, Does.Not.Contain("-- 166MB (INT) AS"),
                "nothing may be appended after a -- comment");
        }

        [Test]
        public void TrailingCommentAliasingIsIdempotent()
        {
            string sql = "select\n    u.Id -- 166MB (INT)\n    ,u.DisplayName -- 300MB\nfrom dbo.Users u;";
            string pass1 = Format(sql);
            string pass2 = Format(pass1);
            Assert.That(pass2, Is.EqualTo(pass1));
        }

        [Test]
        public void EqualSignRewriteKeepsTrailingCommentAtLineEnd()
        {
            var formatter = new TSqlStandardFormatter(new TSqlStandardFormatterOptions
            {
                ColumnAlwaysHasAlias = true,
                ColumnAliasStyle = ColumnAliasStyle.EqualSign
            });
            var manager = new SqlFormattingManager(formatter);
            bool error = false;
            string sql = "select\n    u.Id -- 166MB (INT)\n    ,u.DisplayName -- 300MB\nfrom dbo.Users u;";
            string pass1 = manager.Format(sql, ref error);
            Assert.That(error, Is.False);

            Assert.That(pass1, Does.Contain("Id = u.Id -- 166MB (INT)"),
                "the comment must trail the whole rewritten column, never swallow '= expr'");
            string pass2 = manager.Format(pass1, ref error);
            Assert.That(pass2, Is.EqualTo(pass1), "equals-rewrite with comments must be idempotent");
        }

        [Test]
        public void BracketedAsLessAliasIsRecognized()
        {
            string sql = "select Name [Test Case Name], Id [The Id] from dbo.t;";
            string output = Format(sql);

            Assert.That(output, Does.Contain("Name [Test Case Name]"));
            Assert.That(output, Does.Not.Contain("ColumnAlias"),
                "AS-less bracketed aliases must be recognized as existing aliases");
        }

        [Test]
        public void BracketedOperandStillGetsAlias()
        {
            // [b] here is an operand of +, not an alias — the expression is unaliased.
            string sql = "select a + [b] from dbo.t;";
            string output = Format(sql);
            Assert.That(output, Does.Contain("a + [b] AS ColumnAlias_1"));
        }

        [Test]
        public void QualifiedBracketedColumnStillGetsSelfAlias()
        {
            string sql = "select t.[Col Name] from dbo.t t;";
            string output = Format(sql);
            Assert.That(output, Does.Contain("t.[Col Name] AS [Col Name]"),
                "t.[Col] is a reference, not an aliased expression");
        }
    }
}
