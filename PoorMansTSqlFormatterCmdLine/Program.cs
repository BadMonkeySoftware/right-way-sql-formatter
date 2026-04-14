/*
Right Way SQL Formatter - modernized T-SQL formatter CLI
Based on Poor Man's T-SQL Formatter by Tao Klerks
Licensed under GNU AGPL v3
*/

using System.CommandLine;
using System.Text;
using PoorMansTSqlFormatterLib.Formatters;

var indentString = new Option<string>("--indent-string", () => "\t", "String to use for indentation");
indentString.AddAlias("-is");

var spacesPerTab = new Option<int>("--spaces-per-tab", () => 4, "Number of spaces per tab");
spacesPerTab.AddAlias("-st");

var maxLineWidth = new Option<int>("--max-line-width", () => 999, "Maximum line width");
maxLineWidth.AddAlias("-mw");

var statementBreaks = new Option<int>("--statement-breaks", () => 2, "Newlines between statements");
statementBreaks.AddAlias("-sb");

var clauseBreaks = new Option<int>("--clause-breaks", () => 1, "Newlines between clauses");
clauseBreaks.AddAlias("-cb");

var trailingCommas = new Option<bool>("--trailing-commas", () => false, "Use trailing commas instead of leading");
trailingCommas.AddAlias("-tc");

var spaceAfterExpandedComma = new Option<bool>("--space-after-expanded-comma", () => false, "Add space after expanded comma");
spaceAfterExpandedComma.AddAlias("-sac");

var expandBetween = new Option<bool>("--expand-between", () => true, "Expand BETWEEN conditions");
expandBetween.AddAlias("-ebc");

var expandBoolean = new Option<bool>("--expand-boolean", () => true, "Expand boolean expressions");
expandBoolean.AddAlias("-ebe");

var expandCase = new Option<bool>("--expand-case", () => true, "Expand CASE statements");
expandCase.AddAlias("-ecs");

var expandCommaLists = new Option<bool>("--expand-comma-lists", () => true, "Expand comma lists");
expandCommaLists.AddAlias("-ecl");

var expandInLists = new Option<bool>("--expand-in-lists", () => true, "Expand IN lists");
expandInLists.AddAlias("-eil");

var breakJoin = new Option<bool>("--break-join-on-sections", () => false, "Break JOIN on sections");
breakJoin.AddAlias("-bjo");

var uppercaseKeywords = new Option<bool>("--uppercase-keywords", () => true, "Uppercase keywords");
uppercaseKeywords.AddAlias("-uk");

var standardizeKeywords = new Option<bool>("--standardize-keywords", () => true, "Standardize keywords");
standardizeKeywords.AddAlias("-sk");

var allowParsingErrors = new Option<bool>("--allow-parsing-errors", () => false, "Allow and continue on parsing errors");
allowParsingErrors.AddAlias("-ae");

var outputFile = new Option<string?>("--output", () => null, "Output file path (default: stdout)");
outputFile.AddAlias("-o");

var inputFile = new Argument<string?>("input-file", () => null, "Input SQL file (default: stdin)");

var rootCommand = new RootCommand("Right Way SQL Formatter - formats T-SQL files")
{
    indentString, spacesPerTab, maxLineWidth, statementBreaks, clauseBreaks,
    trailingCommas, spaceAfterExpandedComma, expandBetween, expandBoolean,
    expandCase, expandCommaLists, expandInLists, breakJoin,
    uppercaseKeywords, standardizeKeywords, allowParsingErrors,
    outputFile, inputFile
};

rootCommand.SetHandler(async (context) =>
{
    var opts = new TSqlStandardFormatterOptions
    {
        IndentString = context.ParseResult.GetValueForOption(indentString)!,
        SpacesPerTab = context.ParseResult.GetValueForOption(spacesPerTab),
        MaxLineWidth = context.ParseResult.GetValueForOption(maxLineWidth),
        NewStatementLineBreaks = context.ParseResult.GetValueForOption(statementBreaks),
        NewClauseLineBreaks = context.ParseResult.GetValueForOption(clauseBreaks),
        TrailingCommas = context.ParseResult.GetValueForOption(trailingCommas),
        SpaceAfterExpandedComma = context.ParseResult.GetValueForOption(spaceAfterExpandedComma),
        ExpandBetweenConditions = context.ParseResult.GetValueForOption(expandBetween),
        ExpandBooleanExpressions = context.ParseResult.GetValueForOption(expandBoolean),
        ExpandCaseStatements = context.ParseResult.GetValueForOption(expandCase),
        ExpandCommaLists = context.ParseResult.GetValueForOption(expandCommaLists),
        ExpandInLists = context.ParseResult.GetValueForOption(expandInLists),
        BreakJoinOnSections = context.ParseResult.GetValueForOption(breakJoin),
        UppercaseKeywords = context.ParseResult.GetValueForOption(uppercaseKeywords),
        KeywordStandardization = context.ParseResult.GetValueForOption(standardizeKeywords),
    };

    bool allowErrors = context.ParseResult.GetValueForOption(allowParsingErrors);
    string? outPath = context.ParseResult.GetValueForOption(outputFile);
    string? inPath = context.ParseResult.GetValueForArgument(inputFile);

    string inputSql;
    if (!string.IsNullOrEmpty(inPath))
    {
        inputSql = await File.ReadAllTextAsync(inPath);
    }
    else
    {
        Console.InputEncoding = Encoding.UTF8;
        inputSql = await Console.In.ReadToEndAsync();
    }

    var formatter = new TSqlStandardFormatter(opts);
    formatter.ErrorOutputPrefix = "/* WARNING: Parsing error encountered */\n";
    var manager = new PoorMansTSqlFormatterLib.SqlFormattingManager(formatter);

    bool parsingError = false;
    string result;
    try
    {
        result = manager.Format(inputSql, ref parsingError);
        if (allowErrors) parsingError = false;
    }
    catch (Exception ex)
    {
        await Console.Error.WriteLineAsync($"Error formatting SQL: {ex.Message}");
        context.ExitCode = 1;
        return;
    }

    if (parsingError)
    {
        await Console.Error.WriteLineAsync("Warning: parsing error encountered in input.");
        context.ExitCode = 5;
        return;
    }

    if (!string.IsNullOrEmpty(outPath))
    {
        await File.WriteAllTextAsync(outPath, result, Encoding.UTF8);
    }
    else
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(result);
    }
});

return await rootCommand.InvokeAsync(args);
