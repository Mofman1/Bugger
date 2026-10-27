using System.IO.Pipes;
using System.Text.Json;
using Bugger.Core;

namespace Bugger.App;

internal sealed class CommandPipe : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    public static string Name => "PortableReminder_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(AppContext.BaseDirectory.ToUpperInvariant())));

    public CommandPipe(MainForm form)
    {
        _ = Serve(form);
    }

    private async Task Serve(MainForm form)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop.Token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                using var reader = new StreamReader(pipe, leaveOpen: true);
                using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                var line = await reader.ReadLineAsync(deadline.Token);
                var args = JsonSerializer.Deserialize<string[]>(line ?? "null");
                var result = args == null ? new CommandResult(false, "Missing command.") : form.ExecuteCommand(args);
                await writer.WriteLineAsync(JsonSerializer.Serialize(result).AsMemory(), deadline.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) when (e is IOException or JsonException or ArgumentException) { }
        }
    }

    public static CommandResult Send(string[] args)
    {
        using var pipe = new NamedPipeClientStream(".", Name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        pipe.Connect(5000);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var reader = new StreamReader(pipe, leaveOpen: true);
        using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        writer.WriteLine(JsonSerializer.Serialize(args));
        var line = reader.ReadLineAsync(deadline.Token).AsTask().GetAwaiter().GetResult();
        return JsonSerializer.Deserialize<CommandResult>(line ?? "null") ?? new(false, "No response from the application.");
    }

    public void Dispose() { stop.Cancel(); }
}
