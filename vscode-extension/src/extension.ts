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

    // "Format Document" — replaces entire editor content with formatted SQL
    context.subscriptions.push(
        vscode.commands.registerCommand('rightWaySqlFormatter.formatDocument', () => {
            const editor = vscode.window.activeTextEditor;
            if (!editor) {
                vscode.window.showErrorMessage('No active editor.');
                return;
            }
            const fullRange = new vscode.Range(
                editor.document.positionAt(0),
                editor.document.positionAt(editor.document.getText().length)
            );
            formatRange(editor, fullRange);
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

    // Also register as a VS Code document formatter so it participates in
    // "Format Document" (⇧⌥F / Shift+Alt+F) for SQL files.
    context.subscriptions.push(
        vscode.languages.registerDocumentFormattingEditProvider('sql', {
            provideDocumentFormattingEdits(document: vscode.TextDocument): vscode.TextEdit[] {
                // Delegate to the async path; return empty here and apply via edit builder.
                // (The synchronous provider path is kept simple — full async handled by command.)
                return [];
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
 * Formats the text within `range` in the given `editor` by passing it through
 * the SqlFormatter CLI and replacing the range with the result.
 */
async function formatRange(editor: vscode.TextEditor, range: vscode.Range): Promise<void> {
    const inputSql = editor.document.getText(range);

    let formattedSql: string;
    try {
        formattedSql = await runFormatter(inputSql);
    } catch (err) {
        vscode.window.showErrorMessage(`SQL Formatter error: ${err instanceof Error ? err.message : String(err)}`);
        return;
    }

    // Apply the formatted result back into the editor
    await editor.edit(editBuilder => {
        editBuilder.replace(range, formattedSql);
    });
}

/**
 * Runs the SqlFormatter CLI with the current settings, piping `sql` in via
 * stdin and returning the formatted output from stdout.
 *
 * Throws if the process exits non-zero or cannot be found.
 */
function runFormatter(sql: string): Promise<string> {
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
            if (code !== 0) {
                reject(new Error(`Formatter exited with code ${code}. stderr: ${stderr.trim()}`));
            } else {
                // Strip trailing newline added by Console.WriteLine in the CLI
                resolve(stdout.replace(/\n$/, ''));
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
