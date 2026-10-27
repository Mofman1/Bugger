using System.Reflection;
using System.Runtime.InteropServices;
using Bugger.Core;

namespace Bugger.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\" + CommandPipe.Name, out var first);
        if (args.Length > 0)
        {
            var outputHandle = GetStdHandle(-11);
            if (outputHandle == IntPtr.Zero || outputHandle == new IntPtr(-1)) AttachConsole(unchecked((uint)-1));
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
            CommandResult result;
            try
            {
                if (args[0] is "help" or "--help" or "-h") result = new(true, TaskCommands.Help);
                else if (!first) result = CommandPipe.Send(args);
                else
                {
                    var store = new TaskStore(Path.Combine(AppContext.BaseDirectory, "tasks.csv"));
                    result = TaskCommands.Execute(args, store.Load(), updated => store.Save(updated), DateTime.Now);
                }
            }
            catch (Exception e) { result = new(false, e.Message); }
            (result.Success ? Console.Out : Console.Error).WriteLine(result.Output);
            return result.Success ? 0 : 1;
        }
        ApplicationConfiguration.Initialize();
        if (!first) { MessageBox.Show("This application is already running. Open it from its tray icon.", Identity.DefaultName); return 0; }
        try { Application.Run(new MainForm()); }
        catch (Exception e) { MessageBox.Show(e.Message, Identity.DefaultName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        return 0;
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint processId);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int handle);
}

internal static class Identity
{
    public static string DefaultName => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Task reminders";
}

internal sealed class AppSettings
{
    public string Name { get; set; } = Identity.DefaultName;
    public int PopupSeconds { get; set; } = 15;
    public bool TodayExpanded { get; set; } = true;
    public bool OtherDaysExpanded { get; set; } = true;
    private static string PathName => Path.Combine(AppContext.BaseDirectory, "settings.txt");
    public static AppSettings Load()
    {
        var settings = new AppSettings();
        if (!File.Exists(PathName)) return settings;
        foreach (var line in File.ReadLines(PathName))
        {
            var pair = line.Split('=', 2);
            if (pair.Length != 2) continue;
            if (pair[0].Trim() == "Name" && !string.IsNullOrWhiteSpace(pair[1])) settings.Name = pair[1].Trim();
            if (pair[0].Trim() == "PopupSeconds" && int.TryParse(pair[1], out var seconds)) settings.PopupSeconds = Math.Clamp(seconds, 3, 300);
            if (pair[0].Trim() == "TodayExpanded" && bool.TryParse(pair[1], out var today)) settings.TodayExpanded = today;
            if (pair[0].Trim() == "OtherDaysExpanded" && bool.TryParse(pair[1], out var other)) settings.OtherDaysExpanded = other;
        }
        return settings;
    }
    public void Save()
    {
        var temp = PathName + ".tmp";
        File.WriteAllText(temp, $"# Application name, normal reminder duration, and task groups\nName={Name.Replace("\r", "").Replace("\n", "")}\nPopupSeconds={PopupSeconds}\nTodayExpanded={TodayExpanded}\nOtherDaysExpanded={OtherDaysExpanded}\n");
        File.Move(temp, PathName, true);
    }
}
