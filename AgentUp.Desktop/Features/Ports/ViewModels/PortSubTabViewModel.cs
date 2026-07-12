using System.Net.Sockets;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Ports.ViewModels;

public sealed class PortSubTabViewModel : SubTabViewModel
{
    private bool _isOpen;

    public string? Variable { get; }
    public int DefaultPort { get; }
    public int AllocatedPort { get; }

    public override string Label => $"{DefaultPort}:{AllocatedPort}";
    public string Url => $"http://localhost:{AllocatedPort}/";

    public bool IsOpen
    {
        get => _isOpen;
        private set => this.RaiseAndSetIfChanged(ref _isOpen, value);
    }

    public string StatusColor => IsOpen ? "#4cbe78" : "#b85a5a";

    public PortSubTabViewModel(string? variable, int defaultPort, int allocatedPort)
    {
        Variable = variable;
        DefaultPort = defaultPort;
        AllocatedPort = allocatedPort;
    }

    public async Task ProbeAsync()
    {
        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await tcp.ConnectAsync("127.0.0.1", AllocatedPort, cts.Token);
            IsOpen = true;
        }
        catch
        {
            IsOpen = false;
        }
        this.RaisePropertyChanged(nameof(StatusColor));
    }
}
