/*
Right Way SQL Formatter - modernized T-SQL formatter CLI
Based on Poor Man's T-SQL Formatter by Tao Klerks
Licensed under GNU AGPL v3

Argument parsing is hand-rolled (no external dependencies): every option is
--long-name / -alias with boolean, integer, or string values, accepted as
either "--flag=value" or "--flag value"; bare boolean flags mean true.
*/

using System.Text;
using PoorMansTSqlFormatterLib.Formatters;

const int EXIT_OK = 0;
const int EXIT_ERROR = 1;
const int EXIT_PARSE_WARNING = 5;

// ---------------------------------------------------------------------------
// Option table: long name, alias, kind, default (as string), description
// ---------------------------------------------------------------------------
var optionDefs = new List<OptionDef>
{
    new("--indent-string",                  "-is",   OptKind.Str,  "    ",  "String to use for indentation (\\t = tab, \\s = space)"),
    new("--spaces-per-tab",                 "-st",   OptKind.Int,  "4",     "Number of spaces per tab"),
    new("--max-line-width",                 "-mw",   OptKind.Int,  "999",   "Maximum line width"),
    new("--statement-breaks",               "-sb",   OptKind.Int,  "2",     "Newlines between statements"),
    new("--clause-breaks",                  "-cb",   OptKind.Int,  "1",     "Newlines between clauses"),
    new("--trailing-commas",                "-tc",   OptKind.Bool, "false", "Use trailing commas instead of leading"),
    new("--space-after-expanded-comma",     "-sac",  OptKind.Bool, "false", "Add space after expanded comma"),
    new("--expand-between",                 "-ebc",  OptKind.Bool, "true",  "Expand BETWEEN conditions"),
    new("--expand-boolean",                 "-ebe",  OptKind.Bool, "true",  "Expand boolean expressions"),
    new("--expand-case",                    "-ecs",  OptKind.Bool, "true",  "Expand CASE statements"),
    new("--expand-comma-lists",             "-ecl",  OptKind.Bool, "true",  "Expand comma lists"),
    new("--select-first-column-newline",    "-sfcn", OptKind.Bool, "false", "Break first SELECT column to new line (requires --expand-comma-lists)"),
    new("--expand-in-lists",                "-eil",  OptKind.Bool, "true",  "Expand IN lists"),
    new("--break-join-on-sections",         "-bjo",  OptKind.Bool, "false", "Break JOIN on sections"),
    new("--uppercase-keywords",             "-uk",   OptKind.Bool, "true",  "Uppercase keywords"),
    new("--standardize-keywords",           "-sk",   OptKind.Bool, "true",  "Standardize keywords"),
    new("--allow-parsing-errors",           "-ae",   OptKind.Bool, "false", "Exit 0 even when the input has parse errors"),
    new("--alias-style",                    "-as",   OptKind.Str,  "as",    "Column alias style: 'as' (col AS alias) or 'equals' (alias = col)"),
    new("--align-columns",                  "-ac",   OptKind.Bool, "false", "Align aliases vertically in SELECT column list"),
    new("--indent-join-on",                 "-ijo",  OptKind.Bool, "false", "Indent ON clause relative to JOIN columns"),
    new("--indent-where-and-or",            "-iwo",  OptKind.Bool, "false", "Put AND/OR in WHERE onto separate, indented lines"),
    new("--align-ddl-columns",              "-adc",  OptKind.Bool, "false", "Align column definitions in CREATE TABLE vertically"),
    new("--ddl-constraints-newline",        "-dcn",  OptKind.Bool, "false", "Put each column constraint on its own line in CREATE TABLE"),
    new("--align-table-joins",              "-atj",  OptKind.Bool, "false", "Align FROM/JOIN table names, aliases, and ON conditions vertically"),
    new("--align-table-joins-add-aliases",  "-atja", OptKind.Bool, "true",  "With --align-table-joins: add aliases to tables that have none"),
    new("--column-always-has-alias",        "-caha", OptKind.Bool, "false", "Ensure every SELECT column has an explicit AS alias"),
    new("--compact-raiserror",              "-cre",  OptKind.Bool, "false", "Keep RAISERROR(...) argument lists on a single line"),
    new("--remove-harmless-brackets",       "-rhb",  OptKind.Bool, "false", "Strip [brackets] from names that provably don't need them"),
    new("--compact-single-statement-blocks","-csb",  OptKind.Bool, "false", "Render single-statement IF/ELSE/WHILE bodies (no BEGIN/END) inline when they fit"),
    new("--output",                         "-o",    OptKind.Str,  "",      "Output file path (default: stdout)"),
};

