using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Athoria.UI.Menus
{
    /// <summary>
    /// Item de menu minimalista de vidro: só o texto em repouso; no destaque
    /// surge uma placa de vitral atrás, um reflexo atravessa o vidro, os
    /// ornamentos entram pelos lados e o texto clareia.
    /// Clique esquerdo = ação/avançar; clique direito = voltar valor (configurações).
    /// </summary>
    public sealed class GlassMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Image _pane;
        private RectTransform _sheen;
        private Text _label;
        private Text _value;
        private Outline _glow;
        private RectTransform _ornLeft, _ornRight;
        private Image _ornLeftImg, _ornRightImg;
        private float _t;
        private float _sheenT = 1f;
        private bool _wasOn;
        private bool _pointerOver;
        private bool _interactable = true;
        private float _width;

        public bool Highlighted { get; set; }
        public bool IsPointerOver => _pointerOver;
        public event Action Clicked;
        public event Action ClickedBack;
        public event Action PointerEntered;

        public bool Interactable
        {
            get => _interactable;
            set => _interactable = value;
        }

        public string Label
        {
            get => _label.text;
            set => _label.text = value;
        }

        public string Value
        {
            set { if (_value != null) _value.text = value; }
        }

        public static GlassMenuItem Create(Transform parent, MenuArt art, Sprite pane, Sprite sheen, string label,
            float cx, float cy, float w, float h, int fontSize = 32)
        {
            var root = Ui.Center(Ui.Rect(parent, "Item " + label), cx, cy, w, h);
            var item = root.gameObject.AddComponent<GlassMenuItem>();
            item._width = w;

            item._pane = Ui.Image(root, "Placa de Vidro", pane, new Color(1f, 1f, 1f, 0f), true);
            Ui.Stretch(item._pane.rectTransform);
            item._pane.raycastTarget = true;   // a área de clique é a placa inteira

            var clip = Ui.Stretch(Ui.Rect(root, "Recorte do Reflexo"), 6f);
            clip.gameObject.AddComponent<RectMask2D>();
            var sheenImg = Ui.Image(clip, "Reflexo", sheen, new Color(1f, 1f, 1f, 0.35f));
            item._sheen = sheenImg.rectTransform;
            item._sheen.anchorMin = item._sheen.anchorMax = new Vector2(0f, 0.5f);
            item._sheen.pivot = new Vector2(0.5f, 0.5f);
            item._sheen.sizeDelta = new Vector2(h * 2.2f, h * 1.2f);

            item._label = Ui.Text(root, "Texto", label, art.Title, fontSize, MenuTheme.TextDim, TextAnchor.MiddleCenter);
            Ui.Stretch(item._label.rectTransform);
            item._label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Shadow(item._label, 3f, 0.9f);
            item._glow = item._label.gameObject.AddComponent<Outline>();
            item._glow.effectDistance = new Vector2(1.5f, -1.5f);
            item._glow.effectColor = new Color(1f, 0.85f, 0.45f, 0f);

            item._ornLeftImg = Ui.Image(root, "Ornamento Esquerdo", art.OrnamentLeft, MenuTheme.GoldLight);
            item._ornLeft = item._ornLeftImg.rectTransform;
            item._ornRightImg = Ui.Image(root, "Ornamento Direito", art.OrnamentRight, MenuTheme.GoldLight);
            item._ornRight = item._ornRightImg.rectTransform;
            foreach (var r in new[] { item._ornLeft, item._ornRight })
            {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(20f * 2.5f, 9f * 2.5f);
            }
            return item;
        }

        /// <summary>Texto de valor à direita (linhas de configuração). O rótulo vai para a esquerda.</summary>
        public void AddValue(Font font, int size)
        {
            _label.alignment = TextAnchor.MiddleLeft;
            _label.rectTransform.offsetMin = new Vector2(36f, 0f);
            _value = Ui.Text(transform, "Valor", "", font, size, MenuTheme.Text, TextAnchor.MiddleRight);
            Ui.Stretch(_value.rectTransform);
            _value.rectTransform.offsetMax = new Vector2(-36f, 0f);
            _value.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Shadow(_value, 2f, 0.9f);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _pointerOver = true;
            if (_interactable) PointerEntered?.Invoke();
        }

        public void OnPointerExit(PointerEventData e) => _pointerOver = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (!_interactable) return;
            if (e.button == PointerEventData.InputButton.Right) ClickedBack?.Invoke();
            else if (e.button == PointerEventData.InputButton.Left) Clicked?.Invoke();
        }

        public void Press()
        {
            if (_interactable) Clicked?.Invoke();
        }

        private void OnDisable() => _pointerOver = false;

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var on = Highlighted && _interactable;
            if (on && !_wasOn) _sheenT = 0f;               // o reflexo atravessa ao entrar
            _wasOn = on;
            _t = Mathf.MoveTowards(_t, on ? 1f : 0f, dt * 6f);
            var e = _t * _t * (3f - 2f * _t);

            _pane.color = new Color(1f, 1f, 1f, 0.95f * e);
            _pane.rectTransform.localScale = new Vector3(Mathf.Lerp(0.82f, 1f, e), 1f, 1f);

            _sheenT = Mathf.MoveTowards(_sheenT, 1f, dt * 1.8f);
            var w = ((RectTransform)transform).rect.width;
            _sheen.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.2f, w * 1.2f, _sheenT), 0f);
            _sheen.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f * e * (1f - _sheenT * 0.6f));

            var baseColor = _interactable ? MenuTheme.TextDim : new Color(MenuTheme.TextMuted.r, MenuTheme.TextMuted.g, MenuTheme.TextMuted.b, 0.5f);
            _label.color = Color.Lerp(baseColor, new Color(1f, 0.97f, 0.88f), e);
            _glow.effectColor = new Color(1f, 0.85f, 0.45f, 0.45f * e);
            _label.transform.localScale = Vector3.one * (1f + 0.04f * e);

            // Ornamentos entram pelos lados, colados ao texto (ou à placa nas linhas de configuração).
            var textHalf = _value != null ? w * 0.5f - 6f : Mathf.Min(_label.preferredWidth * 0.5f + 46f, w * 0.5f);
            var slide = Mathf.Lerp(30f, 0f, e);
            _ornLeft.anchoredPosition = new Vector2(-textHalf - slide, 0f);
            _ornRight.anchoredPosition = new Vector2(textHalf + slide, 0f);
            var oc = new Color(MenuTheme.GoldLight.r, MenuTheme.GoldLight.g, MenuTheme.GoldLight.b, e);
            _ornLeftImg.color = oc;
            _ornRightImg.color = oc;
        }
    }

    /// <summary>Partículas douradas flutuando (como o brilho do menu de Hollow Knight).</summary>
    public sealed class GoldenMotes : MonoBehaviour
    {
        private RectTransform[] _motes;
        private Image[] _images;
        private Vector2[] _pos, _vel;
        private float[] _size, _phase, _speed;

        public static GoldenMotes Create(Transform parent, Sprite soft, int count)
        {
            var root = Ui.Stretch(Ui.Rect(parent, "Particulas"));
            var m = root.gameObject.AddComponent<GoldenMotes>();
            m._motes = new RectTransform[count];
            m._images = new Image[count];
            m._pos = new Vector2[count];
            m._vel = new Vector2[count];
            m._size = new float[count];
            m._phase = new float[count];
            m._speed = new float[count];
            for (var i = 0; i < count; i++)
            {
                m._images[i] = Ui.Image(root, "Brilho", soft, Color.clear);
                m._motes[i] = m._images[i].rectTransform;
                m._motes[i].anchorMin = m._motes[i].anchorMax = Vector2.zero;
                m.Spawn(i, true);
            }
            return m;
        }

        private void Spawn(int i, bool anywhere)
        {
            _pos[i] = new Vector2(UnityEngine.Random.value, anywhere ? UnityEngine.Random.value : -0.05f);
            _vel[i] = new Vector2(UnityEngine.Random.Range(-0.006f, 0.006f), UnityEngine.Random.Range(0.008f, 0.03f));
            var big = UnityEngine.Random.value < 0.18f;
            _size[i] = big ? UnityEngine.Random.Range(18f, 34f) : UnityEngine.Random.Range(5f, 13f);
            _phase[i] = UnityEngine.Random.value * 10f;
            _speed[i] = UnityEngine.Random.Range(0.6f, 1.8f);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var size = ((RectTransform)transform).rect.size;
            var time = Time.unscaledTime;
            for (var i = 0; i < _motes.Length; i++)
            {
                _pos[i] += (_vel[i] + new Vector2(Mathf.Sin(time * 0.4f + _phase[i]) * 0.004f, 0f)) * dt;
                if (_pos[i].y > 1.05f || _pos[i].x < -0.05f || _pos[i].x > 1.05f) Spawn(i, false);
                var twinkle = 0.55f + 0.45f * Mathf.Sin(time * _speed[i] + _phase[i]);
                var edge = Mathf.Clamp01(_pos[i].y * 6f) * Mathf.Clamp01((1.05f - _pos[i].y) * 6f);
                var big = _size[i] > 16f;
                _images[i].color = new Color(1f, 0.78f, 0.38f, (big ? 0.22f : 0.75f) * twinkle * edge);
                _motes[i].anchoredPosition = new Vector2(_pos[i].x * size.x, _pos[i].y * size.y);
                _motes[i].sizeDelta = Vector2.one * _size[i];
            }
        }
    }

    /// <summary>
    /// Fundo do menu: parede de pedra escura com um grande vitral gótico; pela
    /// rosácea aparece a lua e pelas lancetas o céu e as nuvens em movimento.
    /// </summary>
    public sealed class StainedGlassBackground : MonoBehaviour
    {
        // Vitral: arte de 240×330 px ampliada 3× (pixel art, como a lua e as nuvens).
        public const float WindowW = 720f, WindowH = 990f, WindowTop = 45f;
        private const float RoseX = 120f * 3f, RoseY = 92f * 3f, MoonD = 140f;

        private RawImage _clouds, _shadow;
        private Image _moonGlow;

        public static StainedGlassBackground Create(Transform canvas, MenuArt art, Texture wall, Texture sky, Texture moon,
            Texture shadow, Texture clouds, Texture glass, Texture frame, Texture mask)
        {
            var root = Ui.Stretch(Ui.Rect(canvas, "Fundo"));
            var bg = root.gameObject.AddComponent<StainedGlassBackground>();

            var wallImg = Ui.Stretch(Ui.Rect(root, "Parede")).gameObject.AddComponent<RawImage>();
            wallImg.texture = wall;
            wallImg.color = wall != null ? Color.white : new Color(0.08f, 0.07f, 0.08f);
            wallImg.raycastTarget = false;

            // Luar espalhado na parede em volta do vitral
            var halo = Ui.Image(root, "Luar", art.SoftCircle, new Color(0.55f, 0.65f, 0.9f, 0.22f));
            Ui.Center(halo.rectTransform, 960f, WindowTop + WindowH * 0.42f, 1500f, 1300f);

            var cx = 960f;
            var left = cx - WindowW * 0.5f;
            var window = Ui.TopLeft(Ui.Rect(root, "Vitral"), left, WindowTop, WindowW, WindowH);

            // Céu visto pela janela (recortado no formato do vitral)
            var maskImg = Ui.Image(window, "Formato", SpriteOf(mask), Color.white);
            Ui.Stretch(maskImg.rectTransform);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var sky1 = Raw(maskImg.transform, "Ceu", sky);
            Ui.Center(sky1.rectTransform, WindowW * 0.5f, WindowH * 0.5f, WindowH * 16f / 9f, WindowH);
            var moonImg = Raw(maskImg.transform, "Lua", moon);
            // Recorte só do disco da lua (centro 283,141 numa arte de 576×324)
            moonImg.uvRect = new Rect((283f - 80f) / 576f, 1f - (141f + 80f) / 324f, 160f / 576f, 160f / 324f);
            Ui.Center(moonImg.rectTransform, RoseX, RoseY, MoonD, MoonD);
            bg._moonGlow = Ui.Image(maskImg.transform, "Brilho da Lua", art.SoftCircle, new Color(0.85f, 0.9f, 1f, 0.35f));
            Ui.Center(bg._moonGlow.rectTransform, RoseX, RoseY, MoonD * 2.4f, MoonD * 2.4f);
            var cloudH = WindowH * 1.1f;
            bg._shadow = Raw(maskImg.transform, "Sombra", shadow);
            Ui.TopLeft(bg._shadow.rectTransform, WindowW * 0.5f - cloudH * 16f / 18f, WindowH - cloudH, cloudH * 16f / 9f, cloudH);
            bg._clouds = Raw(maskImg.transform, "Nuvens", clouds);
            Ui.TopLeft(bg._clouds.rectTransform, WindowW * 0.5f - cloudH * 16f / 18f, WindowH - cloudH, cloudH * 16f / 9f, cloudH);

            var glassImg = Raw(window, "Vidro", glass);
            Ui.Stretch(glassImg.rectTransform);
            var frameImg = Raw(window, "Moldura de Pedra", frame);
            Ui.Stretch(frameImg.rectTransform);

            // Luz colorida do vitral caindo no chão
            var floor = Ui.Image(root, "Luz no Chao", art.SoftCircle, new Color(0.75f, 0.55f, 0.7f, 0.18f));
            Ui.Center(floor.rectTransform, 960f, 1060f, 1300f, 220f);

            // Escurece a parte de baixo e as laterais para o texto ler bem
            var bottom = Ui.Image(root, "Sombra Inferior", art.FadeUp, new Color(0f, 0f, 0f, 0.72f));
            bottom.rectTransform.anchorMin = new Vector2(0f, 0f);
            bottom.rectTransform.anchorMax = new Vector2(1f, 0.62f);
            bottom.rectTransform.offsetMin = bottom.rectTransform.offsetMax = Vector2.zero;
            var l = Ui.Image(root, "Vinheta Esquerda", art.FadeLeft, new Color(0f, 0f, 0f, 0.7f));
            l.rectTransform.anchorMin = new Vector2(0f, 0f);
            l.rectTransform.anchorMax = new Vector2(0.28f, 1f);
            l.rectTransform.offsetMin = l.rectTransform.offsetMax = Vector2.zero;
            var r = Ui.Image(root, "Vinheta Direita", art.FadeLeft, new Color(0f, 0f, 0f, 0.7f));
            r.rectTransform.anchorMin = new Vector2(0.72f, 0f);
            r.rectTransform.anchorMax = new Vector2(1f, 1f);
            r.rectTransform.offsetMin = r.rectTransform.offsetMax = Vector2.zero;
            r.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            return bg;
        }

        private static RawImage Raw(Transform parent, string name, Texture tex)
        {
            var img = Ui.Rect(parent, name).gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            img.enabled = tex != null;
            return img;
        }

        private static Sprite SpriteOf(Texture tex) =>
            tex is Texture2D t ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f) : null;

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            Scroll(_clouds, 0.012f * dt);
            Scroll(_shadow, 0.004f * dt);
            if (_moonGlow != null)
                _moonGlow.color = new Color(0.85f, 0.9f, 1f, 0.3f + 0.06f * Mathf.Sin(Time.unscaledTime * 0.7f));
        }

        private static void Scroll(RawImage img, float amount)
        {
            if (img == null || !img.enabled) return;
            var r = img.uvRect;
            r.x = Mathf.Repeat(r.x + amount, 1f);
            img.uvRect = r;
        }
    }
}
