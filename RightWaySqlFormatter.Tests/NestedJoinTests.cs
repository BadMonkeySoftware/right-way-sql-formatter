/*
Nested-join "double ON" support (upstream issues #288, #241, #30).

T-SQL allows nested joins where the ON clauses stack up at the end:

    FROM a LEFT JOIN b INNER JOIN c ON c.k = b.k ON b.k = a.k

The second ON binds the outer (LEFT) join. Historically the parser nested
the second JoinOn section INSIDE the first one's content body, flagged the
statement as an error, and produced non-idempotent output.

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
    public class NestedJoinTests
    {
        private static readonly string[] NestedJoinInputs =
        {
            // upstream #241 - minimal chained double ON
            @"SELECT 1
FROM toto
INNER JOIN titi
LEFT JOIN tata
	ON 2 = 2
		ON 1 = 1",

            // upstream #30 - nested join without parens
            @"SELECT [1].*, [2].*
FROM @TestTable [1]
LEFT JOIN @TestTable [2]
INNER JOIN @TestTable [3] ON [2].TestColumn2 = [3].TestColumn2
      AND 1 < 2
      ON [1].TestColumn1 = [2].TestColumn1",

            // upstream #30 - nested join WITH parens
            @"SELECT [1].*, [2].*
FROM @TestTable [1]
LEFT JOIN (@TestTable [2]
INNER JOIN @TestTable [3] ON [2].TestColumn2 = [3].TestColumn2
      AND 1 < 2)
      ON [1].TestColumn1 = [2].TestColumn1",

            // upstream #288 - INSERT + table hint + nested join
            @"INSERT INTO #tt3 WITH(TABLOCK) (a, b, c) SELECT
T1.x, T3.y, T2.z
FROM dbo.D1 T1
LEFT OUTER JOIN dbo.D2 T2
INNER JOIN dbo.D3 T3
ON (T3.k = T2.k)
ON (T2.k = T1.k)",

            // three-deep chain
            @"SELECT *
FROM a
JOIN b
JOIN c
JOIN d ON d.k = c.k ON c.k = b.k ON b.k = a.k",
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

        [Test, TestCaseSource(nameof(NestedJoinInputs))]
        public void ParsesWithoutError(string sql)
        {
            Node parsed = Parse(sql);
            Assert.That(parsed.GetAttributeValue(SqlStructureConstants.ANAME_ERRORFOUND), Is.Null,
                "nested-join SQL is valid T-SQL and must not be flagged as a parse error");
        }

        [Test, TestCaseSource(nameof(NestedJoinInputs))]
        public void OnSectionsAreSiblingsNotNested(string sql)
        {
            Node parsed = Parse(sql);
            foreach (Node joinOn in AllDescendants(parsed))
            {
                if (!joinOn.Name.Equals(SqlStructureConstants.ENAME_JOIN_ON_SECTION))
                    continue;
                Node? ancestor = joinOn.Parent;
                while (ancestor != null)
                {
                    Assert.That(ancestor.Name, Is.Not.EqualTo(SqlStructureConstants.ENAME_JOIN_ON_SECTION),
                        "a JoinOn section must never be nested inside another JoinOn section");
                    ancestor = ancestor.Parent;
                }
            }
        }

        [Test, TestCaseSource(nameof(NestedJoinInputs))]
        public void FormatsCleanlyAndIdempotently(string sql)
        {
            string pass1 = Format(sql, out bool errors1);
            Assert.That(errors1, Is.False, "first pass must not report parse errors");

            string pass2 = Format(pass1, out bool errors2);
            Assert.That(errors2, Is.False, "second pass must not report parse errors");
            Assert.That(pass2, Is.EqualTo(pass1), "formatting must be idempotent");
        }

        [Test, TestCaseSource(nameof(NestedJoinInputs))]
        public void FormattedOutputKeepsAllOnConditions(string sql)
        {
            string pass1 = Format(sql, out _);
            int inputOnCount = System.Text.RegularExpressions.Regex.Matches(
                sql, @"\bON\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
            int outputOnCount = System.Text.RegularExpressions.Regex.Matches(
                pass1, @"\bON\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
            Assert.That(outputOnCount, Is.EqualTo(inputOnCount),
                "every ON condition in the input must survive formatting");
        }

        [Test, TestCaseSource(nameof(NestedJoinInputs))]
        public void FormattedOutputParsesUnderScriptDom(string sql)
        {
            // These inputs are all valid T-SQL; formatted output must be too.
            Assume.That(ScriptDomOracle.ParsesClean(sql), Is.True,
                "input itself should parse clean under ScriptDom");
            string pass1 = Format(sql, out _);
            var errors = ScriptDomOracle.GetParseErrors(pass1);
            Assert.That(errors, Is.Empty,
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
