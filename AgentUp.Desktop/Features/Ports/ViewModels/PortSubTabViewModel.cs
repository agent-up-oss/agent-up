using System.Net.Sockets;
using AgentUp.Desktop.Features.Applications.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Ports.ViewModels;

public sealed class PortSubTabViewModel : SubTabViewModel
{
    private PortLedState _ledState = PortLedState.Probing;
    private bool _tcpIsOpen;

    public string? Variable { get; }
    public int DefaultPort { get; }
    public int AllocatedPort { get; }
    public string Protocol { get; }
    public bool IsHttp => Protocol == "http";

    public override string Label => $"{DefaultPort}:{AllocatedPort}";
    public string Url => $"http://localhost:{AllocatedPort}/";

    public bool IsOpen => _ledState == PortLedState.Healthy || (_ledState == PortLedState.Probing && _tcpIsOpen);

    public string StatusColor => _ledState switch
    {
        PortLedState.Checking  => AppHealthLedRules.StateColor("Checking"),
        PortLedState.Healthy   => AppHealthLedRules.StateColor("Healthy"),
        PortLedState.Unhealthy => AppHealthLedRules.StateColor("Unhealthy"),
        _                      => _tcpIsOpen ? AppHealthLedRules.StateColor("Healthy") : AppHealthLedRules.StateColor("Failed")
    };

    public PortSubTabViewModel(string? variable, int defaultPort, int allocatedPort, string protocol = "http")
    {
        Variable = variable;
        DefaultPort = defaultPort;
        AllocatedPort = allocatedPort;
        Protocol = protocol;
    }

    public void SetLedState(PortLedState state)
    {
        _ledState = state;
        this.RaisePropertyChanged(nameof(StatusColor));
        this.RaisePropertyChanged(nameof(IsOpen));
    }

    public async Task ProbeAsync()
    {
        if (_ledState != PortLedState.Probing) return;

        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await tcp.ConnectAsync("127.0.0.1", AllocatedPort, cts.Token);
            _tcpIsOpen = true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            _tcpIsOpen = false;
        }
        this.RaisePropertyChanged(nameof(StatusColor));
        this.RaisePropertyChanged(nameof(IsOpen));
    }
}
