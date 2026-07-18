/*
AlignColumnDefinitionsInDDL scope (corpus finding, 2026-07-17).

The parser reuses the DDLParens element for INSERT column lists, VALUES tuples,
and OPTION(...) hints. The DDL column-alignment pass used to rewrite ALL of
them as if they were "name type" column definitions, inserting its padding
INSIDE string literals (N'great match' -> N'great  match', growing on every
re-format) and inside function calls (CONVERT(vector(4,   float32), ...)) —
semantic corruption, and the main driver of heavy-profile non-idempotency on
the real-world corpus.

The pass must only rewrite parens whose parent is a real DDL block
(CREATE/ALTER object bodies, DECLARE @t TABLE); everything else sharing the
element name is arbitrary expression content and stays untouched.

Licensed under the GNU Affero General Public License v3 - see LICENSE.txt.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class DdlAlignScopeTests
    {
        private static string Format(string sql, bool trailingCommas = false)
        {
            var formatter = new TSqlStandardFormatter(new TSqlStandardFormatterOptions
            {
                AlignColumnDefinitionsInDDL = true,
                TrailingCommas = trailingCommas
            });
            var manager = new SqlFormattingManager(formatter);
            bool error = false;
            string result = manager.Format(sql, ref error);
            Assert.That(error, Is.False, "input should format without parse errors");
            return result;
        }

        [Test]
        public void InsertValuesStringLiteralsAreNeverPadded([Values(false, true)] bool trailingCommas)
        {
            string sql = "insert dbo.t (a, b, c) values (N'great match', N'good', convert(vector(4, float32), N'[0.5]'));";
            string output = Format(sql, trailingCommas);

            Assert.That(output, Does.Contain("N'great match'"),
                "string literal content must survive formatting untouched");
            Assert.That(output, Does.Contain("vector(4, float32)"),
                "function-call arguments must not be align-padded");
        }

        [Test]
        public void InsertValuesFormattingIsIdempotent([Values(false, true)] bool trailingCommas)
        {
            string sql = "insert dbo.t (a, b, c) values (N'great match', N'good', convert(vector(4, float32), N'[0.5]'));";
            string pass1 = Format(sql, trailingCommas);
            string pass2 = Format(pass1, trailingCommas);
            Assert.That(pass2, Is.EqualTo(pass1), "re-formatting formatted output must be a no-op");
        }

        [Test]
        public void CreateTableColumnDefinitionsAreStillAligned()
        {
            string sql = "create table dbo.t (Id int not null, LongColumnName nvarchar(50) null);";
            string output = Format(sql);

            // Alignment pads between column name and type so the types line up:
            // the short name gains more padding than a single space.
            Assert.That(output, Does.Contain("Id             INT"),
                "genuine DDL column definitions must still be aligned");
            Assert.That(output, Does.Contain("LongColumnName NVARCHAR(50)"));
        }

        [Test]
        public void DeclareTableColumnDefinitionsAreStillAligned()
        {
            string sql = "declare @t table (Id int not null, LongColumnName nvarchar(50) null);";
            string output = Format(sql);
            Assert.That(output, Does.Contain("Id             INT"),
                "DECLARE @t TABLE definitions must still be aligned");
        }
    }
}
