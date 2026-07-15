/*
--[noformat]/--[/noformat] regions inside statements (upstream #215, #292).

Two long-standing bugs when a noformat region sits INSIDE a statement:

1. Hoisting: the parser's trailing-comment migration pulled the marker
   comments (and everything between them) out of their container - e.g.
   out of an INSERT column list's parens - relocating the protected block.

2. Blank-line accumulation: the closing marker's comment-separation logic
   added a line break even though the verbatim region content already
   ended with one, so every format pass grew the region by one blank line
   (non-idempotent, and each pass corrupts the protected content further).

Licensed under the GNU Affero General Public License v3 - see LICENSE.txt.
*/

using NUnit.Framework;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterTests
{
    [TestFixture]
    public class NoFormatRegionTests
    {
        private static string Format(string sql, out bool errorsEncountered)
        {
            var manager = new SqlFormattingManager(new TSqlStandardFormatter());
            bool error = false;
            string result = manager.Format(sql, ref error);
            errorsEncountered = error;
            return result;
        }

        private const string InsertWithRegionInParens = "INSERT INTO @test (\r\nID,\r\nName\r\n--[noformat]\r\n-- keep   this\r\n--[/noformat]\r\n)\r\nSELECT 1\r\n";

        [Test]
        public void RegionInsideParensIsNotHoisted()
        {
            string output = Format(InsertWithRegionInParens, out bool errors);
            Assert.That(errors, Is.False);
            int closeParen = output.IndexOf(')');
            int regionStart = output.IndexOf("--[noformat]", System.StringComparison.OrdinalIgnoreCase);
            Assert.That(regionStart, Is.GreaterThanOrEqualTo(0), "region markers must survive");
            Assert.That(regionStart, Is.LessThan(closeParen),
                "the noformat block must stay inside the column-list parens, before the ')'");
        }

        [Test]
        public void RegionContentIsVerbatim()
        {
            string output = Format(InsertWithRegionInParens, out _);
            Assert.That(output, Does.Contain("-- keep   this"),
                "internal spacing of protected comments must survive untouched");
        }

        [TestCase(InsertWithRegionInParens)]
        // top-level region (upstream #292's stable case - locked in)
        [TestCase("SELECT 1\r\n--[noformat]\r\nselect    2,    3\r\n--[/noformat]\r\nSELECT 4\r\n")]
        // region in the middle of a select list
        [TestCase("SELECT a,\r\n--[noformat]\r\n   b   ,   c,\r\n--[/noformat]\r\nd\r\nFROM t\r\n")]
        public void RegionsAreIdempotent(string input)
        {
            string pass1 = Format(input, out _);
            string pass2 = Format(pass1, out _);
            string pass3 = Format(pass2, out _);
            Assert.That(pass2, Is.EqualTo(pass1), "second pass must equal first");
            Assert.That(pass3, Is.EqualTo(pass2), "third pass must equal second");
        }

        [TestCase(InsertWithRegionInParens)]
        [TestCase("SELECT a,\r\n--[noformat]\r\n   b   ,   c,\r\n--[/noformat]\r\nd\r\nFROM t\r\n")]
        public void NoBlankLineAccumulatesBeforeClosingMarker(string input)
        {
            string output = Format(input, out _);
            Assert.That(output.Replace("\r", ""), Does.Not.Contain("\n\n--[/noformat]"),
                "no blank line may be injected before the closing marker");
        }

        [Test]
        public void MinifyRegionStillWorks()
        {
            string input = "SELECT 1\r\n--[minify]\r\nselect   2   ,   3\r\n--[/minify]\r\nSELECT 4\r\n";
            string pass1 = Format(input, out _);
            Assert.That(pass1, Does.Contain("--[minify]").IgnoreCase);
            Assert.That(pass1, Does.Contain("--[/minify]").IgnoreCase);
            string pass2 = Format(pass1, out _);
            string pass3 = Format(pass2, out _);
            Assert.That(pass3, Is.EqualTo(pass2), "minify regions must stabilize");
        }
    }
}
