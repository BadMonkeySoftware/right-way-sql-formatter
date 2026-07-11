/**
 * This extension bundles cross-platform SqlFormatter binaries in bin/<rid>/SqlFormatter[.exe].
 * At runtime, it auto-detects the current platform/arch and launches the correct binary.
 *
 * Supported RIDs:
 *   win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, linux-arm64
 *
 * If you add new platforms, update both build-cli.js and resolveExecutablePath().
 */
import * as vscode from 'vscode';
import * as cp from 'child_process';
import * as path from 'path';
import * as fs from 'fs';

// ---------------------------------------------------------------------------
// Extension entry points
// ---------------------------------------------------------------------------

/**
 * Called by VS Code when the extension is first activated.
 * Registers the two formatting commands.
 */
export function activate(context: vscode.ExtensionContext): void {
    console.log('Right Way SQL Formatter activated');

    // Serves formatted-SQL previews as read-only virtual documents for the diff view.
    const previewProvider = new FormatPreviewProvider();
    context.subscriptions.push(
        vscode.workspace.registerTextDocumentContentProvider(PREVIEW_SCHEME, previewProvider)
    );

    // "Format Document" — applies minimal edits to the editor content
    context.subscriptions.push(
        vscode.commands.registerCommand('rightWaySqlFormatter.formatDocument', () => {
            const editor = vscode.window.activeTextEditor;
            if (!editor) {
                vscode.window.showErrorMessage('No active editor.');
                return;
            }
            formatDocument(editor.document);
        })
    );

    // "Format Selection" — replaces only the selected text with formatted SQL
    context.subscriptions.push(
        vscode.commands.registerCommand('rightWaySqlFormatter.formatSelection', () => {
            const editor = vscode.window.activeTextEditor;
            if (!editor) {
                vscode.window.showErrorMessage('No active editor.');
                return;
            }
            if (editor.selection.isEmpty) {
                vscode.window.showWarningMessage('No text selected. Use "Format Document" to format the whole file.');
                return;
            }
            formatRange(editor, editor.selection);
        })
    );

    // "Format Document (Preview)" — opens a native diff (original vs formatted)
    // so changes can be reviewed before applying.
    context.subscriptions.push(
        vscode.commands.registerCommand('rightWaySqlFormatter.formatDocumentPreview', () => {
            const editor = vscode.window.activeTextEditor;
            if (!editor) {
                vscode.window.showErrorMessage('No active editor.');
                return;
            }
            previewFormatDocument(editor.document, previewProvider);
        })
    );

    // Also register as a VS Code document formatter so it participates in
    // "Format Document" (⇧⌥F / Shift+Alt+F) and format-on-save for SQL files.
    context.subscriptions.push(
        vscode.languages.registerDocumentFormattingEditProvider('sql', {
            async provideDocumentFormattingEdits(document: vscode.TextDocument): Promise<vscode.TextEdit[]> {
                try {
                    const result = await runFormatter(document.getText());
                    if (result.parseWarning) {
                        vscode.window.showWarningMessage(
                            'SQL parsing errors encountered — output may be incorrect. See the warning comment at the top of the formatted SQL.'
                        );
                    }
                    return computeMinimalEdits(document, result.sql);
                } catch (err) {
                    vscode.window.showErrorMessage(`SQL Formatter error: ${err instanceof Error ? err.message : String(err)}`);
                    return [];
                }
            }
        })
    );
}

export function deactivate(): void {
    // Nothing to clean up
}

// ---------------------------------------------------------------------------
// Core formatting logic
// ---------------------------------------------------------------------------

/**
 * Formats the whole document, applying the result as MINIMAL edits (only the
 * changed line ranges are touched) so cursor position, undo granularity, and
 * unchanged-region decorations survive formatting.
 */
async function formatDocument(document: vscode.TextDocument): Promise<void> {
    let result: FormatterResult;
    try {
        result = await runFormatter(document.getText());
    } catch (err) {
        vscode.window.showErrorMessage(`SQL Formatter error: ${err instanceof Error ? err.message : String(err)}`);
        return;
    }

    const edits = computeMinimalEdits(document, result.sql);
    if (edits.length > 0) {
        const workspaceEdit = new vscode.WorkspaceEdit();
        workspaceEdit.set(document.uri, edits);
        await vscode.workspace.applyEdit(workspaceEdit);
    }

    if (result.parseWarning) {
        showParseWarning();
    }
}

