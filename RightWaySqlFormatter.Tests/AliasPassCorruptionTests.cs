/*
Right Way SQL Formatter - modernized T-SQL formatter
Regression tests for the align/alias text post-passes corrupting wrapped /
AS-less / commented SELECT columns into invalid SQL. Each case parsed clean as
input and, before the fix, produced output that failed the real T-SQL grammar
(the CorpusOracle bug family, 188 corpus failures -> 0). Every assertion here
formats a minimal snippet and checks the output still parses under ScriptDom -
the same invariant CorpusOracle enforces corpus-wide.
Licensed under GNU AGPL v3.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class AliasPassCorruptionTests
    {
        // AlignEquals + HeavyEditor exercise EnsureColumnAliases, RewriteAliasesToEqualSign,
        // and the SELECT-column align passes - where all these corruptions lived.
        private const string AlignEquals =
            "AlignColumnDefinitions=True,ColumnAliasStyle=EqualSign,ColumnAlwaysHasAlias=True";
        private const string Heavy =
            "ExpandBetweenConditions=false,ExpandBooleanExpressions=false,ExpandCaseStatements=false,"
            + "ExpandInLists=false,UppercaseKeywords=false,TrailingCommas=True,AlignTableJoins=True,"
            + "ColumnAlwaysHasAlias=True,SelectFirstColumnOnNewLine=True,AlignColumnDefinitions=True,"
            + "AlignColumnDefinitionsInDDL=True,ColumnAliasStyle=EqualSign,IndentWhereAndOrConditions=True,"
            + "MaxLineWidth=200,CompactRaiserror=True,CompactSingleStatementBlocks=True";

        private static void AssertFormatsToValidSql(string sql, string config)
        {
            Assume.That(ScriptDomOracle.ParsesClean(sql), "test input must itself parse clean");
            var mgr = new SqlFormattingManager(new TSqlStandardFormatter(new TSqlStandardFormatterOptions(config)));
            bool err = false;
            string pass1 = mgr.Format(sql, ref err);
            Assert.That(ScriptDomOracle.GetParseErrors(pass1), Is.Empty,
                "formatted output must parse under the real T-SQL grammar:\n" + pass1);
            // re-format must also stay valid (guards the align passes' idempotency corruptions)
            string pass2 = mgr.Format(pass1, ref err);
            Assert.That(ScriptDomOracle.GetParseErrors(pass2), Is.Empty,
                "re-formatted output must parse:\n" + pass2);
        }

        [Test]
        public void UnclosedBracketColumn_IsIdempotent_NoAliasStacking()
        {
            // Deliberately malformed: the `[A` bracket never closes on its own line (it pairs
            // with the ']' of a later [X]). EnsureColumnAliases could not parse the alias
            // structure, so it re-aliased the column every pass: `[A = 1` -> `[A = 1 = [A = 1`
            // -> ... (unbounded, non-convergent - the tsqlt ParsingDisaster corpus file).
            // Malformed input is not required to be VALID, but it must be STABLE.
            var mgr = new SqlFormattingManager(new TSqlStandardFormatter(new TSqlStandardFormatterOptions(Heavy)));
            bool err = false;
            string p1 = mgr.Format("SELECT 1 AS [A\r\nGO\r\nSELECT 0 AS [X]\r\n", ref err);
            string p2 = mgr.Format(p1, ref err);
            string p3 = mgr.Format(p2, ref err);
            Assert.That(p1, Does.Contain("[A = 1"), "the malformed column renders once");
            Assert.That(p2, Is.EqualTo(p1), "an unclosed-bracket column must not stack a new alias each pass");
            Assert.That(p3, Is.EqualTo(p2), "fixed point by pass 3");
        }

        [Test]
        public void AsLessPlainAlias_NotReAliased()
        {
            // 'B' Id / count(1) Cnt / CASE...END AvailabilityGroup are already aliased (AS-less).
            AssertFormatsToValidSql(
                "SELECT 'B' Id, count(1) Cnt, "
                + "CASE WHEN LEFT(x,1)='[' THEN PARSENAME(x,1) ELSE x END AvailabilityGroup FROM t;",
                AlignEquals);
        }

        [Test]
        public void AsLessStringLiteralAlias_NotReAliased()
        {
            // legacy string-literal alias: expr N'alias'
            AssertFormatsToValidSql(
                "SELECT N'Database ' + CONVERT(NVARCHAR(16), GETDATE(), 121) "
                + "N'sp_BlitzIndex(TM) v2.02 - Jan 30, 2014' FROM t;",
                AlignEquals);
        }

        [Test]
        public void GluedEmptyStringAlias_NotReAliased()
        {
            AssertFormatsToValidSql("SELECT ''r FROM t;", AlignEquals);
        }

        [Test]
        public void WrappedAliasRhs_NotAliasedAsColumn()
        {
            // FieldList = '<long string that wraps past width 200>' - the wrapped RHS on the
            // next line must not become its own ColumnAlias_N column.
            AssertFormatsToValidSql(
                "SELECT FieldList = '" + new string('x', 260) + "', b = 1 FROM t;",
                Heavy);
        }

        [Test]
        public void WrappedConcatWithTrailingAsAlias_NotDoubleAliased()
        {
            // A concat that wraps and ends with "AS Details" on a later line.
            AssertFormatsToValidSql(
                "SELECT 'The user ' + a + ' has run it ' + CAST(b AS NVARCHAR(100)) "
                + "+ ' between ' + c + ' and ' + d + '. " + new string('z', 120) + "' AS Details FROM t;",
                Heavy);
        }

        [Test]
        public void WrappedCaseContinuation_AlignPassLeavesItAlone()
        {
            // CASE that wraps across lines: "case when left(...)='[' and right(...)=']' then..."
            AssertFormatsToValidSql(
                "SELECT CASE WHEN LEFT(DatabaseItem,1) = '[' AND RIGHT(DatabaseItem,1) = ']' "
                + "THEN PARSENAME(DatabaseItem,1) ELSE DatabaseItem END AS DatabaseItem, "
                + "b = 1 FROM t;",
                Heavy);
        }

        [Test]
        public void KeywordNamedDerivedAlias_IsBracketed()
        {
            // t.HASH -> alias HASH is a reserved keyword and must be bracketed ([HASH] = t.HASH).
            AssertFormatsToValidSql("SELECT t.HASH, t.TEXT, t.[VALUE] FROM t;", AlignEquals);
        }

        [Test]
        public void VariableColumn_AliasedToColumnAlias_NotItself()
        {
            // SELECT @v in a derived table needs an alias, but not "@v = @v".
            AssertFormatsToValidSql(
                "SELECT x FROM (SELECT @FileExtensionDiff UNION ALL SELECT @FileExtensionLog) q(x);",
                AlignEquals);
        }

        [Test]
        public void CompoundAssignmentOperator_NotSplit()
        {
            // @v += (...) must not become "@v + = (...)".
            AssertFormatsToValidSql(
                "DECLARE @v NVARCHAR(MAX); SELECT @v += (SELECT TOP (1) name FROM sys.objects);",
                AlignEquals);
        }

        [Test]
        public void EscapedBracketAlias_Recognized()
        {
            // [Definition: [Property]] ...] contains an escaped ]] and is a single alias.
            AssertFormatsToValidSql(
                "SELECT [Definition: [Property]] x] = index_definition, b = 1 FROM t;",
                Heavy);
        }

        [Test]
        public void MemberAccessSplit_NotAliasedAsColumn()
        {
            // A.B split as "... + A." then "B + ..." across a width wrap.
            AssertFormatsToValidSql(
                "SELECT '" + new string('p', 150) + "' + AL.EscapedAnnotationString + ';' AS Cmd, b = 1 FROM AnnotationList AL;",
                Heavy);
        }

        [Test]
        public void FunctionNameParenSplit_NotAliasedAsColumn()
        {
            // quotename ( x ) split as "... + quotename" then "(x) + ...".
            AssertFormatsToValidSql(
                "SELECT '" + new string('q', 150) + "' + QUOTENAME(a) + '.' + QUOTENAME(b) + '.' + QUOTENAME(c) AS Cmd, d = 1 FROM t;",
                Heavy);
        }

        [Test]
        public void TrailingBlockCommentAfterAliasComma_RewrittenCorrectly()
        {
            // "wNow.wait_type AS Finding, /* note */" must become "Finding = wNow.wait_type, /* note */".
            AssertFormatsToValidSql(
                "SELECT wNow.wait_type AS Finding, /* IF YOU CHANGE THIS, checks 11, 12 break */ "
                + "wNow.wait_time_ms AS Ms FROM w wNow;",
                Heavy);
        }

        [Test]
        public void DdlCreateUserFromLogin_NotGivenTableAlias()
        {
            // CREATE USER x FROM LOGIN y - the FROM is a DDL source, not a table source.
            AssertFormatsToValidSql("CREATE USER [tSQLt.Build] FROM LOGIN [tSQLt.Build];", Heavy);
        }
    }
}
