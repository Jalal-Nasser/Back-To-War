namespace Back2War.Core.Commands
{
    /// <summary>
    /// Prototype command base retained for migration while SimCommand is authoritative for lockstep payloads.
    /// </summary>
    public abstract class Command
    {
        public abstract void Execute();
    }
}
