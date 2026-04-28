using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
    /// Converts a CIDR route (e.g. "10.53.0.0/16") to its subnet mask.
    /// Returns an empty string when the CIDR is invalid.
    /// </summary>
    private static string CIDRToMask(string cidr)
    {
        try
        {
            string[] parts = cidr.Trim().Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int prefix))
                return string.Empty;

            return CidrToSubnetMask(prefix);
        }
        catch
        {
            return string.Empty;
        }
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
    /// Runs <c>route print</c> and returns the raw routing table output.
    /// Returns an empty string if the command fails so callers can fail safely.
    /// </summary>
    private string GetRouteTable()
    {
        try
        {
            var (exitCode, output) = RunCommand("route", "print");
            if (exitCode != 0)
                Log($"[WARN] route print failed: {output}");

            return output;
        }
        catch (Exception ex)
        {
            Log($"[WARN] GetRouteTable: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Converts a dotted-decimal subnet mask to CIDR prefix length.
    /// Returns -1 when the mask is invalid or non-contiguous.
    /// </summary>
    private static int MaskToCIDR(string mask)
    {
        try
        {
            if (!IPAddress.TryParse(mask.Trim(), out IPAddress? address)
                || address.AddressFamily != AddressFamily.InterNetwork)
            {
                return -1;
            }

            uint value = IPv4ToUInt32(address);
            bool zeroSeen = false;
            int prefix = 0;

            for (int bit = 31; bit >= 0; bit--)
            {
                bool isSet = ((value >> bit) & 1u) == 1u;
                if (isSet)
                {
                    if (zeroSeen)
                        return -1;

                    prefix++;
                }
                else
                {
                    zeroSeen = true;
                }
            }

            return prefix;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Returns active, non-loopback network adapters. Failures return an empty
    /// list so network auto-detection never blocks the form from loading.
    /// </summary>
    private List<NetworkInterface> GetActiveAdapters()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
                .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();
        }
        catch (Exception ex)
        {
            Log($"[WARN] GetActiveAdapters: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Finds the IPv4 gateway for Ethernet or WiFi adapters.
    /// When multiple adapters match, adapters carrying the default gateway are preferred.
    /// </summary>
    private string GetGatewayByType(IEnumerable<NetworkInterface> adapters, NetworkAdapterKind adapterKind)
    {
        try
        {
            List<NetworkInterface> candidates = GetAdaptersByType(adapters, adapterKind);

            var matches = candidates
                .Select(adapter => new
                {
                    Gateway = GetIPv4Gateway(adapter),
                    IsDefaultGateway = IsDefaultGatewayAdapter(adapter),
                })
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Gateway))
                .OrderByDescending(candidate => candidate.IsDefaultGateway)
                .ToList();

            string gateway = matches.FirstOrDefault()?.Gateway ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(gateway))
                return gateway;

            if (adapterKind == NetworkAdapterKind.Ethernet)
            {
                foreach (NetworkInterface adapter in candidates)
                {
                    string ipv4 = GetIPv4(adapter);
                    string guessedGateway = GuessGatewayFromIP(ipv4);
                    if (string.IsNullOrWhiteSpace(guessedGateway))
                        continue;

                    Log($"Gateway not found → guessed: {guessedGateway}");
                    return guessedGateway;
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Log($"[WARN] GetGatewayByType({adapterKind}): {ex.Message}");
            return string.Empty;
        }
    }

    private static List<NetworkInterface> GetAdaptersByType(
        IEnumerable<NetworkInterface> adapters,
        NetworkAdapterKind adapterKind)
    {
        List<NetworkInterface> adapterList = adapters.ToList();
        List<NetworkInterface> candidates = adapterList
            .Where(adapter => IsAdapterKind(adapter, adapterKind))
            .ToList();

        if (adapterKind == NetworkAdapterKind.Ethernet && candidates.Count == 0)
        {
            // Some Windows drivers report wired cards as vendor-specific
            // adapters instead of plain "Ethernet"; fall back to any
            // active, non-WiFi adapter before trying gateway/IP fallback.
            candidates = adapterList
                .Where(adapter => !IsAdapterKind(adapter, NetworkAdapterKind.Wifi))
                .ToList();
        }

        return candidates;
    }

    private static int GetInterfaceIndexByType(
        IEnumerable<NetworkInterface> adapters,
        NetworkAdapterKind adapterKind)
    {
        try
        {
            return GetAdaptersByType(adapters, adapterKind)
                .Select(adapter => new
                {
                    InterfaceIndex = GetIPv4InterfaceIndex(adapter.GetIPProperties()),
                    Gateway        = GetIPv4Gateway(adapter),
                    IPv4           = GetIPv4(adapter),
                    IsDefault      = IsDefaultGatewayAdapter(adapter),
                })
                .Where(candidate => candidate.InterfaceIndex > 0)
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Gateway)
                    || !string.IsNullOrWhiteSpace(candidate.IPv4))
                .OrderByDescending(candidate => candidate.IsDefault)
                .Select(candidate => candidate.InterfaceIndex)
                .FirstOrDefault();
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsAdapterKind(NetworkInterface adapter, NetworkAdapterKind adapterKind)
    {
        string name = adapter.Name ?? string.Empty;
        string description = adapter.Description ?? string.Empty;
        string text = $"{name} {description}";

        return adapterKind switch
        {
            NetworkAdapterKind.Ethernet =>
                adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                || adapter.NetworkInterfaceType == NetworkInterfaceType.FastEthernetFx
                || adapter.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT
                || adapter.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet
                || (text.Contains("Ethernet", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Local Area", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("LAN", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("GbE", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Gigabit", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Realtek PCIe", StringComparison.OrdinalIgnoreCase)),

            NetworkAdapterKind.Wifi =>
                adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                || text.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
                || text.Contains("WiFi", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Wireless", StringComparison.OrdinalIgnoreCase)
                || text.Contains("WLAN", StringComparison.OrdinalIgnoreCase),

            _ => false,
        };
    }

    private static string GetIPv4Gateway(NetworkInterface adapter)
    {
        try
        {
            return adapter.GetIPProperties().GatewayAddresses
                .Where(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(gateway => gateway.Address.ToString())
                .FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetIPv4(NetworkInterface adapter)
    {
        try
        {
            return adapter.GetIPProperties().UnicastAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.Address.ToString())
                .FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GuessGatewayFromIP(string ipv4)
    {
        try
        {
            if (!IPAddress.TryParse(ipv4, out IPAddress? address)
                || address.AddressFamily != AddressFamily.InterNetwork)
            {
                return string.Empty;
            }

            byte[] bytes = address.GetAddressBytes();
            bytes[3] = 1;
            return new IPAddress(bytes).ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsDefaultGatewayAdapter(NetworkInterface adapter)
    {
        try
        {
            return adapter.GetIPProperties().GatewayAddresses
                .Any(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork
                    && gateway.Address.ToString() != "0.0.0.0");
        }
        catch
        {
            return false;
        }
    }

    private void ResetInterfaceMetric(int interfaceIndex, string label)
    {
        if (interfaceIndex <= 0)
        {
            Log($"  [WARN] Cannot reset {label} metric: interface index not found.");
            return;
        }

        try
        {
            string script =
                $"Set-NetIPInterface -InterfaceIndex {interfaceIndex} -AddressFamily IPv4 -AutomaticMetric Enabled";
            var (exitCode, output) = RunCommand(
                "powershell",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"");

            if (exitCode != 0)
                Log($"  [WARN] Cannot reset {label} metric on interface {interfaceIndex}: {output}");
            else
                Log($"  [OK]   Reset {label} metric on interface {interfaceIndex}");
        }
        catch (Exception ex)
        {
            Log($"  [WARN] ResetInterfaceMetric({label}): {ex.Message}");
        }
    }

    private void SetInterfaceMetric(int interfaceIndex, int metric, string label)
    {
        if (interfaceIndex <= 0)
        {
            Log($"  [WARN] Cannot set {label} metric: interface index not found.");
            return;
        }

        try
        {
            var (exitCode, output) = RunCommand(
                "netsh",
                $"interface ipv4 set interface {interfaceIndex} metric={metric}");

            if (exitCode != 0)
                Log($"  [WARN] Cannot set {label} metric on interface {interfaceIndex}: {output}");
            else
                Log($"  [OK]   Set {label} interface {interfaceIndex} metric={metric}");
        }
        catch (Exception ex)
        {
            Log($"  [WARN] SetInterfaceMetric({label}): {ex.Message}");
        }
    }

    private void ResetManagedNetworkMetrics(int ethernetInterfaceIndex, int wifiInterfaceIndex)
    {
        var resetIndexes = new HashSet<int>();

        if (ethernetInterfaceIndex > 0 && resetIndexes.Add(ethernetInterfaceIndex))
            ResetInterfaceMetric(ethernetInterfaceIndex, "Ethernet");

        if (wifiInterfaceIndex > 0 && resetIndexes.Add(wifiInterfaceIndex))
            ResetInterfaceMetric(wifiInterfaceIndex, "WiFi");
    }

    private void ApplyInterfaceMetrics(int ethernetInterfaceIndex, int wifiInterfaceIndex)
    {
        SetInterfaceMetric(ethernetInterfaceIndex, metric: 10, label: "Ethernet");
        SetInterfaceMetric(wifiInterfaceIndex,     metric: 50, label: "WiFi");
    }

    /// <summary>
    /// Parses route print output and returns only routes whose gateway equals
    /// the Ethernet gateway entered by the user.
    /// </summary>
    private RoutesByGateway ParseRoutesByGateway(string gateway)
    {
        var networks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ips      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            string ethGateway = gateway.Trim();
            if (!IsValidIp(ethGateway))
            {
                Log($"[WARN] Ethernet Gateway không hợp lệ, không load route: '{gateway}'");
                return new RoutesByGateway([], []);
            }

            string routeTable = GetRouteTable();
            if (string.IsNullOrWhiteSpace(routeTable))
                return new RoutesByGateway([], []);

            foreach (string line in routeTable.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string[] parts = line.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3)
                        continue;

                    string destination = parts[0];
                    string mask        = parts[1];
                    string routeGateway = parts[2];

                    if (!IPAddress.TryParse(destination, out IPAddress? destinationAddress)
                        || destinationAddress.AddressFamily != AddressFamily.InterNetwork
                        || !IPAddress.TryParse(mask, out IPAddress? maskAddress)
                        || maskAddress.AddressFamily != AddressFamily.InterNetwork
                        || !IPAddress.TryParse(routeGateway, out IPAddress? gatewayAddress)
                        || gatewayAddress.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    if (!string.Equals(gatewayAddress.ToString(), ethGateway, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (IsSystemRoute(destinationAddress))
                        continue;

                    if (mask == "255.255.255.255")
                    {
                        ips.Add(destinationAddress.ToString());
                        continue;
                    }

                    int prefix = MaskToCIDR(mask);
                    if (prefix < 0)
                    {
                        Log($"  [WARN] Bỏ qua route có subnet mask không hợp lệ: {destination} {mask}");
                        continue;
                    }

                    networks.Add($"{destinationAddress}/{prefix}");
                }
                catch (Exception ex)
                {
                    Log($"  [WARN] Bỏ qua dòng route không parse được: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[WARN] ParseRoutesByGateway: {ex.Message}");
        }

        return new RoutesByGateway(
            SortRoutes(networks, includePrefix: true),
            SortRoutes(ips, includePrefix: false));
    }

    private static bool IsSystemRoute(IPAddress destination)
    {
        byte[] bytes = destination.GetAddressBytes();
        byte firstOctet = bytes[0];

        return destination.ToString() == "0.0.0.0"
            || firstOctet == 127
            || firstOctet is >= 224 and <= 239;
    }

    private static string[] SortRoutes(IEnumerable<string> routes, bool includePrefix)
    {
        return routes
            .OrderBy(route => GetRouteSortAddress(route), Comparer<uint>.Default)
            .ThenBy(route => includePrefix ? GetRouteSortPrefix(route) : 0)
            .ThenBy(route => route, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static uint GetRouteSortAddress(string route)
    {
        string ip = route.Split('/')[0];
        return IPAddress.TryParse(ip, out IPAddress? address)
            && address.AddressFamily == AddressFamily.InterNetwork
                ? IPv4ToUInt32(address)
                : uint.MaxValue;
    }

    private static int GetRouteSortPrefix(string route)
    {
        string[] parts = route.Split('/');
        return parts.Length == 2 && int.TryParse(parts[1], out int prefix)
            ? prefix
            : 0;
    }

    private static uint IPv4ToUInt32(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24)
            | ((uint)bytes[1] << 16)
            | ((uint)bytes[2] << 8)
            | bytes[3];
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
    private void DeleteRoute(string destination, string mask)
    {
        var (exitCode, output) = RunCommand("route", $"delete {destination} mask {mask}");
        if (exitCode != 0)
            Log($"  [WARN] Cannot delete route {destination} mask {mask}: {output}");
        else
            Log($"  [OK]   Deleted route {destination} mask {mask}");
    }

    /// <summary>
    /// Deletes only the routes currently entered in the Networks/IPs UI.
    /// Missing routes are logged as warnings by route.exe but never crash Apply.
    /// </summary>
    private void DeleteRoutesFromUI()
    {
        try
        {
            foreach (string rawLine in txtNetworks.Lines)
            {
                string cidr = rawLine.Trim();
                if (string.IsNullOrEmpty(cidr))
                    continue;

                if (!IsValidCidr(cidr))
                {
                    Log($"  [WARN] Bỏ qua CIDR không hợp lệ khi xóa: '{cidr}'");
                    continue;
                }

                string[] parts = cidr.Split('/');
                string network = parts[0].Trim();
                string mask = CIDRToMask(cidr);
                if (string.IsNullOrEmpty(mask))
                {
                    Log($"  [WARN] Không convert được CIDR sang mask: '{cidr}'");
                    continue;
                }

                DeleteRoute(network, mask);
                Log($"Deleted route: {cidr}");
            }

            foreach (string rawLine in txtIPs.Lines)
            {
                string ip = rawLine.Trim();
                if (string.IsNullOrEmpty(ip))
                    continue;

                if (!IsValidIp(ip))
                {
                    Log($"  [WARN] Bỏ qua IP không hợp lệ khi xóa: '{ip}'");
                    continue;
                }

                DeleteRoute(ip, "255.255.255.255");
                Log($"Deleted IP route: {ip}");
            }
        }
        catch (Exception ex)
        {
            Log($"[WARN] DeleteRoutesFromUI: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes every non-system route currently using the Ethernet gateway.
    /// This removes routes that were previously added by the app but have since
    /// been removed from the UI, so Apply can rebuild the route list cleanly.
    /// </summary>
    private void DeleteExistingRoutesByGateway(string ethGateway)
    {
        try
        {
            RoutesByGateway existingRoutes = ParseRoutesByGateway(ethGateway);
            Log($"Found {existingRoutes.Count} existing route(s) from gateway {ethGateway}");

            foreach (string cidr in existingRoutes.Networks)
            {
                string[] parts = cidr.Split('/');
                if (parts.Length != 2)
                {
                    Log($"  [WARN] Bỏ qua route không hợp lệ khi xóa theo gateway: '{cidr}'");
                    continue;
                }

                string network = parts[0].Trim();
                string mask = CIDRToMask(cidr);
                if (string.IsNullOrEmpty(mask))
                {
                    Log($"  [WARN] Không convert được CIDR sang mask: '{cidr}'");
                    continue;
                }

                DeleteRoute(network, mask);
                Log($"Deleted existing route from gateway: {cidr}");
            }

            foreach (string ip in existingRoutes.IPs)
            {
                if (!IsValidIp(ip))
                {
                    Log($"  [WARN] Bỏ qua IP không hợp lệ khi xóa theo gateway: '{ip}'");
                    continue;
                }

                DeleteRoute(ip, "255.255.255.255");
                Log($"Deleted existing IP route from gateway: {ip}");
            }
        }
        catch (Exception ex)
        {
            Log($"[WARN] DeleteExistingRoutesByGateway: {ex.Message}");
        }
    }

    private void DeleteDefaultRoute()
    {
        try
        {
            DeleteRoute("0.0.0.0", "0.0.0.0");
            Log("Deleted default route: 0.0.0.0/0");
        }
        catch (Exception ex)
        {
            Log($"[WARN] DeleteDefaultRoute: {ex.Message}");
        }
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

            var activeAdapters = GetActiveAdapters();
            int ethernetInterfaceIndex = GetInterfaceIndexByType(activeAdapters, NetworkAdapterKind.Ethernet);
            int wifiInterfaceIndex     = GetInterfaceIndexByType(activeAdapters, NetworkAdapterKind.Wifi);

            // Step 1 – Clear and re-apply interface metrics like the BAT file
            Log("─── Bước 1: Reset / set interface metric ───────────────");
            ResetManagedNetworkMetrics(ethernetInterfaceIndex, wifiInterfaceIndex);
            ApplyInterfaceMetrics(ethernetInterfaceIndex, wifiInterfaceIndex);

            // Step 2 – Delete all old Ethernet routes plus the default WiFi route
            Log("─── Bước 2: Xóa route cũ ───────────────────────────────");
            DeleteExistingRoutesByGateway(ethGateway);
            DeleteDefaultRoute();

            // Step 3 – Add routes for each CIDR network → Ethernet gateway
            Log("─── Bước 3: Thêm route mạng (CIDR) → Ethernet ─────────");
            foreach (string cidr in networks)
            {
                string[] parts  = cidr.Split('/');
                string network  = parts[0];
                int    prefix   = int.Parse(parts[1]);
                string mask     = CidrToSubnetMask(prefix);
                AddRoute(network, mask, ethGateway, metric: 5);
            }

            // Step 4 – Add host routes for individual IPs → Ethernet gateway
            Log("─── Bước 4: Thêm route IP riêng lẻ → Ethernet ─────────");
            foreach (string ip in ips)
            {
                AddRoute(ip, "255.255.255.255", ethGateway, metric: 5);
            }

            // Step 5 – Default route via WiFi gateway
            Log("─── Bước 5: Thêm default route → WiFi ─────────────────");
            AddRoute("0.0.0.0", "0.0.0.0", wifiGateway, metric: 20);

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

    /// <summary>Detect Network button: fills Ethernet/WiFi gateway text boxes.</summary>
    private void btnDetectNetwork_Click(object sender, EventArgs e) => DetectNetworkGateways();

    private void DetectNetworkGateways()
    {
        try
        {
            btnDetectNetwork.Enabled = false;
            Cursor = Cursors.WaitCursor;

            var adapters = GetActiveAdapters();
            string ethernetGateway = GetGatewayByType(adapters, NetworkAdapterKind.Ethernet);
            string wifiGateway     = GetGatewayByType(adapters, NetworkAdapterKind.Wifi);

            txtEthGateway.Text  = ethernetGateway;
            txtWifiGateway.Text = wifiGateway;

            Log($"Detected Ethernet: {(string.IsNullOrEmpty(ethernetGateway) ? "(none)" : ethernetGateway)}");
            Log($"Detected WiFi: {(string.IsNullOrEmpty(wifiGateway) ? "(none)" : wifiGateway)}");
        }
        catch (Exception ex)
        {
            Log($"[WARN] DetectNetworkGateways: {ex.Message}");
        }
        finally
        {
            btnDetectNetwork.Enabled = true;
            Cursor = Cursors.Default;
        }
    }

    /// <summary>Load Existing Routes button: fills route text boxes from route print.</summary>
    private void btnLoadExistingRoutes_Click(object sender, EventArgs e) => LoadExistingRoutesIntoUi(logWhenGatewayMissing: true);

    private void LoadExistingRoutesIntoUi(bool logWhenGatewayMissing)
    {
        string ethGateway = txtEthGateway.Text.Trim();
        if (string.IsNullOrWhiteSpace(ethGateway))
        {
            if (logWhenGatewayMissing)
                Log("[WARN] Nhập Ethernet Gateway trước khi load route hiện có.");

            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;

            RoutesByGateway routes = ParseRoutesByGateway(ethGateway);
            txtNetworks.Lines = routes.Networks;
            txtIPs.Lines      = routes.IPs;

            Log($"Loaded {routes.Count} routes from gateway {ethGateway}");
        }
        catch (Exception ex)
        {
            Log($"[WARN] LoadExistingRoutesIntoUi: {ex.Message}");
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    // ─────────────────────── Form Load ──────────────────────────────────

    /// <summary>
    /// On form load, populate the Network Info tab immediately so the user
    /// sees data without having to click Refresh first.
    /// </summary>
    private void Form1_Load(object sender, EventArgs e)
    {
        DetectNetworkGateways();
        LoadNetworkInfo();
        LoadExistingRoutesIntoUi(logWhenGatewayMissing: false);
    }

    // ──────────────────── Network Info – Button Handlers ────────────────

    /// <summary>Refresh button on the Network Info tab.</summary>
    private void btnRefreshNetwork_Click(object sender, EventArgs e)
    {
        btnRefreshNetwork.Enabled = false;
        Cursor = Cursors.WaitCursor;
        try
        {
            LoadNetworkInfo();
        }
        finally
        {
            btnRefreshNetwork.Enabled = true;
            Cursor = Cursors.Default;
        }
    }

    /// <summary>
    /// Copy IP button: copies the IPv4 address of the selected adapter row
    /// to the system clipboard.
    /// </summary>
    private void btnCopyIP_Click(object sender, EventArgs e)
    {
        if (dgvNetworkInfo.SelectedRows.Count == 0)
        {
            MessageBox.Show(
                "Vui lòng chọn một adapter trước.",
                "Copy IP", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string ip = dgvNetworkInfo.SelectedRows[0].Cells["colIPv4"].Value?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(ip) || ip == "(none)")
        {
            MessageBox.Show(
                "Adapter đang chọn không có địa chỉ IPv4.",
                "Copy IP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Clipboard.SetText(ip);
        Log($"Đã copy IP: {ip}");
        lblNetworkStatus.Text = $"✅  Copied to clipboard: {ip}";
    }

    // ──────────────────── Network Info – Core Logic ──────────────────────

    /// <summary>
    /// Data model for one network adapter entry.
    /// </summary>
    private sealed record AdapterInfo(
        string Name,
        string Status,
        string IPv4,
        string SubnetMask,
        string Gateway,
        string DNS,
        int    InterfaceIndex,
        bool   IsDefault);

    private enum NetworkAdapterKind
    {
        Ethernet,
        Wifi,
    }

    private sealed record RoutesByGateway(string[] Networks, string[] IPs)
    {
        public int Count => Networks.Length + IPs.Length;
    }

    /// <summary>
    /// Loads active (Up, non-loopback) network adapters into the DataGridView
    /// and highlights the adapter currently carrying the default route.
    /// </summary>
    private void LoadNetworkInfo()
    {
        Log("═══════════════ Network Info – Refreshing ═══════════════");

        try
        {
            string defaultGatewayIp = GetDefaultGatewayIp();
            var adapters = GetNetworkAdapters(defaultGatewayIp);

            dgvNetworkInfo.Rows.Clear();

            foreach (AdapterInfo a in adapters)
            {
                int idx = dgvNetworkInfo.Rows.Add(
                    a.Name,
                    a.Status,
                    a.IPv4,
                    a.SubnetMask,
                    a.Gateway,
                    a.DNS,
                    a.InterfaceIndex);

                if (a.IsDefault)
                {
                    // Highlight the adapter carrying the default (internet) route.
                    var row = dgvNetworkInfo.Rows[idx];
                    row.DefaultCellStyle.BackColor = Color.FromArgb(210, 245, 210);
                    row.DefaultCellStyle.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(0, 100, 0);
                }
            }

            string defaultInfo = string.IsNullOrEmpty(defaultGatewayIp)
                ? "no default gateway detected"
                : $"default gateway → {defaultGatewayIp}";

            lblNetworkStatus.Text =
                $"Last refresh: {DateTime.Now:HH:mm:ss}  |  " +
                $"{adapters.Count} active adapter(s)  |  {defaultInfo}";

            Log($"Network Info: {adapters.Count} active adapter(s) found. {defaultInfo}.");
        }
        catch (Exception ex)
        {
            Log($"[ERR] LoadNetworkInfo: {ex.Message}");
            lblNetworkStatus.Text = $"⚠  Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Returns all active (Up, non-loopback) network adapters with their
    /// IPv4 configuration details.
    /// </summary>
    /// <param name="defaultGatewayIp">
    ///   The IPv4 gateway of the current default route (may be empty).
    ///   Used to mark the <see cref="AdapterInfo.IsDefault"/> flag.
    /// </param>
    private static List<AdapterInfo> GetNetworkAdapters(string defaultGatewayIp)
    {
        var result = new List<AdapterInfo>();

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            // Skip loopback interfaces.
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            // Only show adapters that are currently Up.
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;

            IPInterfaceProperties props = nic.GetIPProperties();

            // ── IPv4 unicast address & subnet mask ────────────────────────
            UnicastIPAddressInformation? unicast = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

            string ipv4       = unicast?.Address.ToString() ?? "(none)";
            string subnetMask = unicast is not null
                ? CidrToSubnetMask(unicast.PrefixLength)
                : "(none)";

            // ── Default gateway ───────────────────────────────────────────
            string gateway = props.GatewayAddresses
                .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(g => g.Address.ToString())
                .FirstOrDefault() ?? "(none)";

            // ── DNS servers (IPv4 only) ────────────────────────────────────
            string dns = string.Join(", ", props.DnsAddresses
                .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                .Select(d => d.ToString()));
            if (string.IsNullOrEmpty(dns))
                dns = "(none)";

            // ── Interface index ───────────────────────────────────────────
            int ifIndex = GetIPv4InterfaceIndex(props);

            // ── Is this the internet-facing adapter? ──────────────────────
            bool isDefault = !string.IsNullOrEmpty(defaultGatewayIp)
                && gateway == defaultGatewayIp;

            result.Add(new AdapterInfo(
                Name:           nic.Name,
                Status:         nic.OperationalStatus.ToString(),
                IPv4:           ipv4,
                SubnetMask:     subnetMask,
                Gateway:        gateway,
                DNS:            dns,
                InterfaceIndex: ifIndex,
                IsDefault:      isDefault));
        }

        return result;
    }

    /// <summary>
    /// Safely retrieves the IPv4 interface index; returns 0 on failure.
    /// </summary>
    private static int GetIPv4InterfaceIndex(IPInterfaceProperties props)
    {
        try
        {
            return props.GetIPv4Properties().Index;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Parses <c>route print 0.0.0.0</c> to find the gateway of the current
    /// default route (the route with destination 0.0.0.0 mask 0.0.0.0).
    /// Returns an empty string if the default route cannot be determined.
    /// </summary>
    private string GetDefaultGatewayIp()
    {
        try
        {
            var (_, output) = RunCommand("route", "print 0.0.0.0");

            // Data rows look like (columns separated by whitespace):
            //   Network Dest   Netmask     Gateway        Interface  Metric
            //   0.0.0.0        0.0.0.0     192.168.5.1    192.168.5.100    1
            foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.TrimStart();
                if (!trimmed.StartsWith("0.0.0.0", StringComparison.Ordinal))
                    continue;

                string[] parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                // parts[0] = "0.0.0.0", parts[1] = "0.0.0.0", parts[2] = gateway
                if (parts.Length >= 3 && IPAddress.TryParse(parts[2], out _))
                    return parts[2];
            }
        }
        catch (Exception ex)
        {
            Log($"[WARN] GetDefaultGatewayIp: {ex.Message}");
        }

        return string.Empty;
    }
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
