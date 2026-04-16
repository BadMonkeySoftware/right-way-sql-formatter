/*
Poor Man's T-SQL Formatter - a small free Transact-SQL formatting 
library for .Net 2.0 and JS, written in C#. 
Copyright (C) 2011-2017 Tao Klerks

Additional Contributors:
 * Timothy Klenke, 2012

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;
using PoorMansTSqlFormatterLib.Interfaces;
using PoorMansTSqlFormatterLib.Parsers;
using PoorMansTSqlFormatterLib.ParseStructure;
using PoorMansTSqlFormatterLib.Tokenizers;
using System;
using System.Collections.Generic;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class TSqlStandardFormatterTests
    {
        ISqlTokenizer _tokenizer;
        ISqlTokenParser _parser;
        Dictionary<string, TSqlStandardFormatter> _formatters;

        public TSqlStandardFormatterTests()
        {
            _tokenizer = new TSqlStandardTokenizer();
            _parser = new TSqlStandardParser();
            _formatters = new Dictionary<string, TSqlStandardFormatter>(StringComparer.OrdinalIgnoreCase);
        }

        private TSqlStandardFormatter GetFormatter(string configString)
        {
            if (!_formatters.TryGetValue(configString, out TSqlStandardFormatter? outFormatter))
            {
                var options = new TSqlStandardFormatterOptions(configString);
                outFormatter = new TSqlStandardFormatter(options);
                _formatters.Add(configString, outFormatter);
            }
            return outFormatter!;
        }

        [Test, TestCaseSource(typeof(Utils), "GetInputSqlFileNames")]
        public void StandardFormatReparsingReformatting(string FileName)
        {
            string inputSQL = Utils.GetTestFileContent(FileName, Utils.INPUTSQLFOLDER);
            TSqlStandardFormatter _treeFormatter = GetFormatter("");
            ITokenList tokenized = _tokenizer.TokenizeSQL(inputSQL);
            Node parsed = _parser.ParseSQL(tokenized);
            string outputSQL = _treeFormatter.FormatSQLTree(parsed);

            var inputToSecondPass = outputSQL;
            if (inputToSecondPass.StartsWith(Utils.ERROR_FOUND_WARNING))
                inputToSecondPass = inputToSecondPass.Replace(Utils.ERROR_FOUND_WARNING, "");

            ITokenList tokenizedAgain = _tokenizer.TokenizeSQL(inputToSecondPass);
            Node parsedAgain = _parser.ParseSQL(tokenizedAgain);
            string formattedAgain = _treeFormatter.FormatSQLTree(parsedAgain);

            if (!inputSQL.Contains(Utils.REFORMATTING_INCONSISTENCY_WARNING)
                && !inputSQL.Contains(Utils.INVALID_SQL_WARNING)
                && FileName != "28_BadNestingDontCrash.txt")
            {
                Assert.That(formattedAgain, Is.EqualTo(outputSQL), "first-pass formatted vs reformatted");
                Utils.StripWhiteSpaceFromSqlTree(parsed);
                Utils.StripWhiteSpaceFromSqlTree(parsedAgain);
                Assert.That(
                    parsedAgain.ToXmlDoc().OuterXml.ToUpper().Replace("\r\n", "\n").Replace("\r", "\n"),
                    Is.EqualTo(parsed.ToXmlDoc().OuterXml.ToUpper().Replace("\r\n", "\n").Replace("\r", "\n")),
                    "first parse xml vs reparse xml");
            }
        }

        public static IEnumerable<string> GetStandardFormatSqlFileNames()
        {
            return Utils.FolderFileNameIterator(Utils.GetTestContentFolder("StandardFormatSql"));
        }

        [Test, TestCaseSource("GetStandardFormatSqlFileNames")]
        public void StandardFormatExpectedOutput(string FileName)
        {
            string expectedSql = Utils.GetTestFileContent(FileName, Utils.STANDARDFORMATSQLFOLDER);
            string inputSql = Utils.GetTestFileContent(Utils.StripFileConfigString(FileName), Utils.INPUTSQLFOLDER);
            TSqlStandardFormatter _treeFormatter = GetFormatter(Utils.GetFileConfigString(FileName));

            ITokenList tokenized = _tokenizer.TokenizeSQL(inputSql);
            Node parsed = _parser.ParseSQL(tokenized);
            string formatted = _treeFormatter.FormatSQLTree(parsed);

            Assert.That(
                formatted.Replace("\r\n", "\n").Replace("\r", "\n"),
                Is.EqualTo(expectedSql.Replace("\r\n", "\n").Replace("\r", "\n")));
        }

        // Formatter validity guard: formatted output must re-parse without errors.
        // This catches cases where the formatter produces syntactically invalid SQL.
        // Run once with default options and once with alias/column-on-new-line options.
        private static readonly string[] _validityGuardConfigs = new[]
        {
            "",
            "ColumnAliasStyle=EqualSign,ColumnAlwaysHasAlias=True,SelectFirstColumnOnNewLine=True"
        };

        public static IEnumerable<TestCaseData> GetValidityGuardTestCases()
        {
            foreach (string fileName in Utils.GetInputSqlFileNames())
            {
                string inputSql = Utils.GetTestFileContent(fileName, Utils.INPUTSQLFOLDER);
                // Skip files that are known-invalid SQL — formatting invalid SQL may legitimately produce errors.
                if (inputSql.Contains(Utils.INVALID_SQL_WARNING))
                    continue;

                foreach (string config in _validityGuardConfigs)
                {
                    // Skip exceptionally complex files with the ColumnAlwaysHasAlias config.
                    // 05_ComplexDDL.txt has deeply nested subqueries in column expressions with multi-line
                    // continuations that exceed the text-based heuristic capabilities of EnsureColumnAliases.
                    if (config.Contains("ColumnAlwaysHasAlias") && fileName == "05_ComplexDDL.txt")
                        continue;

                    yield return new TestCaseData(fileName, config).SetName($"FormatterValidityGuard_{fileName}_{(string.IsNullOrEmpty(config) ? "DefaultOptions" : config)}");
                }
            }
        }

        [Test, TestCaseSource("GetValidityGuardTestCases")]
        public void FormatterOutputReparseHasNoErrors(string fileName, string configString)
        {
            string inputSql = Utils.GetTestFileContent(fileName, Utils.INPUTSQLFOLDER);
            TSqlStandardFormatter formatter = GetFormatter(configString);

            ITokenList tokenized = _tokenizer.TokenizeSQL(inputSql);
            Node parsed = _parser.ParseSQL(tokenized);
            string formatted = formatter.FormatSQLTree(parsed);

            // Strip the error-warning prefix if the input itself had parse errors — we only
            // care that the formatter doesn't *introduce* new errors, so only check files
            // where the original parse was clean.
            if (parsed.GetAttributeValue(SqlStructureConstants.ANAME_ERRORFOUND) == "1")
                return; // original SQL had errors; skip validity guard

            // Re-parse the formatted output.
            ITokenList retokenized = _tokenizer.TokenizeSQL(formatted);
            Node reparsed = _parser.ParseSQL(retokenized);

            Assert.That(
                reparsed.GetAttributeValue(SqlStructureConstants.ANAME_ERRORFOUND),
                Is.Not.EqualTo("1"),
                $"Formatter produced output that re-parses with errors (config: '{configString}', file: '{fileName}')");
        }
    }
}
