namespace SITWCH_ETH;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Standard WinForms bootstrapping for .NET 6+
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}
