using System;

namespace SolarGrow
{
    /// <summary>Where frames come from.</summary>
    public enum SourceKind { Simulated, TcpJsonLines }

    /// <summary>
    /// A source of twin/state frames. Connect/Disconnect/Tick are called on the Unity main thread;
    /// OnState and OnStatus are raised only from Tick, so handlers may touch Unity objects.
    /// </summary>
    public interface ITelemetrySource
    {
        event Action<TwinStateFrame> OnState;
        event Action<string> OnStatus;
        bool Connected { get; }
        void Connect();
        void Disconnect();
        void Tick();
    }

    /// <summary>Takes manual commands from the panel. Returns false when the command was not sent.</summary>
    public interface ICommandSink
    {
        bool SendCommand(string greenhouse, string device, string value);
    }
}