/**
 * Formats the text within `range` in the given `editor` by passing it through
 * the SqlFormatter CLI and replacing the range with the result.
 */
async function formatRange(editor: vscode.TextEditor, range: vscode.Range): Promise<void> {
    const inputSql = editor.document.getText(range);

    let result: FormatterResult;
    try {
        result = await runFormatter(inputSql);
    } catch (err) {
        vscode.window.showErrorMessage(`SQL Formatter error: ${err instanceof Error ? err.message : String(err)}`);
        return;
    }

    // Apply the formatted result back into the editor
    await editor.edit(editBuilder => {
        editBuilder.replace(range, result.sql);
    });

    if (result.parseWarning) {
        showParseWarning();
    }
}

function showParseWarning(): void {
    vscode.window.showWarningMessage(
        'SQL parsing errors encountered — output may be incorrect. See the warning comment at the top of the formatted SQL for details.'
    );
}

/** Result of a formatter run: the output SQL plus whether parse errors were reported. */
interface FormatterResult {
    sql: string;
    parseWarning: boolean;
}

// ---------------------------------------------------------------------------
// Format preview (native diff view)
// ---------------------------------------------------------------------------

const PREVIEW_SCHEME = 'rwsql-format-preview';

/** Read-only virtual documents backing the right-hand side of the format-preview diff. */
class FormatPreviewProvider implements vscode.TextDocumentContentProvider {
    private readonly contents = new Map<string, string>();
    private readonly emitter = new vscode.EventEmitter<vscode.Uri>();
    readonly onDidChange = this.emitter.event;

    provideTextDocumentContent(uri: vscode.Uri): string {
        return this.contents.get(uri.toString()) ?? '';
    }

    update(uri: vscode.Uri, content: string): void {
        this.contents.set(uri.toString(), content);
        this.emitter.fire(uri);
    }

    remove(uri: vscode.Uri): void {
        this.contents.delete(uri.toString());
    }
}

/**
 * Opens VS Code's native diff editor showing original (left) vs formatted
 * (right), then offers an Apply action. Applying re-formats the document's
 * CURRENT text (safe even if it changed while the preview was open) using
 * minimal edits.
 */
async function previewFormatDocument(document: vscode.TextDocument, provider: FormatPreviewProvider): Promise<void> {
    let result: FormatterResult;
    try {
        result = await runFormatter(document.getText());
    } catch (err) {
        vscode.window.showErrorMessage(`SQL Formatter error: ${err instanceof Error ? err.message : String(err)}`);
        return;
    }

    const fileName = document.uri.path.split('/').pop() ?? 'untitled.sql';
    const previewUri = vscode.Uri.from({
        scheme: PREVIEW_SCHEME,
        path: document.uri.path,
        query: document.uri.toString()
    });
    provider.update(previewUri, result.sql);

    await vscode.commands.executeCommand(
        'vscode.diff',
        document.uri,
        previewUri,
        `${fileName}: current ↔ formatted`,
        { preview: true }
    );

    if (result.parseWarning) {
        showParseWarning();
    }

    const choice = await vscode.window.showInformationMessage(
        `Apply formatting to ${fileName}?`,
        'Apply',
        'Discard'
    );

    if (choice === 'Apply') {
        await formatDocument(document); // re-runs on current text; minimal edits
    }
    if (choice !== undefined) {
        await closeDiffTab(previewUri);
    }
    provider.remove(previewUri);
}

/** Closes the diff tab whose modified (right) side is the given preview URI. */
async function closeDiffTab(previewUri: vscode.Uri): Promise<void> {
    for (const group of vscode.window.tabGroups.all) {
        for (const tab of group.tabs) {
            if (tab.input instanceof vscode.TabInputTextDiff
                && tab.input.modified.toString() === previewUri.toString()) {
                await vscode.window.tabGroups.close(tab);
                return;
            }
        }
    }
}

// ---------------------------------------------------------------------------
// Minimal-edit computation
// ---------------------------------------------------------------------------

