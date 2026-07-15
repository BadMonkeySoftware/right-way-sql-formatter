/*
Cosmetic spacing/structure fixes for upstream issues #240, #151 and #99.

#240 - a space was injected between a word and a directly adjacent
       bracket-quoted name: "table_[some_id]" -> "table_ [some_id]".
       PL/SQL-style templating relies on the adjacency.

#151 - "ALTER TABLE x ALTER COLUMN ..." was split into two statements,
       inserting a blank line mid-statement. ALTER COLUMN is a clause of
       ALTER TABLE, never a statement; it renders inline like ADD.

#99  - a multi-line block comment that started at column 0 in the source
       got its FIRST line indented while the remaining lines kept their
       original alignment, breaking box-art comment banners.

Licensed under the GNU Affero General Public License v3 - see LICENSE.txt.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    public abstract class CosmeticSpacingTestBase
    {
        protected static string Format(string sql, out bool errorsEncountered)
        {
            var manager = new SqlFormattingManager(new TSqlStandardFormatter());
            bool error = false;
            string result = manager.Format(sql, ref error);
            errorsEncountered = error;
            return result;
        }

        protected static void AssertIdempotent(string input)
        {
            string pass1 = Format(input, out _);
            string pass2 = Format(pass1, out _);
            Assert.That(pass2, Is.EqualTo(pass1), "formatting must be idempotent");
        }
    }

    [TestFixture]
    public class BracketAdjacencyTests : CosmeticSpacingTestBase // upstream #240
    {
        [TestCase("select * from table_[some_id]", "table_[some_id]")]
        [TestCase("select * from table_[some_id]_suffix", "table_[some_id]_suffix")]
        [TestCase("select col_[id] from t", "col_[id]")]
        [TestCase("select * from [a]_[b]", "[a]_[b]")]
        public void AdjacentBracketsStayGlued(string input, string mustContain)
        {
            string output = Format(input, out bool errors);
            Assert.That(errors, Is.False);
            Assert.That(output, Does.Contain(mustContain),
                "source adjacency between words and bracket names must be preserved");
            AssertIdempotent(input);
        }

        [Test]
        public void KeywordAndCommaSpacingUnchanged()
        {
            Assert.That(Format("select[a],[b]from[c]", out _), Does.Contain("SELECT [a]"),
                "keywords still get a space before a bracket name");
            Assert.That(Format("select[a],[b]from[c]", out _), Does.Contain(",[b]"),
                "comma spacing (leading-comma style glues bracket to comma) is decided by comma rules, not this fix");
            Assert.That(Format("select x from dbo.t where [a] = 1", out _), Does.Contain("WHERE [a]"));
        }

        [Test]
        public void SourceWhitespaceIsKept()
        {
            Assert.That(Format("select * from mytable [alias]", out _), Does.Contain("mytable [alias]"),
                "a bracket alias separated by whitespace in the source stays separated");
        }
    }

    [TestFixture]
    public class AlterTableAlterColumnTests : CosmeticSpacingTestBase // upstream #151
    {
        [Test]
        public void SingleStatementNoBlankLine()
        {
            string output = Format("alter table dbo.TableName alter column ColumnName decimal(24, 2) not null", out bool errors);
            Assert.That(errors, Is.False);
            Assert.That(output.Trim(), Is.EqualTo("ALTER TABLE dbo.TableName ALTER COLUMN ColumnName DECIMAL(24, 2) NOT NULL"),
                "ALTER COLUMN is a clause of ALTER TABLE and renders inline, like ADD");
        }

        [Test]
        public void MultiLineInputAlsoJoins()
        {
            string output = Format("alter table dbo.T\r\nalter column C int not null", out _);
            Assert.That(output, Does.Not.Contain("\n\n"),
                "no blank line may be inserted inside the ALTER TABLE statement");
            AssertIdempotent("alter table dbo.T\r\nalter column C int not null");
        }

        [Test]
        public void SeparateAlterStatementsStillSeparate()
        {
            string output = Format("alter table a alter column x int;\r\nalter table b alter column y int;", out bool errors);
            Assert.That(errors, Is.False);
            int count = System.Text.RegularExpressions.Regex.Matches(output, "ALTER TABLE").Count;
            Assert.That(count, Is.EqualTo(2));
            Assert.That(output, Does.Contain("\n"), "two ALTER TABLE statements stay separate");
        }

        [Test]
        public void AlterTableAddIsUnchanged()
        {
            Assert.That(Format("alter table dbo.T add C int not null", out _).Trim(),
                Is.EqualTo("ALTER TABLE dbo.T ADD C INT NOT NULL"));
        }
    }

    [TestFixture]
    public class BlockCommentIndentTests : CosmeticSpacingTestBase // upstream #99
    {
        private const string ProcWithBanner = @"CREATE PROCEDURE dbo.test
AS
BEGIN
/**********************************************************
* This is a comment
*********************************************************/
    SELECT 1;
END";

        [Test]
        public void ColumnZeroBannerCommentKeepsColumnZero()
        {
            string output = Format(ProcWithBanner, out bool errors);
            Assert.That(errors, Is.False);
            foreach (string line in output.Replace("\r", "").Split('\n'))
            {
                if (line.Contains("/*****"))
                    Assert.That(line, Does.StartWith("/*"),
                        "the first line of a column-0 multi-line comment must stay at column 0");
            }
            AssertIdempotent(ProcWithBanner);
        }

        [Test]
        public void IndentedSourceCommentStillGetsIndent()
        {
            // A multi-line comment the user indented in the source keeps the
            // formatter's normal indenting behavior for its first line.
            string input = "BEGIN\r\n    /* first\r\n       second */\r\n    SELECT 1;\r\nEND";
            string output = Format(input, out _);
            Assert.That(output, Does.Contain("    /* first"),
                "comments not at column 0 in the source are indented as before");
        }

        [Test]
        public void SingleLineBlockCommentBehaviorUnchanged()
        {
            string input = "BEGIN\r\n/* one-liner */\r\nSELECT 1;\r\nEND";
            string output = Format(input, out _);
            Assert.That(output, Does.Contain("    /* one-liner */"),
                "single-line block comments keep the normal indent (no alignment to break)");
        }
    }
}
