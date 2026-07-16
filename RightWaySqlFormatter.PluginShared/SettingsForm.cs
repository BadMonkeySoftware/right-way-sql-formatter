using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterPluginShared
{
    /// <summary>
    /// Formatting-options dialog for the SSMS/VS plugin. Presents each formatter
    /// option with an appropriate control (checkbox / number spinner / dropdown)
    /// instead of a raw serialized string, so it can't be mistyped. Controls are
    /// driven by the descriptor tables below and bound to a
    /// <see cref="TSqlStandardFormatterOptions"/> via reflection, so the core
    /// options class needs no UI-specific attributes.
    /// </summary>
    public class SettingsForm : Form
    {
        public delegate string FixHotkeyDefault(string rawDefault);

        private readonly ISqlSettings _settings;
        private readonly FixHotkeyDefault? _fixHotkeyDefault;
        private readonly bool _showHotkey;

        private readonly ToolTip _tips = new ToolTip { AutoPopDelay = 20000, InitialDelay = 400, ReshowDelay = 100 };
        private readonly Dictionary<string, CheckBox> _checks = new Dictionary<string, CheckBox>();
        private readonly Dictionary<string, NumericUpDown> _nums = new Dictionary<string, NumericUpDown>();
        private ComboBox _indentKind = null!;
        private NumericUpDown _indentCount = null!;
        private ComboBox _aliasStyle = null!;
        private TextBox _txtHotkey = null!;

        // ---- option descriptors (friendly label + tooltip per formatter property) ----
        private sealed class Opt
        {
            public readonly string Name;
            public readonly string Label;
            public readonly string Tip;
            public readonly int Min;
            public readonly int Max;
            public Opt(string name, string label, string tip, int min = 0, int max = 0)
            {
                Name = name; Label = label; Tip = tip; Min = min; Max = max;
            }
        }

        private static readonly Opt[] NumberOptions =
        {
            new Opt("SpacesPerTab", "Tab width (spaces, for alignment)", "How many columns a tab occupies when the formatter computes alignment and line width.", 1, 16),
            new Opt("MaxLineWidth", "Max line width", "Lines longer than this may be wrapped. 999 is effectively unlimited.", 1, 9999),
            new Opt("NewClauseLineBreaks", "Line breaks before each clause", "Number of line breaks inserted before a new clause (FROM, WHERE, GROUP BY, ...).", 0, 5),
            new Opt("NewStatementLineBreaks", "Line breaks before each statement", "Number of line breaks inserted before a new statement.", 0, 5),
        };

        private static readonly (string Title, Opt[] Items)[] CheckGroups =
        {
            ("Comma lists", new[]
            {
                new Opt("ExpandCommaLists", "Put each list item on its own line", "Expand comma-separated lists (SELECT columns, GROUP BY, ...) one item per line."),
                new Opt("TrailingCommas", "Trailing commas (comma at end of line)", "Place the comma after each item instead of before the next item."),
                new Opt("SpaceAfterExpandedComma", "Space after comma", "Add a space after the comma in expanded lists."),
                new Opt("ExpandInLists", "Expand IN (...) value lists", "Break long IN (...) value lists across multiple lines."),
                new Opt("SelectFirstColumnOnNewLine", "First SELECT column on its own line", "Break the first column onto a new line instead of keeping it beside SELECT."),
            }),
            ("Expansion", new[]
            {
                new Opt("ExpandBooleanExpressions", "Expand AND / OR expressions", "Put AND/OR boolean expressions on separate lines."),
                new Opt("ExpandBetweenConditions", "Expand BETWEEN conditions", "Put the two halves of BETWEEN ... AND ... on separate lines."),
                new Opt("ExpandCaseStatements", "Expand CASE statements", "Put WHEN / THEN / ELSE of a CASE on separate lines."),
            }),
            ("Keywords", new[]
            {
                new Opt("UppercaseKeywords", "Uppercase keywords", "Render T-SQL keywords in UPPERCASE."),
                new Opt("KeywordStandardization", "Standardize keyword synonyms", "Rewrite legacy keyword synonyms to their standard form."),
            }),
            ("Column aliases", new[]
            {
                new Opt("ColumnAlwaysHasAlias", "Give every column an alias", "Add an explicit alias to every column in a SELECT list."),
                new Opt("AlignColumnDefinitions", "Align column aliases in SELECT", "Pad columns so the alias operator lines up vertically — the AS keyword, or the = sign when the alias style is 'alias = col'."),
            }),
            ("Joins & conditions", new[]
            {
                new Opt("BreakJoinOnSections", "Break JOIN ... ON onto a new line", "Put the ON condition of a JOIN on its own line."),
                new Opt("IndentJoinOnClause", "Indent the ON clause", "Indent the ON clause an extra level under the JOIN."),
                new Opt("IndentWhereAndOrConditions", "Indent WHERE AND/OR conditions", "Align AND/OR conditions under the first WHERE condition."),
                new Opt("AlignTableJoins", "Align FROM / JOIN tables", "Vertically align table names, aliases and ON conditions across joins."),
                new Opt("AlignTableJoinsAddAliases", "…and add missing table aliases", "When aligning joins, also add an alias (from the table name) to tables that have none."),
            }),
            ("DDL (CREATE TABLE)", new[]
            {
                new Opt("AlignColumnDefinitionsInDDL", "Align column definitions", "Line up column name, data type, nullability and constraints into vertical columns."),
                new Opt("DDLConstraintsOnNewLine", "Each constraint on its own line", "Put each column constraint on a separate line."),
            }),
            ("Other", new[]
            {
                new Opt("CompactRaiserror", "Keep RAISERROR on one line", "Do not expand the RAISERROR argument list."),
                new Opt("RemoveHarmlessBrackets", "Remove unnecessary [brackets]", "Strip square brackets from identifiers that provably don't need them."),
                new Opt("CompactSingleStatementBlocks", "Compact single-statement IF / WHILE", "Keep a single-statement IF/ELSE/WHILE body on the same line when it fits."),
            }),
        };

        // Fixed geometry — no AutoSize on containers (AutoSize + Dock children collapse).
        private const int GroupWidth = 470;
        private const int RowHeight = 26;
        private const int GroupHeaderH = 26;
        private const int GroupPadBottom = 12;

        public SettingsForm(ISqlSettings settings, Assembly callingAssembly, string? aboutDescription, FixHotkeyDefault? fixHotkeyDefault)
        {
            _settings = settings;
            _fixHotkeyDefault = fixHotkeyDefault;
            _showHotkey = fixHotkeyDefault != null;
            InitializeComponent(aboutDescription);
            LoadSettings();
        }

        private void InitializeComponent(string? aboutDescription)
        {
            Text = "Right Way SQL Formatter — Options";
            ClientSize = new Size(GroupWidth + 44, 700);
            MinimumSize = new Size(GroupWidth + 60, 420);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            var bottom = BuildBottomBar(aboutDescription);

            // Scrollable vertical stack of fixed-size option groups.
            var scroll = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12, 12, 12, 4),
            };
            scroll.Controls.Add(BuildIndentationGroup());
            scroll.Controls.Add(BuildAliasStyleGroup());
            foreach (var g in CheckGroups)
                scroll.Controls.Add(BuildCheckGroup(g.Title, g.Items));

            // Add the Dock=Fill panel BEFORE the Dock=Bottom bar so the bar doesn't overlap it.
            Controls.Add(scroll);
            Controls.Add(bottom);
        }

        private Panel BuildBottomBar(string? aboutDescription)
        {
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = _showHotkey ? 96 : 68, Padding = new Padding(12, 6, 12, 8) };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 34 };
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
            var btnDefaults = new Button { Text = "Restore Defaults", AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
            btnOk.Click += BtnOk_Click;
            btnDefaults.Click += (s, e) => Populate(new TSqlStandardFormatterOptions());
            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnOk);
            buttons.Controls.Add(btnDefaults);
            bottom.Controls.Add(buttons);

            if (_showHotkey)
            {
                var hotkeyPanel = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, Height = 28 };
                hotkeyPanel.Controls.Add(new Label { Text = "Hotkey:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
                _txtHotkey = new TextBox { Width = 220 };
                _tips.SetToolTip(_txtHotkey, "Keyboard shortcut used to invoke formatting in this host.");
                hotkeyPanel.Controls.Add(_txtHotkey);
                bottom.Controls.Add(hotkeyPanel);
            }
            else
            {
                _txtHotkey = new TextBox();
            }

            if (!string.IsNullOrEmpty(aboutDescription))
            {
                bottom.Controls.Add(new Label
                {
                    Text = aboutDescription,
                    Dock = DockStyle.Top,
                    Height = 30,
                    ForeColor = SystemColors.GrayText,
                });
            }

            AcceptButton = btnOk;
            CancelButton = btnCancel;
            return bottom;
        }

        private static GroupBox NewGroup(string title, int contentHeight)
        {
            return new GroupBox
            {
                Text = title.Replace("&", "&&"), // literal ampersand, not a mnemonic accelerator
                Width = GroupWidth,
                Height = GroupHeaderH + contentHeight + GroupPadBottom,
                Margin = new Padding(0, 0, 0, 10),
            };
        }

        private GroupBox BuildCheckGroup(string title, Opt[] items)
        {
            var group = NewGroup(title, items.Length * RowHeight);
            var inner = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8, 2, 4, 4) };
            foreach (var opt in items)
            {
                var cb = new CheckBox { Text = opt.Label, AutoSize = true, Margin = new Padding(2, 3, 2, 3) };
                _tips.SetToolTip(cb, opt.Tip);
                _checks[opt.Name] = cb;
                inner.Controls.Add(cb);
            }
            group.Controls.Add(inner);
            return group;
        }

        private const int LabelColWidth = 224;

        /// <summary>A label of fixed width followed by its control — so every row lines up.</summary>
        private static FlowLayoutPanel LabeledRow(string label, Control control)
        {
            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 3) };
            row.Controls.Add(new Label { Text = label, Width = LabelColWidth, Height = 22, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(2, 2, 6, 0) });
            control.Margin = new Padding(0, 1, 0, 1);
            row.Controls.Add(control);
            return row;
        }

        private GroupBox BuildIndentationGroup()
        {
            var group = NewGroup("Indentation & line width", (NumberOptions.Length + 1) * 29 + 10);
            var col = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8, 4, 4, 4) };

            _indentKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 88 };
            _indentKind.Items.AddRange(new object[] { "Spaces", "Tab" });
            _tips.SetToolTip(_indentKind, "Indent using spaces or a tab character.");
            _indentCount = new NumericUpDown { Minimum = 1, Maximum = 16, Width = 54, Margin = new Padding(8, 0, 0, 0) };
            _tips.SetToolTip(_indentCount, "How many spaces make up one indent level.");
            _indentKind.SelectedIndexChanged += (s, e) => _indentCount.Enabled = _indentKind.SelectedIndex == 0;
            var indentControls = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
            indentControls.Controls.Add(_indentKind);
            indentControls.Controls.Add(_indentCount);
            col.Controls.Add(LabeledRow("Indent using", indentControls));

            foreach (var opt in NumberOptions)
            {
                var num = new NumericUpDown { Minimum = opt.Min, Maximum = opt.Max, Width = 70 };
                _tips.SetToolTip(num, opt.Tip);
                _nums[opt.Name] = num;
                col.Controls.Add(LabeledRow(opt.Label, num));
            }

            group.Controls.Add(col);
            return group;
        }

        private GroupBox BuildAliasStyleGroup()
        {
            var group = NewGroup("Column alias style", RowHeight + 10);
            var col = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8, 4, 4, 4) };
            _aliasStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
            _aliasStyle.Items.AddRange(new object[] { "col AS alias  (AS keyword)", "alias = col  (equals sign)" });
            _tips.SetToolTip(_aliasStyle, "How SELECT column aliases are written. The formatter preserves the user's style unless you change this.");
            col.Controls.Add(LabeledRow("Write column aliases as", _aliasStyle));
            group.Controls.Add(col);
            return group;
        }

        private void LoadSettings()
        {
            Populate(new TSqlStandardFormatterOptions(_settings.OptionsSerialized ?? string.Empty));
            if (_showHotkey)
                _txtHotkey.Text = _settings.Hotkey ?? string.Empty;
        }

        /// <summary>Push a formatter-options object into the controls.</summary>
        private void Populate(TSqlStandardFormatterOptions o)
        {
            foreach (var kvp in _checks)
                kvp.Value.Checked = (bool)GetProp(kvp.Key).GetValue(o, null)!;

            foreach (var kvp in _nums)
                kvp.Value.Value = Clamp(kvp.Value, Convert.ToInt32(GetProp(kvp.Key).GetValue(o, null)));

            string indent = o.IndentString ?? "    ";
            bool isTab = indent.IndexOf('\t') >= 0;
            _indentKind.SelectedIndex = isTab ? 1 : 0;
            _indentCount.Value = Clamp(_indentCount, indent.Length < 1 ? 1 : indent.Length);
            _indentCount.Enabled = !isTab;

            _aliasStyle.SelectedIndex = o.ColumnAliasStyle == ColumnAliasStyle.EqualSign ? 1 : 0;
        }

        /// <summary>Read the controls back into a formatter-options object.</summary>
        private TSqlStandardFormatterOptions Harvest()
        {
            var o = new TSqlStandardFormatterOptions();

            foreach (var kvp in _checks)
                GetProp(kvp.Key).SetValue(o, kvp.Value.Checked, null);

            foreach (var kvp in _nums)
                GetProp(kvp.Key).SetValue(o, (int)kvp.Value.Value, null);

            o.IndentString = _indentKind.SelectedIndex == 1 ? "\t" : new string(' ', (int)_indentCount.Value);
            o.ColumnAliasStyle = _aliasStyle.SelectedIndex == 1 ? ColumnAliasStyle.EqualSign : ColumnAliasStyle.AsKeyword;

            return o;
        }

        private static PropertyInfo GetProp(string name)
        {
            return typeof(TSqlStandardFormatterOptions).GetProperty(name)
                   ?? throw new InvalidOperationException("Unknown formatter option: " + name);
        }

        private static decimal Clamp(NumericUpDown num, int value)
        {
            if (value < num.Minimum) return num.Minimum;
            if (value > num.Maximum) return num.Maximum;
            return value;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            try
            {
                var options = Harvest();
                _settings.OptionsSerialized = options.ToSerializedString();
                if (_showHotkey)
                    _settings.Hotkey = _txtHotkey.Text;
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save options: " + ex.Message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _tips?.Dispose();
            base.Dispose(disposing);
        }
    }
}
