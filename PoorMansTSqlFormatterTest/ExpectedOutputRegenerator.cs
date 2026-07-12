/*
Right Way SQL Formatter - modernized T-SQL formatter
Expected-output regeneration tool (see AGENTS.md).
Licensed under GNU AGPL v3
*/

using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using PoorMansTSqlFormatterLib.Formatters;
using PoorMansTSqlFormatterLib.Parsers;
using PoorMansTSqlFormatterLib.Tokenizers;

namespace PoorMansTSqlFormatterTests
{
    /// <summary>
    /// Regenerates expected StandardFormatSql files from InputSql using the formatter
    /// LIBRARY directly — never the CLI (CLI defaults differ from the test harness) and
    /// never by hand (per AGENTS.md).
    ///
    /// Marked [Explicit]: never runs as part of a normal `dotnet test`.
    /// Usage (from repo root):
    ///   REGEN_FILES="39_Foo.sql;39_Foo__AlignCols.sql" \
    ///     dotnet test RightWaySqlFormatter.NoSSMS.slnx --filter "Name~RegenerateExpectedFiles"
    ///
    /// Each entry is an expected-output filename; its __Slug1_Slug2 suffix selects the
    /// formatter options (slugs defined in Utils.CONFIG_SLUGS), exactly as the test
    /// harness reads it. Files are written to the SOURCE Data folder (not the bin copy).
    /// </summary>
    [TestFixture]
    public class ExpectedOutputRegenerator
    {
        [Test, Explicit("Regenerates expected-output files in the source tree; run only deliberately via REGEN_FILES")]
        public void RegenerateExpectedFiles()
        {
            string? regenList = Environment.GetEnvironmentVariable("REGEN_FILES");
            if (string.IsNullOrWhiteSpace(regenList))
                Assert.Fail("Set the REGEN_FILES environment variable to a semicolon-separated list of StandardFormatSql filenames to regenerate.");

            // bin/<config>/<tfm>/ -> project dir -> source Data folder
            string sourceDataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data"));
            Assert.That(Directory.Exists(Path.Combine(sourceDataDir, "InputSql")), Is.True,
                "Could not locate source Data folder at: " + sourceDataDir);

            var tokenizer = new TSqlStandardTokenizer();
            var parser = new TSqlStandardParser();

            foreach (string rawName in regenList!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string fileName = rawName;
                string configString = Utils.GetFileConfigString(fileName);
                string inputName = Utils.StripFileConfigString(fileName);

                string inputPath = Path.Combine(sourceDataDir, "InputSql", inputName);
                Assert.That(File.Exists(inputPath), Is.True, "Input file not found: " + inputPath);

                string inputSql = File.ReadAllText(inputPath);
                var formatter = new TSqlStandardFormatter(new TSqlStandardFormatterOptions(configString));
                string formatted = formatter.FormatSQLTree(parser.ParseSQL(tokenizer.TokenizeSQL(inputSql)));

                string outputPath = Path.Combine(sourceDataDir, "StandardFormatSql", fileName);
                File.WriteAllText(outputPath, formatted, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                TestContext.Out.WriteLine("Regenerated: " + outputPath);
            }
        }
    }
}
