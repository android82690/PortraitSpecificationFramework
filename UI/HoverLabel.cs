using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpecificPortraits.UI
{
    /// <summary>Reports the pointer entering or leaving a grid cell to one shared label. Costs nothing while idle.</summary>
    public class HoverLabel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Label;
        public Action<string> OnHover;

        public static HoverLabel Attach(GameObject go, string label, Action<string> onHover)
        {
            var h = go.AddComponent<HoverLabel>();
            h.Label = label;
            h.OnHover = onHover;
            return h;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnHover?.Invoke(Label);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnHover?.Invoke("");
        }
    }
}