/**
 * Computes line-based minimal edits transforming the document's text into
 * `formatted`. Uses common prefix/suffix trimming plus an LCS diff over the
 * remaining middle section (falling back to a single replace when the middle
 * is very large). EOL differences between the CLI output and the document are
 * normalized away: replacement text always uses the document's EOL.
 */
function computeMinimalEdits(document: vscode.TextDocument, formatted: string): vscode.TextEdit[] {
    const eol = document.eol === vscode.EndOfLine.CRLF ? '\r\n' : '\n';
    const a = document.getText().split(/\r\n|\r|\n/);
    const b = formatted.split(/\r\n|\r|\n/);

    // Trim common prefix / suffix
    let prefix = 0;
    while (prefix < a.length && prefix < b.length && a[prefix] === b[prefix]) prefix++;
    let suffix = 0;
    while (suffix < a.length - prefix && suffix < b.length - prefix
        && a[a.length - 1 - suffix] === b[b.length - 1 - suffix]) suffix++;

    const aMid = a.slice(prefix, a.length - suffix);
    const bMid = b.slice(prefix, b.length - suffix);
    if (aMid.length === 0 && bMid.length === 0) return [];

    // Hunks over the middle section: [aStart, aEnd) in a replaced by [bStart, bEnd) in b
    type Hunk = { aStart: number; aEnd: number; bStart: number; bEnd: number };
    let hunks: Hunk[];
    if (aMid.length * bMid.length > 1_000_000) {
        // Middle too large for LCS — single replace of the whole middle
        hunks = [{ aStart: prefix, aEnd: prefix + aMid.length, bStart: prefix, bEnd: prefix + bMid.length }];
    } else {
        hunks = lcsHunks(aMid, bMid).map(h => ({
            aStart: h.aStart + prefix, aEnd: h.aEnd + prefix,
            bStart: h.bStart + prefix, bEnd: h.bEnd + prefix
        }));
    }

    const edits: vscode.TextEdit[] = [];
    const lastLine = a.length; // a.length lines => line indices 0..a.length-1
    for (const h of hunks) {
        const newText = b.slice(h.bStart, h.bEnd).join(eol);
        let range: vscode.Range;
        let text: string;
        if (h.aEnd < lastLine) {
            // Replace whole lines [aStart, aEnd): range ends at start of line aEnd
            range = new vscode.Range(h.aStart, 0, h.aEnd, 0);
            text = newText.length > 0 ? newText + eol : '';
        } else {
            // Replacement reaches end of document
            const endPos = document.lineAt(document.lineCount - 1).range.end;
            range = new vscode.Range(new vscode.Position(h.aStart, 0), endPos);
            text = newText;
        }
        edits.push(vscode.TextEdit.replace(range, text));
    }
    return edits;
}

/**
 * Standard LCS diff over two string arrays, returning replace-hunks
 * ([aStart,aEnd) -> [bStart,bEnd)) in ascending order.
 */
function lcsHunks(a: string[], b: string[]): { aStart: number; aEnd: number; bStart: number; bEnd: number }[] {
    const n = a.length, m = b.length;
    // DP table of LCS lengths
    const width = m + 1;
    const dp = new Int32Array((n + 1) * width);
    for (let i = n - 1; i >= 0; i--) {
        for (let j = m - 1; j >= 0; j--) {
            dp[i * width + j] = a[i] === b[j]
                ? dp[(i + 1) * width + j + 1] + 1
                : Math.max(dp[(i + 1) * width + j], dp[i * width + j + 1]);
        }
    }
    // Backtrack, emitting hunks for runs of non-matching lines
    const hunks: { aStart: number; aEnd: number; bStart: number; bEnd: number }[] = [];
    let i = 0, j = 0, aStart = 0, bStart = 0;
    let inHunk = false;
    const flush = (aEnd: number, bEnd: number) => {
        if (inHunk) {
            hunks.push({ aStart, aEnd, bStart, bEnd });
            inHunk = false;
        }
    };
    while (i < n && j < m) {
        if (a[i] === b[j]) {
            flush(i, j);
            i++; j++;
        } else {
            if (!inHunk) { aStart = i; bStart = j; inHunk = true; }
            if (dp[(i + 1) * width + j] >= dp[i * width + j + 1]) i++;
            else j++;
        }
    }
    if (i < n || j < m) {
        if (!inHunk) { aStart = i; bStart = j; inHunk = true; }
        i = n; j = m;
    }
    flush(i, j);
    return hunks;
}

