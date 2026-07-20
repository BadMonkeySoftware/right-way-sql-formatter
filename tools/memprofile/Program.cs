// Scratch memory/alloc profiler for the formatter. NOT shipped.
// Usage:
//   dotnet run -c Release -- gen <seedFile> <targetBytes> <outFile>
//   dotnet run -c Release -- run <profile> <file>...      profile: default|alignequals|heavy|width200
using System.Diagnostics;
using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;
using PoorMansTSqlFormatterLib.Tokenizers;
using PoorMansTSqlFormatterLib.Parsers;

static string Cfg(string name) => name switch
{
    "default" => "",
    "alignequals" => "AlignColumnDefinitions=True,ColumnAliasStyle=EqualSign,ColumnAlwaysHasAlias=True",
    "width200" => "MaxLineWidth=200",
    "heavy" => "ExpandBetweenConditions=false,ExpandBooleanExpressions=false,ExpandCaseStatements=false,"
        + "ExpandInLists=false,UppercaseKeywords=false,TrailingCommas=True,AlignTableJoins=True,"
        + "ColumnAlwaysHasAlias=True,SelectFirstColumnOnNewLine=True,AlignColumnDefinitions=True,"
        + "AlignColumnDefinitionsInDDL=True,ColumnAliasStyle=EqualSign,IndentWhereAndOrConditions=True,"
        + "MaxLineWidth=200,CompactRaiserror=True,CompactSingleStatementBlocks=True",
    _ => throw new ArgumentException("unknown profile " + name),
};

if (args.Length >= 4 && args[0] == "gen")
{
    string seed = File.ReadAllText(args[1]);
    long target = long.Parse(args[2]);
    var sb = new System.Text.StringBuilder();
    while (sb.Length < target) { sb.Append(seed); sb.Append("\r\nGO\r\n"); }
    File.WriteAllText(args[3], sb.ToString());
    Console.Error.WriteLine($"wrote {args[3]} {new FileInfo(args[3]).Length} bytes");
    return;
}

if (args.Length >= 2 && args[0] == "depth")
{
    // depth <file>... -> max parse-tree node depth (iterative), per file
    var tok = new TSqlStandardTokenizer();
    var par = new TSqlStandardParser();
    foreach (var file in args.Skip(1))
    {
        var tree = par.ParseSQL(tok.TokenizeSQL(File.ReadAllText(file)));
        // iterative DFS (avoid recursing on the very trees we're measuring)
        int max = 0;
        var stack = new Stack<(PoorMansTSqlFormatterLib.ParseStructure.Node n, int d)>();
        stack.Push((tree, 1));
        while (stack.Count > 0)
        {
            var (n, d) = stack.Pop();
            if (d > max) max = d;
            foreach (var c in n.Children) stack.Push((c, d + 1));
        }
        Console.WriteLine($"{max}\t{Path.GetFileName(file)}");
    }
    return;
}

if (args.Length >= 3 && args[0] == "hash")
{
    // hash <profile> <file>...  -> "<sha256>  <outBytes>  <file>" per file (byte-identity check)
    var opts = new TSqlStandardFormatterOptions(Cfg(args[1]));
    using var sha = System.Security.Cryptography.SHA256.Create();
    foreach (var file in args.Skip(2))
    {
        var mgr = new SqlFormattingManager(new TSqlStandardFormatter(opts));
        bool err = false;
        string outp = mgr.Format(File.ReadAllText(file), ref err);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(outp);
        string hex = Convert.ToHexString(sha.ComputeHash(bytes));
        Console.WriteLine($"{hex}  {bytes.Length,9}  {args[1]}  {Path.GetFileName(file)}");
    }
    return;
}

if (args.Length >= 3 && args[0] == "run")
{
    string profile = args[1];
    var opts = new TSqlStandardFormatterOptions(Cfg(profile));

    // header
    Console.WriteLine("profile\tfile\tinBytes\toutBytes\ttok_ms\tpar_ms\tfmt_ms\ttok_MB\tpar_MB\tfmt_MB\ttotalAlloc_MB\tpeakLive_MB\tpeakWS_MB");

    foreach (var file in args.Skip(2))
    {
        string sql = File.ReadAllText(file);

        // peak live-heap sampler
        long peakLive = 0;
        bool stop = false;
        var sampler = new Thread(() =>
        {
            while (!stop) { long m = GC.GetTotalMemory(false); if (m > peakLive) peakLive = m; Thread.Sleep(2); }
        }) { IsBackground = true };

        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var proc = Process.GetCurrentProcess();
        long startAlloc = GC.GetTotalAllocatedBytes(true);
        sampler.Start();
        var sw = Stopwatch.StartNew();

        var tokenizer = new TSqlStandardTokenizer();
        var parser = new TSqlStandardParser();
        var formatter = new TSqlStandardFormatter(opts);

        long a0 = GC.GetTotalAllocatedBytes(true); long t0 = sw.ElapsedMilliseconds;
        var tokens = tokenizer.TokenizeSQL(sql);
        long a1 = GC.GetTotalAllocatedBytes(true); long t1 = sw.ElapsedMilliseconds;
        var tree = parser.ParseSQL(tokens);
        long a2 = GC.GetTotalAllocatedBytes(true); long t2 = sw.ElapsedMilliseconds;
        string output = formatter.FormatSQLTree(tree);
        long a3 = GC.GetTotalAllocatedBytes(true); long t3 = sw.ElapsedMilliseconds;

        sw.Stop();
        stop = true; sampler.Join();
        proc.Refresh();

        double MB(long b) => b / 1024.0 / 1024.0;
        Console.WriteLine(string.Join('\t',
            profile,
            Path.GetFileName(file),
            sql.Length,
            output.Length,
            t1 - t0, t2 - t1, t3 - t2,
            MB(a1 - a0).ToString("F1"),
            MB(a2 - a1).ToString("F1"),
            MB(a3 - a2).ToString("F1"),
            MB(a3 - startAlloc).ToString("F1"),
            MB(peakLive).ToString("F1"),
            MB(proc.PeakWorkingSet64).ToString("F1")));

        GC.KeepAlive(output);
    }
    return;
}

Console.Error.WriteLine("usage: gen <seed> <bytes> <out>  |  run <profile> <file>...");
