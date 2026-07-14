/*
Empty-string adjacency preservation (upstream issue #200).

People format FRAGMENTS of dynamic SQL, where '' is an escaped quote:

    select ''.txt'' as fileExt      -- fragment of: '... select ''.txt'' as fileExt ...'

The formatter inserted a space between an identifier and a directly
adjacent empty string literal (''.txt'' -> ''.txt ''), silently changing
the text that gets pasted back inside the outer string - a nasty
middle-tier corruption.

Rule: an EMPTY string literal that is directly adjacent (no whitespace in
the source) to a word/value token keeps that adjacency on both sides.
Spacing after keywords (LIKE'', PRINT'') and commas is unchanged, and
strings that were separated by whitespace in the source stay separated.

Licensed under the GNU Affero General Public License v3 - see LICENSE.txt.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class EmptyStringAdjacencyTests
    {
        private static string Format(string sql, out bool errorsEncountered)
        {
            var manager = new SqlFormattingManager(new TSqlStandardFormatter());
            bool error = false;
            string result = manager.Format(sql, ref error);
            errorsEncountered = error;
            return result;
        }

        [TestCase("select ''.txt'' as fileExt", "''.txt''")]
        [TestCase("select ''abc'' as x", "''abc''")]
        [TestCase("select a.b+''.txt'' from t", "''.txt''")]
        public void AdjacentEmptyStringsStayGlued(string input, string mustContain)
        {
            string output = Format(input, out _);
            Assert.That(output, Does.Contain(mustContain),
                "source adjacency around empty strings must be preserved verbatim");
        }

        [TestCase("select ''.txt'' as fileExt")]
        [TestCase("select ''abc'' as x")]
        public void GluedOutputIsIdempotent(string input)
        {
            string pass1 = Format(input, out _);
            string pass2 = Format(pass1, out _);
            Assert.That(pass2, Is.EqualTo(pass1));
        }

        [Test]
        public void SourceWhitespaceAroundEmptyStringsIsKept()
        {
            string output = Format("select '' abc '' as x", out _);
            Assert.That(output, Does.Contain("'' abc ''"),
                "strings separated by whitespace in the source must stay separated");
        }

        [Test]
        public void KeywordSpacingIsUnchanged()
        {
            Assert.That(Format("select x from t where x like''", out _), Does.Contain("LIKE ''"),
                "keywords still get a space before an adjacent string");
            Assert.That(Format("print''", out _), Does.Contain("PRINT ''"));
        }

        [Test]
        public void CommaSpacingIsUnchanged()
        {
            Assert.That(Format("select checksum('a','')", out _), Does.Contain("'a', ''"),
                "comma-separated empty strings keep the standard space after the comma");
        }

        [Test]
        public void NonEmptyAdjacentStringsKeepCurrentBehavior()
        {
            // 'SELECT col'alias'' is valid T-SQL; the formatter has always
            // separated the string alias for readability. Only EMPTY strings
            // (escaped quotes in fragments) preserve adjacency.
            Assert.That(Format("select name'x' from t", out _), Does.Contain("name 'x'"));
        }
    }
}