/** Exit code used by the SqlFormatter CLI to signal "output emitted, but input had parse errors". */
const EXIT_CODE_PARSE_WARNING = 5;

/**
 * Runs the SqlFormatter CLI with the current settings, piping `sql` in via
 * stdin and returning the formatted output from stdout.
 *
 * Exit code 0 = clean; exit code 5 = formatted output emitted but the input
 * had parse errors (a warning comment is prepended to the output by the CLI).
 * Throws for any other exit code or if the executable cannot be found.
 */
function runFormatter(sql: string): Promise<FormatterResult> {
    return new Promise((resolve, reject) => {
        const execPath = resolveExecutablePath();
        if (!execPath) {
            reject(new Error(
                'SqlFormatter executable not found. Set "rightWaySqlFormatter.executablePath" in settings, ' +
                'or publish a release build to PATH.'
            ));
            return;
        }

        const args = buildArgs();
        const proc = cp.spawn(execPath, args, { stdio: ['pipe', 'pipe', 'pipe'] });

        let stdout = '';
        let stderr = '';

        proc.stdout.on('data', (chunk: Buffer) => { stdout += chunk.toString('utf8'); });
        proc.stderr.on('data', (chunk: Buffer) => { stderr += chunk.toString('utf8'); });

        proc.on('error', (err) => {
            reject(new Error(`Failed to start formatter: ${err.message}. Executable: ${execPath}`));
        });

        proc.on('close', (code) => {
            if (code === 0 || (code === EXIT_CODE_PARSE_WARNING && stdout.length > 0)) {
                // Strip trailing newline added by Console.WriteLine in the CLI
                resolve({
                    sql: stdout.replace(/\n$/, ''),
                    parseWarning: code === EXIT_CODE_PARSE_WARNING
                });
            } else {
                reject(new Error(`Formatter exited with code ${code}. stderr: ${stderr.trim()}`));
            }
        });

        // Write the SQL to stdin and close the stream so the formatter knows input is done
        proc.stdin.write(sql, 'utf8');
        proc.stdin.end();
    });
}

// ---------------------------------------------------------------------------
// Executable resolution
// ---------------------------------------------------------------------------

/**
 * Resolves the path to the SqlFormatter binary.
 *
 * Resolution order:
 *   1. Explicit path from settings (`rightWaySqlFormatter.executablePath`)
 *   2. A `SqlFormatter` binary bundled next to the extension's out/ directory
 *      (useful for local dev where you've built the .NET project)
 *   3. `SqlFormatter` on PATH
 *
 * Returns null if nothing is found.
 */
function resolveExecutablePath(): string | null {
    const config = vscode.workspace.getConfiguration('rightWaySqlFormatter');
    const explicit = config.get<string>('executablePath', '').trim();

    if (explicit) {
        if (fs.existsSync(explicit)) {
            return explicit;
        }
        // User set a path but it doesn't exist — warn them rather than silently falling through
        vscode.window.showWarningMessage(
            `rightWaySqlFormatter.executablePath points to "${explicit}" which does not exist. Falling back to auto-detect.`
        );
    }


    // Look for a binary bundled for the current platform/arch
    // Platform/arch to RID mapping
    function getCurrentRid(): string {
        const plat = process.platform;
        const arch = process.arch;
        if (plat === 'darwin') return arch === 'arm64' ? 'osx-arm64' : 'osx-x64';
        if (plat === 'win32')  return arch === 'arm64' ? 'win-arm64' : 'win-x64';
        return arch === 'arm64' ? 'linux-arm64' : 'linux-x64';
    }
    const extDir = path.dirname(path.dirname(__filename)); // out/ -> extension root
    const rid = getCurrentRid();
    const exeName = process.platform === 'win32' ? 'SqlFormatter.exe' : 'SqlFormatter';
    const bundledBin = path.join(extDir, 'bin', rid, exeName);
    if (fs.existsSync(bundledBin)) {
        return bundledBin;
    }
    // Fallback for devs who may have a flat bin/SqlFormatter
    const legacyBin = path.join(extDir, 'bin', exeName);
    if (fs.existsSync(legacyBin)) {
        return legacyBin;
    }

    // Fall back to PATH — `which SqlFormatter` equivalent
    // cp.execFileSync would throw if not found, so we check quietly
    try {
        const result = cp.execFileSync(
            process.platform === 'win32' ? 'where' : 'which',
            ['SqlFormatter'],
            { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }
        ).trim().split('\n')[0].trim();
        if (result && fs.existsSync(result)) {
            return result;
        }
    } catch {
        // Not on PATH — that's fine, fall through to null
    }

    return null;
}

