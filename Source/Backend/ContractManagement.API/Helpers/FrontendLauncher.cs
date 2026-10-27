using System.Diagnostics;
using System.Net.Sockets;

namespace ContractManagement.API.Helpers;

public static class FrontendLauncher
{
    private static bool _started;

    public static void Start(bool launchFrontends = true, bool launchNodeBackend = false, int nodePort = 4000)
    {
        if (_started)
            return;

        _started = true;

        var sourcePath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                ".."));

        var tenantFrontendPath = Path.Combine(
            sourcePath,
            "Frontend");

        var systemAdminFrontendPath = Path.Combine(
            sourcePath,
            "SystemAdmin");

        if (launchNodeBackend)
        {
            StartApplication(
                name: "Node Backend",
                applicationPath: Path.Combine(sourcePath, "BackendNode"),
                port: nodePort);
        }

        if (!launchFrontends) return;

        StartApplication(
            name: "Tenant Frontend",
            applicationPath: tenantFrontendPath,
            port: 3000);

        StartApplication(
            name: "System Admin Frontend",
            applicationPath: systemAdminFrontendPath,
            port: 3001);
    }

    private static void StartApplication(
        string name,
        string applicationPath,
        int port)
    {
        if (IsPortOpen("localhost", port))
        {
            Console.WriteLine(
                $"[{name}] Port {port} đang chạy, bỏ qua khởi động.");

            return;
        }

        if (!Directory.Exists(applicationPath))
        {
            Console.WriteLine(
                $"[{name}] Không tìm thấy thư mục: {applicationPath}");

            return;
        }

        var packageJsonPath = Path.Combine(
            applicationPath,
            "package.json");

        if (!File.Exists(packageJsonPath))
        {
            Console.WriteLine(
                $"[{name}] Không tìm thấy package.json: {packageJsonPath}");

            return;
        }

        try
        {
            var process = new Process();

            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = "/c npm run dev";
            process.StartInfo.WorkingDirectory = applicationPath;
            process.StartInfo.UseShellExecute = true;

            process.Start();

            Console.WriteLine(
                $"[{name}] Đã khởi động tại port {port}.");

            Console.WriteLine(
                $"[{name}] Thư mục: {applicationPath}");
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"[{name}] Không thể khởi động: {exception.Message}");
        }
    }

    private static bool IsPortOpen(
        string host,
        int port)
    {
        try
        {
            using var client = new TcpClient();

            client.Connect(host, port);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
