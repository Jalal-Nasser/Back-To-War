using UnityEngine;

namespace Back2War.Commands {
    /// <summary>
    /// Abstract base class for game commands. Concrete commands should inherit from this class
    /// and implement the Execute method with deterministic logic only (no physics calls).
    /// </summary>
    public abstract class Command {
        public abstract void Execute();
    }
}
