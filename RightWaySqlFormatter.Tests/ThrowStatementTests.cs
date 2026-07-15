/*
THROW statement support (upstream issue #266).

THROW (SQL Server 2012+) is a statement keyword:

    IF @p IS NULL THROW 51001, '@p is null', 1;

The parser historically did not recognize THROW as a statement starter, so
after "IF <condition>" the THROW and its whole argument list - semicolon
included - were swallowed into the BooleanExpression, the NEXT statement
became the IF's body, and consecutive IF...THROW lines produced a parse
error plus an ever-deepening indent cascade.

Licensed under the GNU Affero General Public License v3 - see LICENSE.txt.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;
using PoorMansTSqlFormatterLib.Interfaces;
using PoorMansTSqlFormatterLib.ParseStructure;
using PoorMansTSqlFormatterLib.Parsers;
using PoorMansTSqlFormatterLib.Tokenizers;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class ThrowStatementTests
    {
        private static readonly string[] ThrowInputs =
        {
            // upstream #266 - consecutive IF ... THROW without BEGIN/END
            @"IF @persistence_name_suffix IS NULL THROW 51001, '@persistence_name_suffix is null', 1;
IF @source_RepoObject_guid IS NULL AND @persistence_RepoObject_guid IS NULL THROW 51002, 'both null', 1;
IF NOT @persistence_RepoObject_guid IS NULL AND @source_RepoObject_guid IS NULL
BEGIN
	SELECT 1
END",

            // THROW as the IF body, followed by an unrelated statement
            @"IF @p IS NULL THROW 51001, '@p is null', 1;
SELECT 2;",

            // parameterless re-throw in CATCH
            @"BEGIN TRY
	SELECT 1;
END TRY
BEGIN CATCH
	THROW;
END CATCH;",

            // top-level THROW statement
            @"SELECT 1;
THROW 50000, 'boom', 1;",

            // ELSE branch
            @"IF @p = 1
	SELECT 1;
ELSE THROW 50001, 'unexpected', 1;",
        };

        private static Node Parse(string sql)
        {
            var tokens = new TSqlStandardTokenizer().TokenizeSQL(sql);
            return new TSqlStandardParser().ParseSQL(tokens);
        }

        private static string Format(string sql, out bool errorsEncountered)
        {
            var manager = new SqlFormattingManager(new TSqlStandardFormatter());
            bool error = false;
            string result = manager.Format(sql, ref error);
            errorsEncountered = error;
            return result;
        }

        [Test, TestCaseSource(nameof(ThrowInputs))]
        public void ParsesWithoutError(string sql)
        {
            Node parsed = Parse(sql);
            Assert.That(parsed.GetAttributeValue(SqlStructureConstants.ANAME_ERRORFOUND), Is.Null,
                "IF ... THROW is valid T-SQL and must not be flagged as a parse error");
        }

        [Test, TestCaseSource(nameof(ThrowInputs))]
        public void ThrowNeverSwallowedIntoBooleanExpression(string sql)
        {
            Node parsed = Parse(sql);
            foreach (Node boolExpr in AllDescendants(parsed))
            {
                if (!boolExpr.Name.Equals(SqlStructureConstants.ENAME_BOOLEAN_EXPRESSION))
                    continue;
                foreach (Node child in AllDescendants(boolExpr))
                    Assert.That(child.TextValue?.ToUpperInvariant(), Is.Not.EqualTo("THROW"),
                        "THROW starts the IF/ELSE body; it must never be part of the boolean condition");
            }
        }

        [Test]
        public void FollowingStatementIsNotCapturedAsIfBody()
        {
            // Before the fix, "SELECT 2" became the IF's single-statement body
            // because the THROW (and its terminating semicolon) vanished into
            // the boolean expression.
            Node parsed = Parse("IF @p IS NULL THROW 51001, 'x', 1;\r\nSELECT 2;");
            int topLevelStatements = 0;
            foreach (Node child in parsed.Children)
                if (child.Name.Equals(SqlStructureConstants.ENAME_SQL_STATEMENT))
                    topLevelStatements++;
            Assert.That(topLevelStatements, Is.EqualTo(2),
                "the IF...THROW and the SELECT are two separate top-level statements");
        }

        [Test, TestCaseSource(nameof(ThrowInputs))]
        public void FormatsCleanlyAndIdempotently(string sql)
        {
            string pass1 = Format(sql, out bool errors1);
            Assert.That(errors1, Is.False, "first pass must not report parse errors");

            string pass2 = Format(pass1, out bool errors2);
            Assert.That(errors2, Is.False, "second pass must not report parse errors");
            Assert.That(pass2, Is.EqualTo(pass1), "formatting must be idempotent");
        }

        [Test, TestCaseSource(nameof(ThrowInputs))]
        public void NoIndentCascade(string sql)
        {
            // Upstream #266's visible symptom: each consecutive IF...THROW line
            // indented one level deeper than the last.
            string pass1 = Format(sql, out _);
            foreach (string line in pass1.Split('\n'))
            {
                string trimmed = line.TrimEnd();
                if (trimmed.TrimStart().StartsWith("IF ", System.StringComparison.OrdinalIgnoreCase))
                    Assert.That(trimmed, Does.Not.StartWith(" ").And.Not.StartWith("\t"),
                        "IF statements at the top level must not accumulate indentation: '" + trimmed + "'");
            }
        }

        [Test, TestCaseSource(nameof(ThrowInputs))]
        public void FormattedOutputParsesUnderScriptDom(string sql)
        {
            Assume.That(ScriptDomOracle.ParsesClean(sql), Is.True,
                "input itself should parse clean under ScriptDom");
            string pass1 = Format(sql, out _);
            Assert.That(ScriptDomOracle.GetParseErrors(pass1), Is.Empty,
                "formatted output must remain valid T-SQL under the ScriptDom oracle");
        }

        private static System.Collections.Generic.IEnumerable<Node> AllDescendants(Node node)
        {
            foreach (Node child in node.Children)
            {
                yield return child;
                foreach (Node grandchild in AllDescendants(child))
                    yield return grandchild;
            }
        }
    }
}
