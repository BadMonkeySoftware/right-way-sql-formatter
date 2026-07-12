/*
Right Way SQL Formatter - modernized T-SQL formatter
Tests for ParseErrorAnalyzer: human-readable parse error descriptions.
Licensed under GNU AGPL v3
*/

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Interfaces;
using PoorMansTSqlFormatterLib.Parsers;
using PoorMansTSqlFormatterLib.ParseStructure;
using PoorMansTSqlFormatterLib.Tokenizers;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class ParseErrorAnalyzerTests
    {
        private readonly TSqlStandardTokenizer _tokenizer = new TSqlStandardTokenizer();
        private readonly TSqlStandardParser _parser = new TSqlStandardParser();

        private IList<string> Analyze(string sql, out bool errorFound)
        {
            ITokenList tokens = _tokenizer.TokenizeSQL(sql);
            Node tree = _parser.ParseSQL(tokens);
            errorFound = tree.GetAttributeValue(SqlStructureConstants.ANAME_ERRORFOUND) == "1";
            return ParseErrorAnalyzer.GetErrorDescriptions(tree, tokens);
        }

        [Test]
        public void ValidSqlProducesNoDescriptions()
        {
            var descriptions = Analyze("SELECT Name FROM Employees WHERE ID = 1", out bool errorFound);
            Assert.That(errorFound, Is.False);
            Assert.That(descriptions, Is.Empty);
        }

        [Test]
        public void UnclosedStringLiteralIsDescribed()
        {
            var descriptions = Analyze("SELECT 'abc", out bool errorFound);
            Assert.That(errorFound, Is.True);
            Assert.That(descriptions.Any(d => d.Contains("Unclosed string literal")), Is.True,
                "got: " + string.Join(" | ", descriptions));
        }

        [Test]
        public void UnclosedBlockCommentIsDescribed()
        {
            var descriptions = Analyze("SELECT 1 /* comment never ends", out bool errorFound);
            Assert.That(errorFound, Is.True);
            Assert.That(descriptions.Any(d => d.Contains("Unclosed block comment")), Is.True,
                "got: " + string.Join(" | ", descriptions));
        }

        [Test]
        public void UnclosedBracketIdentifierIsDescribed()
        {
            var descriptions = Analyze("SELECT [SomeColumn FROM x", out bool errorFound);
            Assert.That(errorFound, Is.True);
            Assert.That(descriptions.Any(d => d.Contains("Unclosed bracket-quoted identifier")), Is.True,
                "got: " + string.Join(" | ", descriptions));
        }

        [Test]
        public void StrayClosingParenIsDescribed()
        {
            var descriptions = Analyze("SELECT 1)", out bool errorFound);
            Assert.That(errorFound, Is.True);
            Assert.That(descriptions.Any(d => d.Contains(")")), Is.True,
                "got: " + string.Join(" | ", descriptions));
        }

        [Test]
        public void UnclosedParenReportsSomething()
        {
            var descriptions = Analyze("SELECT (1 + 2", out bool errorFound);
            Assert.That(errorFound, Is.True);
            Assert.That(descriptions, Is.Not.Empty);
        }

        [Test]
        public void ErrorEncounteredFlagStillReportedByManager()
        {
            var manager = new SqlFormattingManager();
            bool errorsEncountered = false;
            string output = manager.Format("SELECT 1)", ref errorsEncountered, out IList<string> descriptions);
            Assert.That(errorsEncountered, Is.True);
            Assert.That(descriptions, Is.Not.Empty);
            // Default formatter prefix (unchanged behavior) must still lead the output
            Assert.That(output, Does.StartWith("--WARNING! ERRORS ENCOUNTERED DURING SQL PARSING!"));
        }

        [Test]
        public void DescriptionsIncludeSourceLineNumbers()
        {
            var descriptions = Analyze("SELECT 1\nFROM x\nWHERE y = 1)", out bool errorFound);
            Assert.That(errorFound, Is.True);
            Assert.That(descriptions.Any(d => d.Contains("')'") && d.Contains("(line 3)")), Is.True,
                "got: " + string.Join(" | ", descriptions));
        }

        [Test]
        public void UnfinishedTokenIncludesLineNumber()
        {
            var descriptions = Analyze("SELECT 1\n\n\nSELECT 'never closed", out bool errorFound);
            Assert.That(errorFound, Is.True);
            Assert.That(descriptions.Any(d => d.Contains("Unclosed string literal") && d.Contains("(line 4)")), Is.True,
                "got: " + string.Join(" | ", descriptions));
        }

        [Test]
        public void DescriptionsAreSingleLineAndBounded()
        {
            var descriptions = Analyze("SELECT 'multi\nline\nnever-closed string literal that runs on and on and on and on", out bool errorFound);
            Assert.That(errorFound, Is.True);
            foreach (string d in descriptions)
            {
                Assert.That(d, Does.Not.Contain("\n"));
                Assert.That(d, Does.Not.Contain("\r"));
            }
        }
    }
}
