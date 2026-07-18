/*
ColumnAlwaysHasAlias vs complex TOP modifiers (corpus finding, 2026-07-17).

StripSelectModifierPrefix located the end of a TOP (...) argument with
IndexOf(')') — the FIRST close paren, not the matching one. A nested call like
SELECT TOP (LEN(ISNULL(@s, N''))) severed the TOP expression mid-call, and the
auto-alias pass then appended "AS ColumnAlias_1" to the severed modifier,
corrupting the statement (and compounding on every re-format).

Licensed under the GNU Affero General Public License v3 - see LICENSE.txt.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class ColumnAliasTopModifierTests
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
        public void TopWithNestedFunctionCallsIsNeverAliased()
        {
            string sql = "select top (len(isnull(@s, N''))) row_number() over (order by n.Number) as x from dbo.Numbers n;";
            string output = Format(sql);

            Assert.That(output, Does.Contain("TOP (LEN(ISNULL(@s, N'')))"),
                "the TOP argument must survive as one expression");
            Assert.That(output, Does.Not.Contain("))) AS ColumnAlias"),
                "a TOP modifier must never be given a column alias");
        }

        [Test]
        public void TopWithNestedFunctionCallsIsIdempotent()
        {
            string sql = "select top (len(isnull(@s, N''))) row_number() over (order by n.Number) as x from dbo.Numbers n;";
            string pass1 = Format(sql);
            string pass2 = Format(pass1);
            Assert.That(pass2, Is.EqualTo(pass1));
        }
    }
}
