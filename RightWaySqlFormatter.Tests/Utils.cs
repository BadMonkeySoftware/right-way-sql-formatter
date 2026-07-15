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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PoorMansTSqlFormatterLib.Interfaces;
using PoorMansTSqlFormatterLib.ParseStructure;

namespace PoorMansTSqlFormatterTests
{
    static class Utils
    {
        public const string DATAFOLDER = "Data";
        public const string INPUTSQLFOLDER = "InputSql";
        public const string PARSEDSQLFOLDER = "ParsedSql";
        public const string STANDARDFORMATSQLFOLDER = "StandardFormatSql";

        public const string INVALID_SQL_WARNING = "THIS TEST FILE IS NOT VALID SQL"; //currently unused, could be used for special exceptions
        public const string REFORMATTING_INCONSISTENCY_WARNING = "KNOWN SQL REFORMATTING INCONSISTENCY";
        public const string ERROR_FOUND_WARNING = "--WARNING! ERRORS ENCOUNTERED DURING SQL PARSING!\r\n"; //repeated wrt constant in the library, intentionally.

        public static string GetTestContentFolder(string folderName)
        {
            DirectoryInfo thisDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            return Path.Combine(thisDirectory.FullName, DATAFOLDER, folderName);
        }

        public static IEnumerable<string> FolderFileNameIterator(string path)
        {
            DirectoryInfo textFileFolder = new DirectoryInfo(path);
            foreach (FileInfo sampleFile in textFileFolder.GetFiles())
            {
                yield return sampleFile.Name;
            }
        }

        public static void StripWhiteSpaceFromSqlTree(Node sqlTree)
        {
            StripElementNamesFromXml(sqlTree, new[] { SqlStructureConstants.ENAME_WHITESPACE });
        }

        public static void StripCommentsFromSqlTree(Node sqlTree)
        {
            StripElementNamesFromXml(sqlTree, SqlStructureConstants.ENAMELIST_COMMENT);
        }

        private static void StripElementNamesFromXml(Node sqlTree, IEnumerable<string> elementNames)
        {
            var toRemove = sqlTree.ChildrenByNames(elementNames).ToList();
            foreach (Node childThing in toRemove)
                sqlTree.RemoveChild(childThing);

            foreach (Node childThing in sqlTree.Children)
                StripElementNamesFromXml(childThing, elementNames);
        }

        public static IEnumerable<string> GetInputSqlFileNames()
        {
            return FolderFileNameIterator(GetTestContentFolder("InputSql"));
        }

        public static string GetTestFileContent(string fileName, string testFolderPath)
        {
            return File.ReadAllText(Path.Combine(Utils.GetTestContentFolder(testFolderPath), fileName))
                .Replace("\r\n", "\n").Replace("\r", "\n");
        }

        // Expected-output filenames encode formatter options as short slugs, filesystem-safe
        // on every platform (no parens/equals/commas/spaces, and much shorter for Windows
        // MAX_PATH):   <InputBaseName>__<Slug1>_<Slug2>.sql
        // Slugs contain no underscores; '__' separates the base name from the config part.
        // Each slug maps to one TSqlStandardFormatterOptions fragment below. Add new slugs
        // here when new option combinations get expected files.
        private static readonly Dictionary<string, string> CONFIG_SLUGS = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "NoExpandComma",   "ExpandCommaLists=false" },
            { "Width60",         "MaxLineWidth=60" },
            { "Tab8",            "SpacesPerTab=8" },
            { "Clause2",         "NewClauseLineBreaks=2" },
            { "Stmt3",           "NewStatementLineBreaks=3" },
            { "NoUpper",         "UppercaseKeywords=false" },
            { "StdKw",           "KeywordStandardization=true" },
            { "Html",            "HTMLColoring=true" },
            { "SpaceComma",      "SpaceAfterExpandedComma=true" },
            { "NoExpandBool",    "ExpandBooleanExpressions=false" },
            { "NoExpandBetween", "ExpandBetweenConditions=false" },
            { "NoExpandCase",    "ExpandCaseStatements=false" },
            { "TrailComma",      "TrailingCommas=true" },
            { "Indent6Sp",       "IndentString=      " },
            { "BreakJoinOn",     "BreakJoinOnSections=true" },
            { "AlignCols",       "AlignColumnDefinitions=True" },
            { "EqAlias",         "ColumnAliasStyle=EqualSign" },
            { "AlignDdl",        "AlignColumnDefinitionsInDDL=True" },
            { "DdlConstrNL",     "DDLConstraintsOnNewLine=True" },
            { "IndentOn",        "IndentJoinOnClause=True" },
            { "IndentAndOr",     "IndentWhereAndOrConditions=True" },
            { "AlignJoins",      "AlignTableJoins=True" },
            { "AlwaysAlias",     "ColumnAlwaysHasAlias=True" },
            { "FirstColNL",      "SelectFirstColumnOnNewLine=True" },
            { "CompactRaiserror","CompactRaiserror=True" },
            { "RmBrackets","RemoveHarmlessBrackets=True" },
            { "CompactBlocks",   "CompactSingleStatementBlocks=True" },
            { "NoJoinAliases",   "AlignTableJoinsAddAliases=False" },
        };

        private const string CONFIG_MARKER = "__";

        public static string StripFileConfigString(string fileName)
        {
            int marker = fileName.IndexOf(CONFIG_MARKER, StringComparison.Ordinal);
            if (marker < 0)
                return fileName;
            return fileName.Substring(0, marker) + Path.GetExtension(fileName);
        }

        public static string GetFileConfigString(string fileName)
        {
            int marker = fileName.IndexOf(CONFIG_MARKER, StringComparison.Ordinal);
            if (marker < 0)
                return "";
            string extension = Path.GetExtension(fileName);
            string slugPart = fileName.Substring(marker + CONFIG_MARKER.Length,
                fileName.Length - marker - CONFIG_MARKER.Length - extension.Length);
            var fragments = new List<string>();
            foreach (string slug in slugPart.Split('_'))
            {
                if (!CONFIG_SLUGS.TryGetValue(slug, out string? fragment))
                    throw new ArgumentException(
                        $"Unknown config slug '{slug}' in test data filename '{fileName}'. " +
                        "Add it to Utils.CONFIG_SLUGS.");
                fragments.Add(fragment);
            }
            return string.Join(",", fragments);
        }
    }
}
