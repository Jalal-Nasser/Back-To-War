using System.Collections.Generic;
using UnityEngine;

namespace Back2War.Selection {
    /// <summary>
    /// Manages the current selection of units or buildings.
    /// </summary>
    public class SelectionManager : MonoBehaviour {
        private List<GameObject> selectedUnits = new List<GameObject>();

        public void SelectUnit(GameObject unit) {
            // TODO: add selection logic (multi-select, shift-click, etc.)
            selectedUnits.Add(unit);
        }

        public void DeselectAll() {
            // TODO: clear selection and update UI/visuals
            selectedUnits.Clear();
        }
    }
}
