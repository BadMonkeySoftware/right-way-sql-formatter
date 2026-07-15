/*
RemoveHarmlessBrackets option (upstream issue #133).

Opt-in setting (default FALSE - existing formatting untouched) that strips
square brackets from names where they are provably unnecessary:

  - the name is a valid regular identifier (letter/underscore start,
    letters/digits/underscores only), AND
  - it is not in the T-SQL keyword list (so [Order], [User], [definition]
    keep their brackets - also prevents keyword-uppercasing on reformat), AND
  - it is not directly adjacent to a token it would merge with when the
    brackets disappear (interacts with the #240 adjacency preservation:
    table_[some_id] must never become table_some_id).

Licensed under the GNU Affero General Public License v3 - see LICENSE.txt.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class RemoveHarmlessBracketsTests
    {
        private static string Format(string sql, bool removeHarmlessBrackets, out bool errorsEncountered)
        {
            var formatter = new TSqlStandardFormatter(new TSqlStandardFormatterOptions
            {
                RemoveHarmlessBrackets = removeHarmlessBrackets
            });
            var manager = new SqlFormattingManager(formatter);
            bool error = false;
            string result = manager.Format(sql, ref error);
            errorsEncountered = error;
            return result;
        }

        [Test]
        public void DefaultOffLeavesBracketsAlone()
        {
            string output = Format("SELECT [Name], [dbo].[MyTable].[Id] FROM [dbo].[MyTable]", false, out _);
            Assert.That(output, Does.Contain("[Name]"));
            Assert.That(output, Does.Contain("[dbo].[MyTable]"));
        }

        [TestCase("SELECT [Name] FROM [dbo].[MyTable]", "Name")]
        [TestCase("SELECT [Name] FROM [dbo].[MyTable]", "dbo.MyTable")]
        [TestCase("SELECT [t].[Col_1] FROM [MyTable] [t]", "t.Col_1")]
        [TestCase("SELECT [t].[Col_1] FROM [MyTable] [t]", "MyTable t")]
        [TestCase("SELECT [_x] FROM t", "_x")]
        public void HarmlessBracketsAreRemoved(string input, string mustContain)
        {
            string output = Format(input, true, out bool errors);
            Assert.That(errors, Is.False);
            Assert.That(output, Does.Contain(mustContain));
        }

        // Reserved words / keyword-list members must keep their brackets:
        // unbracketed they would parse differently and/or get uppercased.
        [TestCase("SELECT [Order] FROM t", "[Order]")]
        [TestCase("SELECT [User] FROM t", "[User]")]
        [TestCase("SELECT [From] FROM t", "[From]")]
        [TestCase("SELECT [Some] FROM t", "[Some]")]
        [TestCase("SELECT [definition] FROM sys.sql_modules", "[definition]")]
        // Not valid regular identifiers:
        [TestCase("SELECT [Some Name] FROM t", "[Some Name]")]
        [TestCase("SELECT [2Col] FROM t", "[2Col]")]
        [TestCase("SELECT [a-b] FROM t", "[a-b]")]
        [TestCase("SELECT [a]]b] FROM t", "[a]]b]")]
        [TestCase("SELECT [@v] FROM t", "[@v]")]
        [TestCase("SELECT [#tmp] FROM t", "[#tmp]")]
        public void RiskyBracketsAreKept(string input, string mustContain)
        {
            string output = Format(input, true, out _);
            Assert.That(output, Does.Contain(mustContain));
        }

        [Test]
        public void AdjacentBracketsAreKept()
        {
            // #240 adjacency: stripping would merge tokens (table_some_id).
            string output = Format("select * from table_[some_id]", true, out _);
            Assert.That(output, Does.Contain("table_[some_id]"));
            Assert.That(output, Does.Not.Contain("table_some_id"));
        }

        [Test]
        public void IdempotentAndScriptDomClean()
        {
            string input = "SELECT [Name], [Order], [t].[Col_1] FROM [dbo].[MyTable] [t] WHERE [t].[Col_1] = 1";
            Assume.That(ScriptDomOracle.ParsesClean(input), Is.True);
            string pass1 = Format(input, true, out bool errors);
            Assert.That(errors, Is.False);
            string pass2 = Format(pass1, true, out _);
            Assert.That(pass2, Is.EqualTo(pass1), "must be idempotent");
            Assert.That(ScriptDomOracle.GetParseErrors(pass1), Is.Empty,
                "unbracketed output must remain valid T-SQL");
        }

        [Test]
        public void OptionRoundTripsThroughSerialization()
        {
            var options = new TSqlStandardFormatterOptions { RemoveHarmlessBrackets = true };
            var reparsed = new TSqlStandardFormatterOptions(options.ToSerializedString());
            Assert.That(reparsed.RemoveHarmlessBrackets, Is.True);
        }
    }
}
