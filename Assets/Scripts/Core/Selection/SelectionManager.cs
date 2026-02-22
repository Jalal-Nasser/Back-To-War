using System.Collections.Generic;
using UnityEngine;

namespace Back2War.Core.Selection
{
    /// <summary>
    /// Prototype manager retained while deterministic SelectionController handles runtime selection logic.
    /// </summary>
    public sealed class SelectionManager : MonoBehaviour
    {
        private readonly List<GameObject> _selectedUnits = new List<GameObject>();

        public void SelectUnit(GameObject unit)
        {
            if (unit != null)
            {
                _selectedUnits.Add(unit);
            }
        }

        public void DeselectAll()
        {
            _selectedUnits.Clear();
        }
    }
}
