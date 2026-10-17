using UnityEngine;
using UnityEngine.UI;

namespace MercenariesOfMars
{
    // Image를 사용해 스프라이트 시트와 아틀라스의 포트레이트도 올바르게 표시한다.
    public sealed class DialoguePortraitUI : MonoBehaviour
    {
        GameObject root;
        Image image;
        public Sprite DisplayedSprite => root != null && root.activeSelf ? image.sprite : null;
        public void Show(Sprite sprite, Rect rect)
        {
            if (sprite == null) { Hide(); return; }
            if (root == null)
            {
                root = new GameObject("대사 포트레이트",typeof(RectTransform),typeof(Canvas)); root.transform.SetParent(transform,false);
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
                var child = new GameObject("선택 포트레이트",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)); child.transform.SetParent(root.transform,false);
                image = child.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0,1);
                image.rectTransform.pivot = new Vector2(0,1);
            }
            root.SetActive(true); image.sprite = sprite;
            image.rectTransform.anchoredPosition = new Vector2(rect.x,-rect.y);
            image.rectTransform.sizeDelta = rect.size;
        }
        public void Hide() { if (root != null) root.SetActive(false); }
        void OnDisable() => Hide();
        void OnDestroy() { if (root != null) Destroy(root); }
    }
}