var lookup = new Dictionary<string, OptionDef>(StringComparer.Ordinal);
foreach (var def in optionDefs)
{
    lookup[def.LongName] = def;
    lookup[def.Alias] = def;
}

// ---------------------------------------------------------------------------
// Parse arguments
// ---------------------------------------------------------------------------
var values = optionDefs.ToDictionary(d => d.LongName, d => d.DefaultValue, StringComparer.Ordinal);
string? inputPath = null;

static bool TryParseBool(string s, out bool result) =>
    bool.TryParse(s, out result);

for (int i = 0; i < args.Length; i++)
{
    string arg = args[i];

    if (arg is "--help" or "-h" or "-?" or "/?")
    {
        PrintUsage(optionDefs);
        return EXIT_OK;
    }

    if (arg.StartsWith('-') && arg.Length > 1 && !char.IsDigit(arg[1]))
    {
        string name = arg;
        string? inlineValue = null;
        int eq = arg.IndexOf('=');
        if (eq >= 0)
        {
            name = arg.Substring(0, eq);
            inlineValue = arg.Substring(eq + 1);
        }

        if (!lookup.TryGetValue(name, out OptionDef? def))
        {
            await Console.Error.WriteLineAsync($"Unknown option: {name}  (use --help for the option list)");
            return EXIT_ERROR;
        }

        string? value = inlineValue;
        if (value == null)
        {
            if (def.Kind == OptKind.Bool)
            {
                //bare boolean flag means true; also accept a following true/false token
                if (i + 1 < args.Length && TryParseBool(args[i + 1], out _))
                    value = args[++i];
                else
                    value = "true";
            }
            else
            {
                if (i + 1 >= args.Length)
                {
                    await Console.Error.WriteLineAsync($"Option {def.LongName} requires a value.");
                    return EXIT_ERROR;
                }
                value = args[++i];
            }
        }

        if (def.Kind == OptKind.Bool && !TryParseBool(value, out _))
        {
            await Console.Error.WriteLineAsync($"Option {def.LongName} expects true or false, got '{value}'.");
            return EXIT_ERROR;
        }
        if (def.Kind == OptKind.Int && !int.TryParse(value, out _))
        {
            await Console.Error.WriteLineAsync($"Option {def.LongName} expects an integer, got '{value}'.");
            return EXIT_ERROR;
        }

        values[def.LongName] = value;
    }
    else
    {
        if (inputPath != null)
        {
            await Console.Error.WriteLineAsync($"Unexpected argument: {arg} (input file already specified as {inputPath})");
            return EXIT_ERROR;
        }
        inputPath = arg;
    }
}

string S(string name) => values[name];
bool B(string name) => bool.Parse(values[name]);
int I(string name) => int.Parse(values[name]);

// ---------------------------------------------------------------------------
// Build options and run
// ---------------------------------------------------------------------------
var opts = new TSqlStandardFormatterOptions
{
    IndentString = S("--indent-string"),
    SpacesPerTab = I("--spaces-per-tab"),
    MaxLineWidth = I("--max-line-width"),
    NewStatementLineBreaks = I("--statement-breaks"),
    NewClauseLineBreaks = I("--clause-breaks"),
    TrailingCommas = B("--trailing-commas"),
    SpaceAfterExpandedComma = B("--space-after-expanded-comma"),
    ExpandBetweenConditions = B("--expand-between"),
    ExpandBooleanExpressions = B("--expand-boolean"),
    ExpandCaseStatements = B("--expand-case"),
    ExpandCommaLists = B("--expand-comma-lists"),
    SelectFirstColumnOnNewLine = B("--select-first-column-newline"),
    ExpandInLists = B("--expand-in-lists"),
    BreakJoinOnSections = B("--break-join-on-sections"),
    UppercaseKeywords = B("--uppercase-keywords"),
    KeywordStandardization = B("--standardize-keywords"),
    ColumnAliasStyle = S("--alias-style") == "equals"
        ? ColumnAliasStyle.EqualSign
        : ColumnAliasStyle.AsKeyword,
    AlignColumnDefinitions = B("--align-columns"),
    IndentJoinOnClause = B("--indent-join-on"),
    IndentWhereAndOrConditions = B("--indent-where-and-or"),
    AlignColumnDefinitionsInDDL = B("--align-ddl-columns"),
    DDLConstraintsOnNewLine = B("--ddl-constraints-newline"),
    AlignTableJoins = B("--align-table-joins"),
    AlignTableJoinsAddAliases = B("--align-table-joins-add-aliases"),
    ColumnAlwaysHasAlias = B("--column-always-has-alias"),
    CompactRaiserror = B("--compact-raiserror"),
    RemoveHarmlessBrackets = B("--remove-harmless-brackets"),
    CompactSingleStatementBlocks = B("--compact-single-statement-blocks"),
};

