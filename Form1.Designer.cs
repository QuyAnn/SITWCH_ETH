// ============================================================================
// Form1.Designer.cs – Auto-generated UI layout
//
// Control inventory (referenced by Form1.cs):
//   txtEthGateway   – Ethernet Gateway input
//   txtWifiGateway  – WiFi Gateway input
//   txtNetworks     – Multiline TextBox for CIDR networks (one per line)
//   txtIPs          – Multiline TextBox for individual host IPs (one per line)
//   btnApply        – Apply routes
//   btnSaveConfig   – Save configuration to JSON
//   btnLoadConfig   – Load configuration from JSON
//   btnClearLog     – Clear the log panel
//   txtLog          – Read-only multiline log output
// ============================================================================

namespace SITWCH_ETH;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    // ── Gateway group ─────────────────────────────────────────────────────
    private GroupBox grpGateway;
    private Label    lblEthGateway;
    private TextBox  txtEthGateway;
    private Label    lblWifiGateway;
    private TextBox  txtWifiGateway;

    // ── Routes group ──────────────────────────────────────────────────────
    private GroupBox grpRoutes;
    private Label    lblNetworks;
    private TextBox  txtNetworks;
    private Label    lblNetworksHint;
    private Label    lblIPs;
    private TextBox  txtIPs;
    private Label    lblIPsHint;

    // ── Buttons ───────────────────────────────────────────────────────────
    private Panel    pnlButtons;
    private Button   btnApply;
    private Button   btnSaveConfig;
    private Button   btnLoadConfig;
    private Button   btnClearLog;

    // ── Log group ─────────────────────────────────────────────────────────
    private GroupBox grpLog;
    private TextBox  txtLog;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        // ── Form ──────────────────────────────────────────────────────────
        Text            = "SITWCH_ETH – Network Route Manager";
        Size            = new Size(840, 680);
        MinimumSize     = new Size(700, 580);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        Font            = new Font("Segoe UI", 9F);

        // ── grpGateway ────────────────────────────────────────────────────
        grpGateway = new GroupBox
        {
            Text     = "⚙ Gateway Configuration",
            Location = new Point(12, 10),
            Size     = new Size(800, 80),
            Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
        };

        lblEthGateway = new Label
        {
            Text     = "Ethernet Gateway:",
            Location = new Point(12, 28),
            Size     = new Size(130, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Font     = new Font("Segoe UI", 9F),
        };

        txtEthGateway = new TextBox
        {
            Name        = "txtEthGateway",
            Location    = new Point(148, 26),
            Size        = new Size(190, 23),
            PlaceholderText = "e.g. 10.21.99.1",
            Font        = new Font("Segoe UI", 9F),
        };

        lblWifiGateway = new Label
        {
            Text      = "WiFi Gateway:",
            Location  = new Point(370, 28),
            Size      = new Size(110, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Font      = new Font("Segoe UI", 9F),
        };

        txtWifiGateway = new TextBox
        {
            Name        = "txtWifiGateway",
            Location    = new Point(486, 26),
            Size        = new Size(190, 23),
            PlaceholderText = "e.g. 192.168.5.1",
            Font        = new Font("Segoe UI", 9F),
        };

        grpGateway.Controls.AddRange([lblEthGateway, txtEthGateway,
                                       lblWifiGateway, txtWifiGateway]);

        // ── grpRoutes ─────────────────────────────────────────────────────
        grpRoutes = new GroupBox
        {
            Text     = "🔀 Route Entries",
            Location = new Point(12, 100),
            Size     = new Size(800, 260),
            Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
        };

        lblNetworks = new Label
        {
            Text      = "Networks (CIDR)  → Ethernet:",
            Location  = new Point(12, 22),
            Size      = new Size(200, 20),
            Font      = new Font("Segoe UI", 9F),
        };

        txtNetworks = new TextBox
        {
            Name        = "txtNetworks",
            Location    = new Point(12, 45),
            Size        = new Size(375, 195),
            Multiline   = true,
            ScrollBars  = ScrollBars.Vertical,
            AcceptsReturn = true,
            Font        = new Font("Consolas", 9F),
            Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
        };

        lblNetworksHint = new Label
        {
            Text      = "One CIDR per line, e.g.  10.53.0.0/16",
            Location  = new Point(12, 243),
            Size      = new Size(370, 16),
            ForeColor = SystemColors.GrayText,
            Font      = new Font("Segoe UI", 8F, FontStyle.Italic),
        };

        lblIPs = new Label
        {
            Text      = "Individual IPs  → Ethernet:",
            Location  = new Point(405, 22),
            Size      = new Size(200, 20),
            Font      = new Font("Segoe UI", 9F),
        };

        txtIPs = new TextBox
        {
            Name        = "txtIPs",
            Location    = new Point(405, 45),
            Size        = new Size(375, 195),
            Multiline   = true,
            ScrollBars  = ScrollBars.Vertical,
            AcceptsReturn = true,
            Font        = new Font("Consolas", 9F),
            Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };

        lblIPsHint = new Label
        {
            Text      = "One IP per line, e.g.  10.53.118.120",
            Location  = new Point(405, 243),
            Size      = new Size(375, 16),
            ForeColor = SystemColors.GrayText,
            Font      = new Font("Segoe UI", 8F, FontStyle.Italic),
        };

        grpRoutes.Controls.AddRange([lblNetworks, txtNetworks, lblNetworksHint,
                                      lblIPs,      txtIPs,      lblIPsHint]);

        // ── pnlButtons ────────────────────────────────────────────────────
        pnlButtons = new Panel
        {
            Location = new Point(12, 370),
            Size     = new Size(800, 44),
            Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        btnApply = new Button
        {
            Name     = "btnApply",
            Text     = "▶  Apply Routes",
            Location = new Point(0, 4),
            Size     = new Size(140, 36),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor   = Cursors.Hand,
        };
        btnApply.FlatAppearance.BorderSize = 0;

        btnSaveConfig = new Button
        {
            Name     = "btnSaveConfig",
            Text     = "💾  Save Config",
            Location = new Point(150, 4),
            Size     = new Size(140, 36),
            FlatStyle = FlatStyle.Flat,
            Font     = new Font("Segoe UI", 9F),
            Cursor   = Cursors.Hand,
        };

        btnLoadConfig = new Button
        {
            Name     = "btnLoadConfig",
            Text     = "📂  Load Config",
            Location = new Point(300, 4),
            Size     = new Size(140, 36),
            FlatStyle = FlatStyle.Flat,
            Font     = new Font("Segoe UI", 9F),
            Cursor   = Cursors.Hand,
        };

        btnClearLog = new Button
        {
            Name     = "btnClearLog",
            Text     = "🗑  Clear Log",
            Location = new Point(450, 4),
            Size     = new Size(120, 36),
            FlatStyle = FlatStyle.Flat,
            Font     = new Font("Segoe UI", 9F),
            Cursor   = Cursors.Hand,
        };

        pnlButtons.Controls.AddRange([btnApply, btnSaveConfig, btnLoadConfig, btnClearLog]);

        // ── grpLog ────────────────────────────────────────────────────────
        grpLog = new GroupBox
        {
            Text     = "📋 Log",
            Location = new Point(12, 424),
            Size     = new Size(800, 198),
            Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
        };

        txtLog = new TextBox
        {
            Name       = "txtLog",
            Location   = new Point(8, 22),
            Size       = new Size(782, 166),
            Multiline  = true,
            ReadOnly   = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor  = Color.FromArgb(30, 30, 30),
            ForeColor  = Color.FromArgb(180, 230, 180),
            Font       = new Font("Consolas", 8.5F),
            Anchor     = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };

        grpLog.Controls.Add(txtLog);

        // ── Wire up button click handlers ─────────────────────────────────
        btnApply.Click      += btnApply_Click;
        btnSaveConfig.Click += btnSaveConfig_Click;
        btnLoadConfig.Click += btnLoadConfig_Click;
        btnClearLog.Click   += btnClearLog_Click;

        // ── Add all top-level controls to the form ────────────────────────
        Controls.AddRange([grpGateway, grpRoutes, pnlButtons, grpLog]);
    }
}
