using UnityEngine;

namespace GridMage.Workshop
{
    public abstract class WorkshopTab : MonoBehaviour
    {
        public abstract string Title { get; }
        public RectTransform Panel { get; private set; }
        public void Initialize(RectTransform panel) { Panel = panel; Build(panel); }
        protected abstract void Build(RectTransform panel);
        public virtual void SetActive(bool active) { Panel.gameObject.SetActive(active); enabled = active; }
    }
}