// ---------------------------------------------------------------------------
// CLI argument builder
// ---------------------------------------------------------------------------

/**
 * Reads the extension settings and converts them to CLI flags for SqlFormatter.
 * Each flag maps 1:1 to a --flag in the System.CommandLine-based CLI.
 */
function buildArgs(): string[] {
    const cfg = vscode.workspace.getConfiguration('rightWaySqlFormatter');

    const args: string[] = [];

    // Helper to add a boolean flag only when it differs from the CLI default,
    // using --flag=true / --flag=false syntax understood by System.CommandLine.
    const bool = (key: string, flag: string, defaultVal: boolean): void => {
        const val = cfg.get<boolean>(key, defaultVal);
        if (val !== defaultVal) {
            args.push(`--${flag}=${val}`);
        }
    };

    // Helper for string/number flags
    const str = (key: string, flag: string, defaultVal: string): void => {
        const val = cfg.get<string>(key, defaultVal);
        if (val !== defaultVal) {
            args.push(`--${flag}=${val}`);
        }
    };

    const num = (key: string, flag: string, defaultVal: number): void => {
        const val = cfg.get<number>(key, defaultVal);
        if (val !== defaultVal) {
            args.push(`--${flag}=${val}`);
        }
    };

    // Indent string: build literal value from enum + size
    const indentType = cfg.get<string>('indentString', 'space');
    const indentSize = cfg.get<number>('indentSize', 4);
    const indentStr = indentType === 'tab' ? '\t' : ' '.repeat(indentSize);
    args.push(`--indent-string=${indentStr}`);
    num('maxLineWidth',            'max-line-width',             999);
    num('newStatementLineBreaks',  'statement-breaks',           2);
    num('newClauseLineBreaks',     'clause-breaks',              1);
    bool('uppercaseKeywords',      'uppercase-keywords',         true);
    bool('standardizeKeywords',    'standardize-keywords',       true);
    bool('expandCommaLists',       'expand-comma-lists',         true);
    bool('selectFirstColumnOnNewLine', 'select-first-column-newline', false);
    bool('expandInLists',          'expand-in-lists',            true);
    bool('trailingCommas',         'trailing-commas',            false);
    bool('expandBooleanExpressions','expand-boolean',            true);
    bool('expandCaseStatements',   'expand-case',                true);
    bool('expandBetweenConditions','expand-between',             true);
    bool('breakJoinOnSections',    'break-join-on-sections',     false);

    // Task 2: Column alias style
    const aliasStyleVal = cfg.get<string>('columnAliasStyle', 'as');
    if (aliasStyleVal !== 'as') {
        args.push(`--alias-style=${aliasStyleVal}`);
    }

    // Task 3: Column alignment
    bool('alignColumnDefinitions',   'align-columns',              false);

    // Task 4: JOIN/WHERE alignment
    bool('indentJoinOnClause',       'indent-join-on',             false);
    bool('indentWhereAndOrConditions','indent-where-and-or',       false);

    // Task 5: DDL formatting
    bool('alignColumnDefinitionsInDDL', 'align-ddl-columns',      false);
    bool('ddlConstraintsOnNewLine',   'ddl-constraints-newline',  false);

    // Task 6: Table join alignment
    bool('alignTableJoins',           'align-table-joins',        false);

    // Task 7: Column always has alias
    bool('columnAlwaysHasAlias',      'column-always-has-alias',  false);

    return args;
}
