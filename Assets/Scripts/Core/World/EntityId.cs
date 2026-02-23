using UnityEngine;

namespace Back2War.Core.World
{
    /// <summary>
    /// Stable identity for deterministic input targeting.
    /// </summary>
    public sealed class EntityId : MonoBehaviour
    {
        public uint Id = 1;
        public byte TeamId = 0;
    }
}

