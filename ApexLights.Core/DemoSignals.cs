namespace ApexLights.Core;

// Ephemeral simulator inputs. A reconnect or a new session re-arms active alerts.
public sealed class DemoSignals
{
    public int Gear { get; set; } = 3;
    public RaceFlag Flags { get; set; }
    public SpotterState Spotter { get; set; } = SpotterState.Clear;
    public bool Connected { get; private set; } = true;
    private int sessionNumber = 1;

    public RaceSignal? Read() => Connected
        ? new RaceSignal(Gear, Flags, Spotter, new SessionKey(0, 0, sessionNumber, "demo")) : null;

    public void SetConnection(bool connected)
    {
        if (!Connected && connected) NewSession();
        Connected = connected;
    }
    public void NewSession() => sessionNumber++;
}
