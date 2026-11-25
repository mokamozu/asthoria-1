using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Athoria.UI.Menus
{
    /// <summary>
    /// Botão de menu no estilo do Asthoria antigo: moldura do tileset, texto
    /// que desliza para a direita, cresce e fica dourado no destaque, com um
    /// marcador à esquerda e um traço embaixo.
    /// </summary>
    public sealed class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private MenuArt _art;
        private Image _frame;
        private RectTransform _content;
        private Text _label;
        private Image _marker;
        private Image _underline;
        private bool _pointerOver;
        private float _t;
        private bool _interactable = true;
        private bool _slides = true;

        public bool Highlighted { get; set; }
        public bool IsPointerOver => _pointerOver;
        public event Action Clicked;
        public event Action PointerEntered;

        public string Label
        {
            get => _label.text;
            set => _label.text = value;
        }

        public bool Interactable
        {
            get => _interactable;
            set
            {
                _interactable = value;
                _label.color = value ? MenuTheme.Text : MenuTheme.TextMuted;
            }
        }

        public RectTransform RectTransform => (RectTransform)transform;

        public static MenuButton Create(Transform parent, MenuArt art, string label, float x, float y, float w, float h,
            int fontSize = 30, bool slides = true, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var root = Ui.TopLeft(Ui.Rect(parent, "Botao " + label), x, y, w, h);
            var b = root.gameObject.AddComponent<MenuButton>();
            b._art = art;
            b._slides = slides;
            b._frame = Ui.Image(root, "Moldura", art.Button, new Color(1f, 1f, 1f, 0.92f), true);
            Ui.Stretch(b._frame.rectTransform);
            b._frame.raycastTarget = true;

            b._content = Ui.Stretch(Ui.Rect(root, "Conteudo"));
            b._label = Ui.Text(b._content, "Texto", label, art.Title, fontSize, MenuTheme.Text, align);
            Ui.Stretch(b._label.rectTransform);
            b._label.rectTransform.offsetMin = new Vector2(align == TextAnchor.MiddleLeft ? 26f : 0f, 0f);
            b._label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Shadow(b._label);

            b._marker = Ui.Image(root, "Marcador", art.Triangle, MenuTheme.GoldLight);
            Ui.TopLeft(b._marker.rectTransform, -16f, h * 0.5f - 7f, 14f, 14f);
            b._underline = Ui.Box(b._content, "Traço", MenuTheme.GoldLight);
            var ur = b._underline.rectTransform;
            ur.anchorMin = ur.anchorMax = new Vector2(0f, 0.5f);
            ur.pivot = new Vector2(0f, 0.5f);
            ur.anchoredPosition = new Vector2(align == TextAnchor.MiddleLeft ? 26f : 0f, -fontSize * 0.6f);
            ur.sizeDelta = new Vector2(0f, 2f);
            if (align != TextAnchor.MiddleLeft) b._underline.enabled = false;
            return b;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _pointerOver = true;
            if (_interactable) PointerEntered?.Invoke();
        }

        public void OnPointerExit(PointerEventData e) => _pointerOver = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (_interactable && e.button == PointerEventData.InputButton.Left) Clicked?.Invoke();
        }

        public void Press()
        {
            if (_interactable) Clicked?.Invoke();
        }

        private void OnDisable() => _pointerOver = false;

        private void Update()
        {
            var on = (Highlighted || _pointerOver) && _interactable;
            _t = Mathf.MoveTowards(_t, on ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            var e = _t * _t * (3f - 2f * _t);

            _frame.sprite = on ? _art.ButtonHover : _art.Button;
            if (_slides) _content.anchoredPosition = new Vector2(16f * e, 0f);
            _content.localScale = Vector3.one * (1f + 0.05f * e);
            if (_interactable) _label.color = Color.Lerp(MenuTheme.Text, MenuTheme.GoldLight, e);

            _marker.color = new Color(MenuTheme.GoldLight.r, MenuTheme.GoldLight.g, MenuTheme.GoldLight.b, e);
            var width = Mathf.Min(_label.preferredWidth, ((RectTransform)transform).rect.width - 40f);
            _underline.rectTransform.sizeDelta = new Vector2(width * e, 2f);
        }
    }

    /// <summary>Clique e passagem do mouse em qualquer elemento (miniaturas da roleta).</summary>
    public sealed class UiPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public bool Over { get; private set; }
        public event Action Clicked;

        public void OnPointerEnter(PointerEventData e) => Over = true;
        public void OnPointerExit(PointerEventData e) => Over = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Clicked?.Invoke();
        }
    }
}
