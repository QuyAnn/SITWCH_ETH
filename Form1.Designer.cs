// ============================================================================
// Form1.Designer.cs – Auto-generated UI layout
//
// Control inventory (referenced by Form1.cs):
//   Tab 1 – Route Manager:
//     txtEthGateway   – Ethernet Gateway input
//     txtWifiGateway  – WiFi Gateway input
//     txtNetworks     – Multiline TextBox for CIDR networks (one per line)
//     txtIPs          – Multiline TextBox for individual host IPs (one per line)
//     btnApply        – Apply routes
//     btnSaveConfig   – Save configuration to JSON
//     btnLoadConfig   – Load configuration from JSON
//     btnClearLog     – Clear the log panel
//     txtLog          – Read-only multiline log output
//   Tab 2 – Network Info:
//     dgvNetworkInfo  – DataGridView of active network adapters
//     btnRefreshNetwork – Reload adapter list
//     btnCopyIP       – Copy selected row's IPv4 to clipboard
//     lblNetworkStatus – Status / last-refresh timestamp
// ============================================================================

namespace SITWCH_ETH;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    // ── Tab host ──────────────────────────────────────────────────────────
    private TabControl tabMain;
    private TabPage    tabPageRouteManager;
    private TabPage    tabPageNetworkInfo;

    // ── Gateway group (Tab 1) ─────────────────────────────────────────────
    private GroupBox grpGateway;
    private Label    lblEthGateway;
    private TextBox  txtEthGateway;
    private Label    lblWifiGateway;
    private TextBox  txtWifiGateway;

    // ── Routes group (Tab 1) ──────────────────────────────────────────────
    private GroupBox grpRoutes;
    private Label    lblNetworks;
    private TextBox  txtNetworks;
    private Label    lblNetworksHint;
    private Label    lblIPs;
    private TextBox  txtIPs;
    private Label    lblIPsHint;

    // ── Buttons (Tab 1) ───────────────────────────────────────────────────
    private Panel    pnlButtons;
    private Button   btnApply;
    private Button   btnSaveConfig;
    private Button   btnLoadConfig;
    private Button   btnClearLog;

    // ── Log group (Tab 1) ─────────────────────────────────────────────────
    private GroupBox grpLog;
    private TextBox  txtLog;

    // ── Network Info controls (Tab 2) ─────────────────────────────────────
    private Panel        pnlNetworkButtons;
    private Button       btnRefreshNetwork;
    private Button       btnCopyIP;
    private Label        lblNetworkStatus;
    private DataGridView dgvNetworkInfo;

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
        Size            = new Size(900, 720);
        MinimumSize     = new Size(760, 620);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        Font            = new Font("Segoe UI", 9F);

        // ══════════════════════════════════════════════════════════════════
        // TAB CONTROL
        // ══════════════════════════════════════════════════════════════════
        tabMain = new TabControl
        {
            Dock     = DockStyle.Fill,
            Font     = new Font("Segoe UI", 9.5F),
            Padding  = new Point(14, 4),
        };

        tabPageRouteManager = new TabPage
        {
            Text    = "🔀  Route Manager",
            Padding = new Padding(4),
        };

        tabPageNetworkInfo = new TabPage
        {
            Text    = "🌐  Network Info",
            Padding = new Padding(4),
        };

        tabMain.TabPages.AddRange([tabPageRouteManager, tabPageNetworkInfo]);

        // ══════════════════════════════════════════════════════════════════
        // TAB 1 – ROUTE MANAGER  (all existing controls, anchored inside the tab page)
        // ══════════════════════════════════════════════════════════════════

        // ── grpGateway ────────────────────────────────────────────────────
        grpGateway = new GroupBox
        {
            Text     = "⚙ Gateway Configuration",
            Location = new Point(12, 10),
            Size     = new Size(856, 80),
            Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
        };

        lblEthGateway = new Label
        {
            Text      = "Ethernet Gateway:",
            Location  = new Point(12, 28),
            Size      = new Size(130, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Font      = new Font("Segoe UI", 9F),
        };

        txtEthGateway = new TextBox
        {
            Name            = "txtEthGateway",
            Location        = new Point(148, 26),
            Size            = new Size(190, 23),
            PlaceholderText = "e.g. 10.21.99.1",
            Font            = new Font("Segoe UI", 9F),
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
            Name            = "txtWifiGateway",
            Location        = new Point(486, 26),
            Size            = new Size(190, 23),
            PlaceholderText = "e.g. 192.168.5.1",
            Font            = new Font("Segoe UI", 9F),
        };

        grpGateway.Controls.AddRange([lblEthGateway, txtEthGateway,
                                       lblWifiGateway, txtWifiGateway]);

        // ── grpRoutes ─────────────────────────────────────────────────────
        grpRoutes = new GroupBox
        {
            Text     = "🔀 Route Entries",
            Location = new Point(12, 100),
            Size     = new Size(856, 260),
            Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
        };

        lblNetworks = new Label
        {
            Text     = "Networks (CIDR)  → Ethernet:",
            Location = new Point(12, 22),
            Size     = new Size(200, 20),
            Font     = new Font("Segoe UI", 9F),
        };

        txtNetworks = new TextBox
        {
            Name          = "txtNetworks",
            Location      = new Point(12, 45),
            Size          = new Size(408, 195),
            Multiline     = true,
            ScrollBars    = ScrollBars.Vertical,
            AcceptsReturn = true,
            Font          = new Font("Consolas", 9F),
            Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
        };

        lblNetworksHint = new Label
        {
            Text      = "One CIDR per line, e.g.  10.53.0.0/16",
            Location  = new Point(12, 243),
            Size      = new Size(390, 16),
            ForeColor = SystemColors.GrayText,
            Font      = new Font("Segoe UI", 8F, FontStyle.Italic),
        };

        lblIPs = new Label
        {
            Text     = "Individual IPs  → Ethernet:",
            Location = new Point(432, 22),
            Size     = new Size(200, 20),
            Font     = new Font("Segoe UI", 9F),
        };

        txtIPs = new TextBox
        {
            Name          = "txtIPs",
            Location      = new Point(432, 45),
            Size          = new Size(408, 195),
            Multiline     = true,
            ScrollBars    = ScrollBars.Vertical,
            AcceptsReturn = true,
            Font          = new Font("Consolas", 9F),
            Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };

        lblIPsHint = new Label
        {
            Text      = "One IP per line, e.g.  10.53.118.120",
            Location  = new Point(432, 243),
            Size      = new Size(390, 16),
            ForeColor = SystemColors.GrayText,
            Font      = new Font("Segoe UI", 8F, FontStyle.Italic),
        };

        grpRoutes.Controls.AddRange([lblNetworks, txtNetworks, lblNetworksHint,
                                      lblIPs,      txtIPs,      lblIPsHint]);

        // ── pnlButtons ────────────────────────────────────────────────────
        pnlButtons = new Panel
        {
            Location = new Point(12, 370),
            Size     = new Size(856, 44),
            Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        btnApply = new Button
        {
            Name      = "btnApply",
            Text      = "▶  Apply Routes",
            Location  = new Point(0, 4),
            Size      = new Size(140, 36),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor    = Cursors.Hand,
        };
        btnApply.FlatAppearance.BorderSize = 0;

        btnSaveConfig = new Button
        {
            Name      = "btnSaveConfig",
            Text      = "💾  Save Config",
            Location  = new Point(150, 4),
            Size      = new Size(140, 36),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9F),
            Cursor    = Cursors.Hand,
        };

        btnLoadConfig = new Button
        {
            Name      = "btnLoadConfig",
            Text      = "📂  Load Config",
            Location  = new Point(300, 4),
            Size      = new Size(140, 36),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9F),
            Cursor    = Cursors.Hand,
        };

        btnClearLog = new Button
        {
            Name      = "btnClearLog",
            Text      = "🗑  Clear Log",
            Location  = new Point(450, 4),
            Size      = new Size(120, 36),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9F),
            Cursor    = Cursors.Hand,
        };

        pnlButtons.Controls.AddRange([btnApply, btnSaveConfig, btnLoadConfig, btnClearLog]);

        // ── grpLog ────────────────────────────────────────────────────────
        grpLog = new GroupBox
        {
            Text   = "📋 Log",
            Location = new Point(12, 424),
            Size   = new Size(856, 198),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Font   = new Font("Segoe UI", 9F, FontStyle.Bold),
        };

        txtLog = new TextBox
        {
            Name       = "txtLog",
            Location   = new Point(8, 22),
            Size       = new Size(838, 166),
            Multiline  = true,
            ReadOnly   = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor  = Color.FromArgb(30, 30, 30),
            ForeColor  = Color.FromArgb(180, 230, 180),
            Font       = new Font("Consolas", 8.5F),
            Anchor     = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };

        grpLog.Controls.Add(txtLog);

        // ── Wire up Tab 1 button click handlers ───────────────────────────
        btnApply.Click      += btnApply_Click;
        btnSaveConfig.Click += btnSaveConfig_Click;
        btnLoadConfig.Click += btnLoadConfig_Click;
        btnClearLog.Click   += btnClearLog_Click;

        // ── Add Tab 1 controls ────────────────────────────────────────────
        tabPageRouteManager.Controls.AddRange([grpGateway, grpRoutes, pnlButtons, grpLog]);

        // ══════════════════════════════════════════════════════════════════
        // TAB 2 – NETWORK INFO
        // ══════════════════════════════════════════════════════════════════

        // ── pnlNetworkButtons ─────────────────────────────────────────────
        pnlNetworkButtons = new Panel
        {
            Location = new Point(12, 10),
            Size     = new Size(856, 44),
            Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        btnRefreshNetwork = new Button
        {
            Name      = "btnRefreshNetwork",
            Text      = "🔄  Refresh",
            Location  = new Point(0, 4),
            Size      = new Size(120, 36),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor    = Cursors.Hand,
        };
        btnRefreshNetwork.FlatAppearance.BorderSize = 0;

        btnCopyIP = new Button
        {
            Name      = "btnCopyIP",
            Text      = "📋  Copy IP",
            Location  = new Point(130, 4),
            Size      = new Size(120, 36),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9F),
            Cursor    = Cursors.Hand,
        };

        pnlNetworkButtons.Controls.AddRange([btnRefreshNetwork, btnCopyIP]);

        // ── lblNetworkStatus ──────────────────────────────────────────────
        lblNetworkStatus = new Label
        {
            Text      = "Press 🔄 Refresh to load network adapters.",
            Location  = new Point(12, 58),
            Size      = new Size(856, 22),
            Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = SystemColors.GrayText,
            Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
        };

        // ── dgvNetworkInfo ────────────────────────────────────────────────
        dgvNetworkInfo = new DataGridView
        {
            Name                  = "dgvNetworkInfo",
            Location              = new Point(12, 86),
            Size                  = new Size(856, 530),
            Anchor                = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ReadOnly              = true,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            MultiSelect           = false,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible     = false,
            AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor       = Color.White,
            BorderStyle           = BorderStyle.Fixed3D,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            Font                  = new Font("Segoe UI", 9F),
        };

        // Style the column headers
        dgvNetworkInfo.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgvNetworkInfo.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 80, 160);
        dgvNetworkInfo.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        dgvNetworkInfo.EnableHeadersVisualStyles               = false;

        // Alternating row color
        dgvNetworkInfo.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 248, 255);

        // ── Define columns ────────────────────────────────────────────────
        dgvNetworkInfo.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { Name = "colAdapterName",  HeaderText = "Adapter Name",       FillWeight = 18 },
            new DataGridViewTextBoxColumn { Name = "colStatus",       HeaderText = "Status",             FillWeight = 8  },
            new DataGridViewTextBoxColumn { Name = "colIPv4",         HeaderText = "IPv4 Address",       FillWeight = 13 },
            new DataGridViewTextBoxColumn { Name = "colSubnetMask",   HeaderText = "Subnet Mask",        FillWeight = 13 },
            new DataGridViewTextBoxColumn { Name = "colGateway",      HeaderText = "Default Gateway",    FillWeight = 13 },
            new DataGridViewTextBoxColumn { Name = "colDNS",          HeaderText = "DNS Servers",        FillWeight = 22 },
            new DataGridViewTextBoxColumn { Name = "colIfIndex",      HeaderText = "Interface Index",    FillWeight = 10 },
        ]);

        // ── Wire up Tab 2 button click handlers ───────────────────────────
        btnRefreshNetwork.Click += btnRefreshNetwork_Click;
        btnCopyIP.Click         += btnCopyIP_Click;

        // ── Add Tab 2 controls ────────────────────────────────────────────
        tabPageNetworkInfo.Controls.AddRange([pnlNetworkButtons, lblNetworkStatus, dgvNetworkInfo]);

        // ── Add TabControl to the form ────────────────────────────────────
        this.Load += Form1_Load;
        Controls.Add(tabMain);
    }
}
