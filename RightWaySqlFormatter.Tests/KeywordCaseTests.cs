/*
Keyword-list cleanup (upstream issue #272).

DEFINITION and STATUS were in the parser's keyword dictionary despite having
no T-SQL keyword role anywhere in the grammar - they are, however, extremely
common catalog/table column names (sys.sql_modules.definition,
sys.dm_exec_requests.status). Being classified as keywords meant:

  - keyword-uppercasing rewrote the user's identifier case
    (SELECT definition -> SELECT DEFINITION), and
  - removeHarmlessBrackets refused to unbracket [definition]/[status].

Genuine soft keywords (LEVEL in ISOLATION LEVEL, STATE in endpoint DDL,
MATCHED in MERGE, ...) stay in the dictionary: evicting those would leave
"set transaction isolation level" half-lowercased. Genuinely reserved words
and niladic functions ([Order], [User]) still keep their brackets - USER
unbracketed would silently mean the USER() function, not the column.

Licensed under the GNU Affero General Public License v3 - see LICENSE.txt.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class KeywordCaseTests
    {
        private static string Format(string sql, bool removeHarmlessBrackets = false)
        {
            var formatter = new TSqlStandardFormatter(new TSqlStandardFormatterOptions
            {
                RemoveHarmlessBrackets = removeHarmlessBrackets
            });
            var manager = new SqlFormattingManager(formatter);
            bool error = false;
            string result = manager.Format(sql, ref error);
            Assert.That(error, Is.False);
            return result;
        }

        [TestCase("select definition from sys.sql_modules", "definition")]
        [TestCase("select Definition from sys.sql_modules", "Definition")]
        [TestCase("select status from sys.dm_exec_requests", "status")]
        [TestCase("select r.Status from dbo.Requests r", "r.Status")]
        public void IdentifierCaseIsPreserved(string input, string mustContain)
        {
            // upstream #272: these are column names, not keywords - the user's
            // casing must survive keyword-uppercasing.
            Assert.That(Format(input), Does.Contain(mustContain));
        }

        [TestCase("select [definition] from sys.sql_modules", "definition")]
        [TestCase("select [status], [t].[definition] from t", "status")]
        [TestCase("select [status], [t].[definition] from t", "t.definition")]
        public void BracketsNowRemovableForEvictedWords(string input, string mustContain)
        {
            string pass1 = Format(input, removeHarmlessBrackets: true);
            Assert.That(pass1, Does.Contain(mustContain));
            string pass2 = Format(pass1, removeHarmlessBrackets: true);
            Assert.That(pass2, Is.EqualTo(pass1),
                "unbracketed evicted words must not get re-uppercased on the next pass");
        }

        [Test]
        public void SoftKeywordsStillUppercase()
        {
            Assert.That(Format("set transaction isolation level read committed"),
                Does.Contain("SET TRANSACTION ISOLATION LEVEL READ COMMITTED"));
            Assert.That(Format("select * from t with (nolock)"), Does.Contain("NOLOCK"));
            Assert.That(Format("merge t using s on t.id = s.id when matched then update set x = 1;"),
                Does.Contain("WHEN MATCHED"));
        }

        [Test]
        public void ReservedAndNiladicNamesStillKeepBrackets()
        {
            string output = Format("select [Order], [User], [Some] from t", removeHarmlessBrackets: true);
            Assert.That(output, Does.Contain("[Order]"));
            Assert.That(output, Does.Contain("[User]"), "USER unbracketed would mean the niladic function");
            Assert.That(output, Does.Contain("[Some]"));
        }
    }
}
