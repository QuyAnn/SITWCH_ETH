using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SITWCH_ETH;

// ---------------------------------------------------------------------------
// Form1.cs – Network Route Manager
// Manages Windows routing table entries so that:
//   • Internal networks (e.g. 10.x.x.x) are routed via the Ethernet gateway
//   • Everything else (default route) goes via the WiFi gateway
//
// Requires Administrator rights (enforced in app.manifest).
// ---------------------------------------------------------------------------
public partial class Form1 : Form
{
    // Name of the JSON config file written next to the EXE.
    private const string DefaultConfigFileName = "config.json";

    // ───────────────────────────── Constructor ──────────────────────────────

    public Form1()
    {
        InitializeComponent();
    }

    // ─────────────────────────── Helper Methods ─────────────────────────────

    /// <summary>
    /// Appends a timestamped line to the log TextBox.
    /// Thread-safe – can be called from background threads.
    /// </summary>
    private void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";

        if (txtLog.InvokeRequired)
            txtLog.Invoke(() => AppendLog(line));
        else
            AppendLog(line);
    }

    private void AppendLog(string line)
    {
        txtLog.AppendText(line + Environment.NewLine);
        // Auto-scroll to the bottom
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }

    /// <summary>
    /// Converts a CIDR prefix length (0–32) to a dotted-decimal subnet mask.
    /// Example: 16 → "255.255.0.0"
    /// </summary>
    private static string CidrToSubnetMask(int prefix)
    {
        if (prefix < 0 || prefix > 32)
            throw new ArgumentOutOfRangeException(nameof(prefix), "Prefix length must be between 0 and 32.");

        // Build a 32-bit mask, then split into four octets.
        uint mask = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
        return $"{(mask >> 24) & 0xFF}.{(mask >> 16) & 0xFF}.{(mask >> 8) & 0xFF}.{mask & 0xFF}";
    }

    /// <summary>
    /// Runs an external command and returns its exit code and combined output.
    /// </summary>
    private static (int ExitCode, string Output) RunCommand(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName        = fileName,
            Arguments       = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow  = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start process: {fileName}");

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, (stdout + stderr).Trim());
    }

    /// <summary>
    /// Returns <c>true</c> if a route with the given destination already exists
    /// in the Windows routing table.
    /// Uses "route print destination" and looks for the destination
    /// at the beginning of a data line (after trimming whitespace).
    /// </summary>
    private bool RouteExists(string destination)
    {
        var (_, output) = RunCommand("route", $"print {destination}");

        // Data rows in the routing table start with the destination IP
        // (possibly padded with spaces). We trim each line and check the prefix.
        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line =>
            {
                string trimmed = line.TrimStart();
                // Match lines whose first token equals the destination
                return trimmed.StartsWith(destination + " ", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith(destination + "\t", StringComparison.OrdinalIgnoreCase);
            });
    }

    /// <summary>
    /// Deletes a route silently; logs a warning if it could not be removed
    /// (e.g. because it did not exist).
    /// </summary>
    private void DeleteRoute(string destination)
    {
        var (exitCode, output) = RunCommand("route", $"delete {destination}");
        if (exitCode != 0)
            Log($"  [WARN] Cannot delete route {destination}: {output}");
        else
            Log($"  [OK]   Deleted route {destination}");
    }

    /// <summary>
    /// Adds a route only if it does not already exist, preventing duplicates.
    /// </summary>
    private void AddRoute(string destination, string mask, string gateway, int metric)
    {
        if (RouteExists(destination))
        {
            Log($"  [SKIP] Route {destination} mask {mask} already exists – skipping.");
            return;
        }

        string args = $"add {destination} mask {mask} {gateway} metric {metric}";
        var (exitCode, output) = RunCommand("route", args);

        if (exitCode != 0)
            Log($"  [ERR]  route {args} → {output}");
        else
            Log($"  [OK]   route {args}");
    }

    // ─────────────────────────── Input Validation ───────────────────────────

    /// <summary>Returns <c>true</c> when the string is a valid IPv4 address.</summary>
    private static bool IsValidIp(string ip) =>
        IPAddress.TryParse(ip.Trim(), out IPAddress? addr)
        && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

    /// <summary>
    /// Returns <c>true</c> when the string is a valid IPv4 CIDR notation
    /// (e.g. "10.53.0.0/16").
    /// </summary>
    private static bool IsValidCidr(string cidr)
    {
        string[] parts = cidr.Trim().Split('/');
        return parts.Length == 2
            && IPAddress.TryParse(parts[0], out IPAddress? addr)
            && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && int.TryParse(parts[1], out int prefix)
            && prefix >= 0 && prefix <= 32;
    }

    // ──────────────────────────── Button Handlers ───────────────────────────

    /// <summary>
    /// Apply button: validate inputs, wipe old routes, add new ones.
    /// </summary>
    private void btnApply_Click(object sender, EventArgs e)
    {
        txtLog.Clear();
        Log("═══════════════ Apply Routes – Started ═══════════════");

        // ── Validate gateways ──────────────────────────────────────────────
        string ethGateway  = txtEthGateway.Text.Trim();
        string wifiGateway = txtWifiGateway.Text.Trim();

        if (!IsValidIp(ethGateway))
        {
            MessageBox.Show(
                "Ethernet Gateway không hợp lệ.\nVí dụ hợp lệ: 10.21.99.1",
                "Lỗi Validate", MessageBoxButtons.OK, MessageBoxIcon.Error);
            txtEthGateway.Focus();
            return;
        }

        if (!IsValidIp(wifiGateway))
        {
            MessageBox.Show(
                "WiFi Gateway không hợp lệ.\nVí dụ hợp lệ: 192.168.5.1",
                "Lỗi Validate", MessageBoxButtons.OK, MessageBoxIcon.Error);
            txtWifiGateway.Focus();
            return;
        }

        // ── Parse CIDR network list ────────────────────────────────────────
        var networks = new List<string>();
        foreach (string rawLine in txtNetworks.Lines)
        {
            string trimmed = rawLine.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (!IsValidCidr(trimmed))
            {
                Log($"  [WARN] Bỏ qua CIDR không hợp lệ: '{trimmed}'");
                continue;
            }
            networks.Add(trimmed);
        }

        // ── Parse individual IP list ───────────────────────────────────────
        var ips = new List<string>();
        foreach (string rawLine in txtIPs.Lines)
        {
            string trimmed = rawLine.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (!IsValidIp(trimmed))
            {
                Log($"  [WARN] Bỏ qua IP không hợp lệ: '{trimmed}'");
                continue;
            }
            ips.Add(trimmed);
        }

        // ── Execute route changes ──────────────────────────────────────────
        try
        {
            // Disable button while running to prevent re-entrancy
            btnApply.Enabled = false;
            Cursor = Cursors.WaitCursor;

            // Step 1 – Delete stale/conflicting routes
            Log("─── Bước 1: Xóa route cũ ───────────────────────────────");
            DeleteRoute("10.53.0.0");
            DeleteRoute("10.21.0.0");
            DeleteRoute("0.0.0.0");   // default route will be re-added later

            // Step 2 – Add routes for each CIDR network → Ethernet gateway
            Log("─── Bước 2: Thêm route mạng (CIDR) → Ethernet ─────────");
            foreach (string cidr in networks)
            {
                string[] parts  = cidr.Split('/');
                string network  = parts[0];
                int    prefix   = int.Parse(parts[1]);
                string mask     = CidrToSubnetMask(prefix);
                AddRoute(network, mask, ethGateway, metric: 5);
            }

            // Step 3 – Add host routes for individual IPs → Ethernet gateway
            Log("─── Bước 3: Thêm route IP riêng lẻ → Ethernet ─────────");
            foreach (string ip in ips)
            {
                AddRoute(ip, "255.255.255.255", ethGateway, metric: 5);
            }

            // Step 4 – Default route via WiFi gateway
            Log("─── Bước 4: Thêm default route → WiFi ─────────────────");
            AddRoute("0.0.0.0", "0.0.0.0", wifiGateway, metric: 1);

            Log("═══════════════ Apply Routes – Hoàn Tất ════════════════");
            MessageBox.Show(
                "Áp dụng route thành công!\nXem chi tiết trong Log phía dưới.",
                "Thành Công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log($"[EXCEPTION] {ex.Message}");
            MessageBox.Show(
                $"Đã xảy ra lỗi không mong muốn:\n{ex.Message}",
                "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnApply.Enabled = true;
            Cursor = Cursors.Default;
        }
    }

    /// <summary>
    /// Save Config button: serialize current form state to config.json.
    /// </summary>
    private void btnSaveConfig_Click(object sender, EventArgs e)
    {
        var config = new RouteConfig
        {
            EthGateway  = txtEthGateway.Text.Trim(),
            WifiGateway = txtWifiGateway.Text.Trim(),
            Networks    = txtNetworks.Lines
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrEmpty(l))
                            .ToArray(),
            IPs         = txtIPs.Lines
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrEmpty(l))
                            .ToArray(),
        };

        // Ask user where to save
        using var dialog = new SaveFileDialog
        {
            Filter       = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title        = "Lưu file cấu hình",
            FileName     = DefaultConfigFileName,
            DefaultExt   = "json",
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(dialog.FileName, json, Encoding.UTF8);

            Log($"Config đã lưu vào: {dialog.FileName}");
            MessageBox.Show(
                $"Đã lưu cấu hình vào:\n{dialog.FileName}",
                "Lưu Config", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log($"[ERR] Lưu config thất bại: {ex.Message}");
            MessageBox.Show(
                $"Không thể lưu config:\n{ex.Message}",
                "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Load Config button: deserialize a config.json and populate the form.
    /// </summary>
    private void btnLoadConfig_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter    = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title     = "Chọn file cấu hình",
            FileName  = DefaultConfigFileName,
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            string json = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            RouteConfig? config = JsonSerializer.Deserialize<RouteConfig>(json);

            if (config is null)
            {
                MessageBox.Show(
                    "File config không hợp lệ hoặc rỗng.",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            txtEthGateway.Text  = config.EthGateway  ?? string.Empty;
            txtWifiGateway.Text = config.WifiGateway ?? string.Empty;
            txtNetworks.Lines   = config.Networks    ?? [];
            txtIPs.Lines        = config.IPs         ?? [];

            Log($"Config đã load từ: {dialog.FileName}");
        }
        catch (JsonException jex)
        {
            Log($"[ERR] JSON không hợp lệ: {jex.Message}");
            MessageBox.Show(
                $"File không đúng định dạng JSON:\n{jex.Message}",
                "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            Log($"[ERR] Load config thất bại: {ex.Message}");
            MessageBox.Show(
                $"Không thể load config:\n{ex.Message}",
                "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>Clear Log button: wipes the log TextBox.</summary>
    private void btnClearLog_Click(object sender, EventArgs e) => txtLog.Clear();
}

// ---------------------------------------------------------------------------
// RouteConfig – JSON serialization model
// Matches the schema described in the requirements:
// {
//   "ethGateway":  "10.21.99.1",
//   "wifiGateway": "192.168.5.1",
//   "networks":    ["10.53.0.0/16"],
//   "ips":         ["10.53.118.120"]
// }
// ---------------------------------------------------------------------------
internal sealed class RouteConfig
{
    [JsonPropertyName("ethGateway")]
    public string? EthGateway { get; set; }

    [JsonPropertyName("wifiGateway")]
    public string? WifiGateway { get; set; }

    [JsonPropertyName("networks")]
    public string[]? Networks { get; set; }

    [JsonPropertyName("ips")]
    public string[]? IPs { get; set; }
}
