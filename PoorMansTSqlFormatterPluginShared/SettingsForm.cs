using System;
using System.Reflection;
using System.Windows.Forms;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterPluginShared
{
    /// <summary>
    /// Simple settings dialog for the SSMS/VS plugin.
    /// Allows the user to view and edit the serialized formatter options and hotkey.
    /// </summary>
    public class SettingsForm : Form
    {
        public delegate string FixHotkeyDefault(string rawDefault);

        private readonly ISqlSettings _settings;
        private readonly FixHotkeyDefault? _fixHotkeyDefault;

        private TableLayoutPanel _layout = null!;
        private Label _lblOptions = null!;
        private TextBox _txtOptions = null!;
        private Label _lblHotkey = null!;
        private TextBox _txtHotkey = null!;
        private Label _lblAbout = null!;
        private Button _btnOk = null!;
        private Button _btnCancel = null!;

        public SettingsForm(ISqlSettings settings, Assembly callingAssembly, string? aboutDescription, FixHotkeyDefault? fixHotkeyDefault)
        {
            _settings = settings;
            _fixHotkeyDefault = fixHotkeyDefault;
            InitializeComponent(aboutDescription);
            LoadSettings();
        }

        private void InitializeComponent(string? aboutDescription)
        {
            Text = "SQL Formatter Settings";
            Width = 560;
            Height = 360;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            _layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 2, RowCount = 4 };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _lblOptions = new Label { Text = "Options (serialized):", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true };
            _txtOptions = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 120, ScrollBars = ScrollBars.Vertical };

            _lblHotkey = new Label { Text = "Hotkey:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true };
            _txtHotkey = new TextBox { Dock = DockStyle.Fill };

            _lblAbout = new Label
            {
                Text = aboutDescription ?? string.Empty,
                Dock = DockStyle.Fill,
                AutoSize = false,
                ForeColor = System.Drawing.SystemColors.GrayText
            };

            _btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Anchor = AnchorStyles.Right };
            _btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Right };

            _layout.Controls.Add(_lblOptions, 0, 0);
            _layout.Controls.Add(_txtOptions, 1, 0);
            _layout.Controls.Add(_lblHotkey, 0, 1);
            _layout.Controls.Add(_txtHotkey, 1, 1);
            _layout.Controls.Add(_lblAbout, 0, 2);
            _layout.SetColumnSpan(_lblAbout, 2);

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            btnPanel.Controls.Add(_btnCancel);
            btnPanel.Controls.Add(_btnOk);
            _layout.Controls.Add(btnPanel, 0, 3);
            _layout.SetColumnSpan(btnPanel, 2);

            Controls.Add(_layout);
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            _btnOk.Click += BtnOk_Click;
        }

        private void LoadSettings()
        {
            _txtOptions.Text = _settings.OptionsSerialized ?? string.Empty;
            _txtHotkey.Text = _settings.Hotkey ?? string.Empty;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            // Validate options by trying to parse them
            try
            {
                var options = new TSqlStandardFormatterOptions(_txtOptions.Text);
                _settings.OptionsSerialized = options.ToSerializedString(); // normalize
                _settings.Hotkey = _txtHotkey.Text;
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Invalid options: " + ex.Message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _layout?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
