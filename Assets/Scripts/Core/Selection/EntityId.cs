using UnityEngine;

namespace Back2War.Core.Selection
{
    /// <summary>
    /// Determinism constraints:
    /// - Id must be stable for the full match.
    /// - OwnerPlayerId is used for deterministic enemy/friendly command routing.
    /// </summary>
    public sealed class EntityId : MonoBehaviour
    {
        [field: SerializeField] public uint Id { get; private set; } = 1;
        [field: SerializeField] public byte OwnerPlayerId { get; private set; } = 0;
    }
}
