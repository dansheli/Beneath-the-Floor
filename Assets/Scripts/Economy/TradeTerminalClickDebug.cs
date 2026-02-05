using UnityEngine;
using UnityEngine.EventSystems;

namespace BeneathTheFloor.Economy
{
    /// <summary>
    /// Debug component to verify click events are received on the Trade Terminal panel.
    /// Attach to TradeTerminalPanel to test if raycasts are working.
    /// Can be removed once clicks are confirmed working.
    /// </summary>
    public class TradeTerminalClickDebug : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log($"[TradeTerminalClickDebug] Click detected at {eventData.position} on {gameObject.name}");
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log($"[TradeTerminalClickDebug] Pointer entered {gameObject.name}");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Debug.Log($"[TradeTerminalClickDebug] Pointer exited {gameObject.name}");
        }
    }
}
