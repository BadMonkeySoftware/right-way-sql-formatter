/*
Right Way SQL Formatter - modernized T-SQL formatter
Formatter-output validity tests backed by the ScriptDom oracle:
if the input parses clean under Microsoft's T-SQL grammar, the formatted
output - and the output of re-formatting that output - must parse clean too.
Licensed under GNU AGPL v3.
*/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class ScriptDomValidationTests
    {
        // Always-run matrix: NO profile here may ever produce output that fails the
        // real T-SQL grammar. AlignEquals and HeavyEditor started life as [Explicit]
        // "hunt" profiles with known gaps; all 17 remaining failures were fixed
        // 2026-07-17 (align/alias text-pass corruption of non-table-source FROMs,
        // TVF calls, derived tables, table variables, CASE-arm continuations, and
        // string-literal '=' aliases), so they are now enforced on every run.
        private static readonly (string name, string config)[] PROFILES =
        {
            ("Default", ""),
            ("AlignEquals", "AlignColumnDefinitions=True,ColumnAliasStyle=EqualSign,ColumnAlwaysHasAlias=True"),
            ("HeavyEditor", "ExpandBetweenConditions=false,ExpandBooleanExpressions=false,ExpandCaseStatements=false,"
                + "ExpandInLists=false,UppercaseKeywords=false,TrailingCommas=True,AlignTableJoins=True,"
                + "ColumnAlwaysHasAlias=True,SelectFirstColumnOnNewLine=True,AlignColumnDefinitions=True,"
                + "AlignColumnDefinitionsInDDL=True,ColumnAliasStyle=EqualSign,IndentWhereAndOrConditions=True,"
                + "MaxLineWidth=200,CompactRaiserror=True,CompactSingleStatementBlocks=True"),
        };

        public static IEnumerable<TestCaseData> GetValidationCases()
        {
            foreach (string fileName in Utils.GetInputSqlFileNames())
                foreach (var (name, config) in PROFILES)
                    yield return new TestCaseData(fileName, config)
                        .SetName($"ScriptDomValid_{name}_{fileName}");
        }

        [Test, TestCaseSource(nameof(GetValidationCases))]
        public void FormattedOutputParsesCleanUnderScriptDom(string fileName, string configString)
        {
            string inputSql = Utils.GetTestFileContent(fileName, Utils.INPUTSQLFOLDER);

            // Only judge files the real grammar accepts (excludes the intentionally
            // invalid files and other-dialect samples).
            if (!ScriptDomOracle.ParsesClean(inputSql))
                Assert.Ignore("input does not parse clean under ScriptDom - cannot judge output");

            var manager = new SqlFormattingManager(
                new TSqlStandardFormatter(new TSqlStandardFormatterOptions(configString)));
            bool errors = false;

            string pass1 = manager.Format(inputSql, ref errors);
            var pass1Errors = ScriptDomOracle.GetParseErrors(pass1);
            Assert.That(pass1Errors, Is.Empty,
                "formatted output no longer parses under the real T-SQL grammar:\n  "
                + string.Join("\n  ", pass1Errors.Take(5)));

            string pass2 = manager.Format(pass1, ref errors);
            var pass2Errors = ScriptDomOracle.GetParseErrors(pass2);
            Assert.That(pass2Errors, Is.Empty,
                "RE-formatted output no longer parses under the real T-SQL grammar:\n  "
                + string.Join("\n  ", pass2Errors.Take(5)));
        }

        /// <summary>
        /// Corpus-wide oracle run - only when the real-world corpus is present
        /// (realworld-results/corpus, machine-local). Explicit: run deliberately via
        ///   dotnet test --filter "Name~CorpusOracle"
        /// </summary>
        [Test, Explicit("long-running; requires realworld-results/corpus checkout")]
        public void CorpusOracle()
        {
            string corpusDir = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "realworld-results", "corpus"));
            if (!Directory.Exists(corpusDir))
                Assert.Ignore("corpus not present at " + corpusDir);

            var failures = new List<string>();
            int judged = 0;
            foreach (string file in Directory.EnumerateFiles(corpusDir, "*.sql", SearchOption.AllDirectories))
            {
                string sql = File.ReadAllText(file);
                if (!ScriptDomOracle.ParsesClean(sql))
                    continue; //not judgeable
                judged++;

                foreach (var (name, config) in PROFILES)
                {
                    var manager = new SqlFormattingManager(
                        new TSqlStandardFormatter(new TSqlStandardFormatterOptions(config)));
                    bool err = false;
                    string pass1 = manager.Format(sql, ref err);
                    var e1 = ScriptDomOracle.GetParseErrors(pass1);
                    if (e1.Count > 0)
                    {
                        failures.Add($"[{name}] PASS1 {Path.GetFileName(file)}: {e1[0]}");
                        continue;
                    }
                    string pass2 = manager.Format(pass1, ref err);
                    var e2 = ScriptDomOracle.GetParseErrors(pass2);
                    if (e2.Count > 0)
                        failures.Add($"[{name}] PASS2 {Path.GetFileName(file)}: {e2[0]}");
                }
            }

            TestContext.Out.WriteLine($"judged {judged} corpus files");
            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(25)));
        }
    }
}