bool allowErrors = B("--allow-parsing-errors");
string? outPath = string.IsNullOrEmpty(S("--output")) ? null : S("--output");

string inputSql;
if (!string.IsNullOrEmpty(inputPath))
{
    inputSql = await File.ReadAllTextAsync(inputPath);
}
else
{
    Console.InputEncoding = Encoding.UTF8;
    inputSql = await Console.In.ReadToEndAsync();
}

var formatter = new TSqlStandardFormatter(opts);
formatter.ErrorOutputPrefix = "/* WARNING: Parsing error encountered */\n";

bool parsingError = false;
string result;
try
{
    // Run the pipeline directly (rather than via SqlFormattingManager) so that,
    // on parse errors, a detailed warning comment can be composed and installed
    // as the formatter's error prefix BEFORE formatting.
    var tokenizer = new PoorMansTSqlFormatterLib.Tokenizers.TSqlStandardTokenizer();
    var parser = new PoorMansTSqlFormatterLib.Parsers.TSqlStandardParser();

    var tokenList = tokenizer.TokenizeSQL(inputSql);
    var sqlTree = parser.ParseSQL(tokenList);
    parsingError = sqlTree.GetAttributeValue(
        PoorMansTSqlFormatterLib.Interfaces.SqlStructureConstants.ANAME_ERRORFOUND) == "1";

    if (parsingError)
    {
        var errorDescriptions = PoorMansTSqlFormatterLib.ParseErrorAnalyzer.GetErrorDescriptions(sqlTree, tokenList);
        var warningComment = new StringBuilder();
        warningComment.AppendLine("-- WARNING! ERRORS ENCOUNTERED DURING SQL PARSING - formatted output may be incorrect:");
        foreach (string description in errorDescriptions)
            warningComment.AppendLine("--   " + description);
        formatter.ErrorOutputPrefix = warningComment.ToString();
    }

    result = formatter.FormatSQLTree(sqlTree);
    if (allowErrors) parsingError = false;
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Error formatting SQL: {ex.Message}");
    return EXIT_ERROR;
}

int exitCode = EXIT_OK;
if (parsingError)
{
    // Emit the formatted output (with its warning-comment prefix) anyway; the
    // non-zero exit code is the machine-readable signal that parsing failed.
    await Console.Error.WriteLineAsync("Warning: parsing error encountered in input.");
    exitCode = EXIT_PARSE_WARNING;
}

if (outPath != null)
{
    await File.WriteAllTextAsync(outPath, result, Encoding.UTF8);
}
else
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.WriteLine(result);
}

return exitCode;

static void PrintUsage(List<OptionDef> defs)
{
    Console.WriteLine("Right Way SQL Formatter - formats T-SQL files");
    Console.WriteLine();
    Console.WriteLine("Usage: SqlFormatter [options] [input-file]");
    Console.WriteLine("       (reads stdin when no input file is given; writes stdout unless --output is set)");
    Console.WriteLine();
    Console.WriteLine("Options (--flag=value or --flag value; bare boolean flags mean true):");
    foreach (var def in defs)
    {
        string kind = def.Kind switch { OptKind.Bool => "bool", OptKind.Int => "int", _ => "string" };
        Console.WriteLine($"  {def.LongName,-38} {def.Alias,-6} {kind,-7} default: {(def.DefaultValue == "" ? "(none)" : def.DefaultValue),-8} {def.Description}");
    }
}

internal enum OptKind { Bool, Int, Str }

internal sealed record OptionDef(string LongName, string Alias, OptKind Kind, string DefaultValue, string Description);
