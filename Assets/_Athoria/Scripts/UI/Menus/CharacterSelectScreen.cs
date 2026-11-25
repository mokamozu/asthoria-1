using System.Collections.Generic;
using System.Text;
using Athoria.Gameplay.Characters;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Athoria.UI.Menus
{
    /// <summary>
    /// Seleção de cavaleira: um pergaminho com faixas verticais que giram como
    /// uma roleta (setas ‹ ›, ← → ou clique numa faixa trazem a cavaleira para o
    /// centro). Ao passar o mouse, a faixa brilha e a personagem anima para a pose
    /// alternativa (os pares normal/alternativo do Asthoria antigo). Enter ou JOGAR começa.
    /// </summary>
    public sealed class CharacterSelectScreen : MonoBehaviour
    {
        [SerializeField] private CharacterDatabase database;
        [SerializeField] private string gameScene = "TerrasAlagadas";
        [SerializeField] private string menuScene = "MenuPrincipal";

        [Header("Arte")]
        [SerializeField] private Texture2D uiTiles;
        [SerializeField] private Texture2D glassPane;
        [SerializeField] private Texture2D glassSheen;
        [SerializeField, Tooltip("Pétalas caindo por cima da tela (PixelFX weather/cherry-blossoms).")]
        private Gameplay.Effects.SpriteFxAnimation petals;

        [Header("Sons (pergaminho e roleta)")]
        [SerializeField] private AudioClip[] glassHoverSounds = System.Array.Empty<AudioClip>();
        [SerializeField] private AudioClip confirmSound;
        [SerializeField] private AudioClip backSound;
        [SerializeField] private AudioClip changeSound;

        private sealed class Strip
        {
            public CharacterData Data;
            public RectTransform Root;
            public RectTransform Art;
            public Image Normal, Alternate, Flash, Dim, GlowLeft, GlowRight, Frame;
            public Image EmblemRing;
            public RectTransform NameTag;
            public CanvasGroup NameGroup;
            public UiPointer Pointer;
            public float Width;
            public float Focus;        // 0..1: animação da pose alternativa
            public float FlashT;
            public bool WasFocused;
        }

        // Pergaminho (pixels de 1920×1080 a partir do canto superior esquerdo)
        private const float ScrollX = 110f, ScrollY = 118f, ScrollW = 1700f, ScrollH = 690f;
        private const float StripsX = 160f, StripsY = 160f, StripsW = 1600f, StripsH = 606f;
        private const float Gap = 8f;
        /// <summary>Faixas visíveis de uma vez; com mais cavaleiras a roleta gira.</summary>
        private const int Visible = 5;

        private float _scroll, _scrollTarget;
        private bool Carousel => _strips.Count > Visible;
        private Image _arrowLeft, _arrowRight;
        private UiPointer _arrowLeftPointer, _arrowRightPointer;

        private MenuArt _art;
        private ScreenFader _fader;
        private RectTransform _canvas;
        private readonly List<Strip> _strips = new();
        private int _selected;
        private int _focused = -1;
        private int _shownInfo = -1;
        private float _infoAlpha = 1f;

        private GlassMenuItem _play, _back;
        private CanvasGroup _infoGroup;
        private Text _name, _title, _classRegion, _description, _stats, _ability;
        private Image _infoEmblem, _accentLine;

        private void Awake()
        {
            var module = FindAnyObjectByType<InputSystemUIInputModule>();
            if (module != null && (module.actionsAsset == null || module.point == null || module.leftClick == null))
                module.AssignDefaultActions();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            var hover = glassHoverSounds.Length > 0 ? glassHoverSounds[0] : null;
            MenuSounds.Register(hover, changeSound, confirmSound, backSound, changeSound);
            MenuSounds.RegisterHoverVariants(glassHoverSounds);
            _art = MenuArt.Create(uiTiles);
            _canvas = (RectTransform)transform;

            var characters = new List<CharacterData>();
            if (database != null)
                foreach (var c in database.Characters)
                    if (c != null) characters.Add(c);

            Build(characters);
            if (_strips.Count == 0)
            {
                Debug.LogError("[Athoria] Banco de personagens vazio.", this);
                return;
            }
            var last = PlayerSession.SelectedCharacter != null ? characters.IndexOf(PlayerSession.SelectedCharacter) : -1;
            _selected = Mathf.Max(0, last);
            _scroll = _scrollTarget = Carousel ? _selected - Visible / 2 : 0f;
            _arrowLeft.gameObject.SetActive(Carousel);
            _arrowRight.gameObject.SetActive(Carousel);
            ShowInfo(_selected, true);
            Layout(1f);
        }

        // ================= Montagem =================

        private void Build(List<CharacterData> characters)
        {
            var bg = Ui.Stretch(Ui.Rect(_canvas, "Fundo")).gameObject.AddComponent<RawImage>();
            bg.texture = Gradient(new Color32(10, 8, 12, 255), new Color32(22, 16, 20, 255));
            bg.raycastTarget = false;
            Ui.ScreenFrame(_canvas, 0.5f);

            var heading = Ui.Text(_canvas, "Titulo", "ESCOLHA SUA CAVALEIRA", _art.Title, 40, MenuTheme.GoldLight, TextAnchor.MiddleCenter);
            Ui.TopLeft(heading.rectTransform, 0f, 30f, 1920f, 56f);
            Ui.Shadow(heading, 4f);
            var hint = Ui.Text(_canvas, "Dica", "passe o mouse para conhecer  •  clique ou ‹ › para girar  •  ← →  e  ENTER",
                _art.Body, 16, MenuTheme.TextDim, TextAnchor.MiddleCenter);
            Ui.TopLeft(hint.rectTransform, 0f, 84f, 1920f, 24f);

            // Pergaminho
            var shadowScroll = Ui.Box(_canvas, "Sombra do Pergaminho", new Color(0f, 0f, 0f, 0.55f));
            Ui.TopLeft(shadowScroll.rectTransform, ScrollX + 10f, ScrollY + 12f, ScrollW, ScrollH);
            var scroll = Ui.Image(_canvas, "Pergaminho", _art.Parchment, Color.white, true);
            Ui.TopLeft(scroll.rectTransform, ScrollX, ScrollY, ScrollW, ScrollH);
            var ink = new Color(0.36f, 0.24f, 0.13f, 0.75f);
            var border = Ui.Rect(_canvas, "Borda Interna");
            Ui.TopLeft(border, ScrollX + 22f, ScrollY + 22f, ScrollW - 44f, ScrollH - 44f);
            Ui.Outline(border, 0f, 3f, ink);
            Ui.Outline(border, 8f, 1f, new Color(ink.r, ink.g, ink.b, 0.45f));

            var stripsRoot = Ui.Rect(_canvas, "Faixas");
            Ui.TopLeft(stripsRoot, StripsX, StripsY, StripsW, StripsH);
            stripsRoot.gameObject.AddComponent<RectMask2D>();   // faixas que saem da roleta somem na borda
            foreach (var c in characters) _strips.Add(BuildStrip(stripsRoot, c, _strips.Count));

            // Setas da roleta, fora do pergaminho
            (_arrowLeft, _arrowLeftPointer) = Arrow("Seta Esquerda", ScrollX - 52f, false);
            (_arrowRight, _arrowRightPointer) = Arrow("Seta Direita", ScrollX + ScrollW + 52f, true);
            _arrowLeftPointer.Clicked += () => Step(-1);
            _arrowRightPointer.Clicked += () => Step(1);

            BuildInfo();

            // Partículas douradas e pétalas do ipê caindo (por cima do pergaminho, atrás dos textos)
            GoldenMotes.Create(_canvas, _art.SoftCircle, 28);
            if (petals != null)
            {
                var anim = UiSpriteAnimator.Create(_canvas, "Petalas do Ipe", petals, new Color(1f, 1f, 1f, 0.7f));
                Ui.Stretch((RectTransform)anim.transform);
            }
            _infoGroup.transform.SetAsLastSibling();   // textos por cima das pétalas

            var pane = glassPane != null
                ? Sprite.Create(glassPane, new Rect(0, 0, glassPane.width, glassPane.height), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3))
                : _art.Button;
            var sheen = glassSheen != null
                ? Sprite.Create(glassSheen, new Rect(0, 0, glassSheen.width, glassSheen.height), new Vector2(0.5f, 0.5f), 100f)
                : _art.SoftCircle;
            _play = GlassMenuItem.Create(_canvas, _art, pane, sheen, "JOGAR", 1740f, 1030f, 300f, 56f, 32);
            _play.Clicked += Play;
            _play.PointerEntered += () => MenuSounds.Play(MenuSounds.Kind.Hover, 0.5f);
            _back = GlassMenuItem.Create(_canvas, _art, pane, sheen, "VOLTAR", 180f, 1030f, 260f, 56f, 28);
            _back.Clicked += Back;
            _back.PointerEntered += () => MenuSounds.Play(MenuSounds.Kind.Hover, 0.5f);

            _fader = ScreenFader.Create(_canvas);
        }

        private Strip BuildStrip(RectTransform parent, CharacterData c, int index)
        {
            var s = new Strip { Data = c };
            s.Root = Ui.Rect(parent, "Faixa " + c.DisplayName);
            s.Root.anchorMin = new Vector2(0f, 0f);
            s.Root.anchorMax = new Vector2(0f, 1f);
            s.Root.pivot = new Vector2(0f, 0.5f);
            s.Root.offsetMin = new Vector2(0f, 0f);
            s.Root.offsetMax = new Vector2(0f, 0f);

            // Fundo de pergaminho mais claro com um toque da cor da personagem
            var paper = Ui.Box(s.Root, "Papel", Color.Lerp(new Color(0.93f, 0.86f, 0.71f), c.AccentColor, 0.12f));
            Ui.Stretch(paper.rectTransform);
            paper.raycastTarget = true;
            s.Pointer = paper.gameObject.AddComponent<UiPointer>();
            s.Pointer.Clicked += () => Choose(index);

            var clip = Ui.Stretch(Ui.Rect(s.Root, "Recorte"));
            clip.gameObject.AddComponent<RectMask2D>();

            // Arte: quadrada, encostada embaixo (os bustos vão até a borda inferior)
            s.Art = Ui.Rect(clip, "Arte");
            s.Art.anchorMin = s.Art.anchorMax = new Vector2(0.5f, 0f);
            s.Art.pivot = new Vector2(0.5f, 0f);
            var size = StripsH * 1.22f;
            s.Art.sizeDelta = new Vector2(size, size);
            s.Art.anchoredPosition = new Vector2(size * 0.03f, -6f);
            s.Normal = Ui.Image(s.Art, "Pose Normal", c.Portrait, Color.white);
            Ui.Stretch(s.Normal.rectTransform);
            s.Alternate = Ui.Image(s.Art, "Pose Alternativa", c.PortraitAlternate, new Color(1f, 1f, 1f, 0f));
            Ui.Stretch(s.Alternate.rectTransform);

            // Pé da faixa escurecido para o brasão aparecer
            var foot = Ui.Image(clip, "Sombra Inferior", null, new Color(0.12f, 0.07f, 0.04f, 0.55f));
            foot.rectTransform.anchorMin = new Vector2(0f, 0f);
            foot.rectTransform.anchorMax = new Vector2(1f, 0f);
            foot.rectTransform.pivot = new Vector2(0.5f, 0f);
            foot.rectTransform.sizeDelta = new Vector2(0f, 120f);
            foot.sprite = VerticalFade();

            s.Dim = Ui.Box(clip, "Escurecer", new Color(0.08f, 0.05f, 0.03f, 0f));
            Ui.Stretch(s.Dim.rectTransform);
            s.Flash = Ui.Box(clip, "Clarão", new Color(1f, 0.95f, 0.8f, 0f));
            Ui.Stretch(s.Flash.rectTransform);

            // Brilho dourado nas laterais (como na referência) e moldura
            s.GlowLeft = SideGlow(clip, true);
            s.GlowRight = SideGlow(clip, false);
            s.Frame = Ui.Image(s.Root, "Moldura", null, new Color(0f, 0f, 0f, 0f));
            Ui.Stretch(s.Frame.rectTransform);
            var outline = Ui.Rect(s.Root, "Contorno");
            Ui.Stretch(outline);
            Ui.Outline(outline, 0f, 2f, new Color(0.3f, 0.19f, 0.1f, 0.9f));

            // Brasão da região embaixo (como os ícones de elemento da referência)
            var emblemRoot = Ui.Rect(s.Root, "Brasão");
            emblemRoot.anchorMin = emblemRoot.anchorMax = new Vector2(0.5f, 0f);
            emblemRoot.pivot = new Vector2(0.5f, 0f);
            emblemRoot.anchoredPosition = new Vector2(0f, 20f);
            emblemRoot.sizeDelta = new Vector2(82f, 82f);
            var emblemShadow = Ui.Image(emblemRoot, "Sombra", _art.Circle, new Color(0f, 0f, 0f, 0.5f));
            Ui.Stretch(emblemShadow.rectTransform, -4f);
            emblemShadow.rectTransform.anchoredPosition = new Vector2(2f, -3f);
            s.EmblemRing = Ui.Image(emblemRoot, "Aro", _art.Circle, MenuTheme.Gold);
            Ui.Stretch(s.EmblemRing.rectTransform, -4f);
            var emblemBack = Ui.Image(emblemRoot, "Fundo", _art.Circle, Color.Lerp(new Color(0.1f, 0.08f, 0.08f), c.AccentColor, 0.35f));
            Ui.Stretch(emblemBack.rectTransform);
            var emblem = Ui.Image(emblemRoot, "Imagem", c.RegionEmblem, Color.white);
            emblem.preserveAspect = true;
            Ui.Stretch(emblem.rectTransform, 10f);
            emblem.enabled = c.RegionEmblem != null;

            // Nome que aparece no destaque
            s.NameTag = Ui.Rect(s.Root, "Nome");
            s.NameTag.anchorMin = s.NameTag.anchorMax = new Vector2(0.5f, 0f);
            s.NameTag.pivot = new Vector2(0.5f, 0f);
            s.NameTag.anchoredPosition = new Vector2(0f, 112f);
            s.NameTag.sizeDelta = new Vector2(320f, 40f);
            s.NameGroup = s.NameTag.gameObject.AddComponent<CanvasGroup>();
            s.NameGroup.blocksRaycasts = false;
            var bar = Ui.Image(s.NameTag, "Faixa", _art.GoldBar, Color.white, true);
            Ui.Stretch(bar.rectTransform);
            var label = Ui.Text(s.NameTag, "Texto", c.DisplayName.ToUpperInvariant(), _art.Title, 20, MenuTheme.GoldLight, TextAnchor.MiddleCenter);
            Ui.Stretch(label.rectTransform);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Shadow(label, 2f);
            return s;
        }

        private (Image, UiPointer) Arrow(string name, float centerX, bool right)
        {
            var root = Ui.Rect(_canvas, name);
            Ui.Center(root, centerX, ScrollY + ScrollH * 0.5f, 70f, 110f);
            var hit = Ui.Box(root, "Area", new Color(0f, 0f, 0f, 0f));
            Ui.Stretch(hit.rectTransform);
            hit.raycastTarget = true;
            var shadow = Ui.Image(root, "Sombra", _art.Triangle, new Color(0f, 0f, 0f, 0.6f));
            Ui.Stretch(shadow.rectTransform, 14f);
            shadow.rectTransform.anchoredPosition = new Vector2(3f, -4f);
            var img = Ui.Image(root, "Seta", _art.Triangle, MenuTheme.Gold);
            Ui.Stretch(img.rectTransform, 14f);
            if (!right)
            {
                shadow.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }
            var pointer = hit.gameObject.AddComponent<UiPointer>();
            return (img, pointer);
        }

        private static Image SideGlow(RectTransform parent, bool left)
        {
            var img = Ui.Image(parent, left ? "Brilho Esquerdo" : "Brilho Direito", HorizontalFade(left), new Color(1f, 0.82f, 0.4f, 0f));
            var r = img.rectTransform;
            r.anchorMin = new Vector2(left ? 0f : 1f, 0f);
            r.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            r.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            r.sizeDelta = new Vector2(46f, 0f);
            return img;
        }

        private void BuildInfo()
        {
            var root = Ui.Rect(_canvas, "Informacoes");
            Ui.TopLeft(root, 0f, 0f, 1920f, 1080f);
            _infoGroup = root.gameObject.AddComponent<CanvasGroup>();
            _infoGroup.blocksRaycasts = false;
            const float y = 826f;

            var emblemRing = Ui.Image(root, "Aro", _art.Circle, MenuTheme.Gold);
            Ui.TopLeft(emblemRing.rectTransform, 160f, y + 4f, 104f, 104f);
            var emblemBack = Ui.Image(root, "Fundo", _art.Circle, new Color32(18, 12, 16, 255));
            Ui.TopLeft(emblemBack.rectTransform, 164f, y + 8f, 96f, 96f);
            _infoEmblem = Ui.Image(root, "Brasão", null, Color.white);
            _infoEmblem.preserveAspect = true;
            Ui.TopLeft(_infoEmblem.rectTransform, 174f, y + 18f, 76f, 76f);

            _name = Ui.Text(root, "Nome", "", _art.Title, 38, MenuTheme.GoldLight);
            Ui.TopLeft(_name.rectTransform, 290f, y, 640f, 48f);
            _name.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Shadow(_name);
            _title = Ui.Text(root, "Titulo", "", _art.Body, 19, MenuTheme.Text);
            _title.fontStyle = FontStyle.Italic;
            Ui.TopLeft(_title.rectTransform, 292f, y + 50f, 640f, 26f);
            _accentLine = Ui.Box(root, "Filete", MenuTheme.Gold);
            Ui.TopLeft(_accentLine.rectTransform, 292f, y + 82f, 560f, 2f);
            _classRegion = Ui.Text(root, "Classe e Regiao", "", _art.Title, 16, MenuTheme.Accent);
            Ui.TopLeft(_classRegion.rectTransform, 292f, y + 90f, 640f, 24f);
            _stats = Ui.Text(root, "Valores", "", _art.Body, 16, MenuTheme.TextDim);
            Ui.TopLeft(_stats.rectTransform, 292f, y + 116f, 640f, 24f);

            var divider = Ui.Box(root, "Divisor", new Color(MenuTheme.GoldDark.r, MenuTheme.GoldDark.g, MenuTheme.GoldDark.b, 0.6f));
            Ui.TopLeft(divider.rectTransform, 950f, y + 6f, 1f, 130f);
            _description = Ui.Text(root, "Descricao", "", _art.Body, 17, MenuTheme.Text);
            Ui.TopLeft(_description.rectTransform, 975f, y, 790f, 84f);
            _ability = Ui.Text(root, "Habilidade", "", _art.Body, 16, MenuTheme.Text);
            Ui.TopLeft(_ability.rectTransform, 975f, y + 90f, 790f, 50f);
        }

        // ================= Lógica =================

        private void Choose(int index)
        {
            if (index == _selected) return;
            var n = _strips.Count;
            // menor caminho na roleta (gira para o lado mais perto)
            var delta = ((index - _selected) % n + n) % n;
            if (delta > n / 2) delta -= n;
            Step(delta);
        }

        private void Step(int delta)
        {
            if (_strips.Count == 0 || delta == 0) return;
            _selected = ((_selected + delta) % _strips.Count + _strips.Count) % _strips.Count;
            if (Carousel) _scrollTarget += delta;
            MenuSounds.Play(MenuSounds.Kind.Change, 0.75f);
        }

        private void Update()
        {
            if (_strips.Count == 0) return;
            var dt = Time.unscaledDeltaTime;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Step(1);
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Step(-1);
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Play();
                if (kb.escapeKey.wasPressedThisFrame) Back();
            }

            // Em foco: a faixa sob o mouse; sem mouse em cima, a escolhida.
            var hovered = -1;
            for (var i = 0; i < _strips.Count; i++)
                if (_strips[i].Pointer.Over && _strips[i].Root.gameObject.activeInHierarchy) hovered = i;
            var focus = hovered >= 0 ? hovered : _selected;
            if (focus != _focused)
            {
                if (hovered >= 0 && _focused >= 0) MenuSounds.Play(MenuSounds.Kind.Hover, 0.45f);
                _focused = focus;
            }

            // Informações acompanham o foco, com um fade curto.
            if (focus != _shownInfo)
            {
                _infoAlpha = Mathf.MoveTowards(_infoAlpha, 0f, dt * 10f);
                if (_infoAlpha <= 0f) ShowInfo(focus, false);
            }
            else _infoAlpha = Mathf.MoveTowards(_infoAlpha, 1f, dt * 6f);
            _infoGroup.alpha = _infoAlpha;

            _play.Highlighted = _play.IsPointerOver;
            _back.Highlighted = _back.IsPointerOver;
            Layout(dt);
        }

        private void Layout(float dt)
        {
            var n = _strips.Count;
            var visible = Mathf.Min(Visible, n);
            var slotW = (StripsW - Gap * (visible - 1)) / visible;
            _scroll = Mathf.Lerp(_scroll, _scrollTarget, 1f - Mathf.Exp(-9f * dt));
            if (Mathf.Abs(_scroll - _scrollTarget) < 0.001f) _scroll = _scrollTarget;

            // Setas: acendem com o mouse em cima
            if (_arrowLeft != null)
            {
                var pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.4f);
                _arrowLeft.color = _arrowLeftPointer.Over ? MenuTheme.GoldLight : MenuTheme.Gold * new Color(pulse, pulse, pulse, 1f);
                _arrowRight.color = _arrowRightPointer.Over ? MenuTheme.GoldLight : MenuTheme.Gold * new Color(pulse, pulse, pulse, 1f);
                _arrowLeft.transform.parent.localScale = Vector3.one * (_arrowLeftPointer.Over ? 1.12f : 1f);
                _arrowRight.transform.parent.localScale = Vector3.one * (_arrowRightPointer.Over ? 1.12f : 1f);
            }

            for (var i = 0; i < n; i++)
            {
                var s = _strips[i];
                // posição na roleta: 0 = primeira faixa visível; -1..0 entrando pela esquerda
                var r = Carousel ? Mathf.Repeat(i - _scroll + 1f, n) - 1f : i;
                var shown = r > -1f && r < visible;
                if (s.Root.gameObject.activeSelf != shown) s.Root.gameObject.SetActive(shown);
                s.Width = slotW;
                s.Root.anchoredPosition = new Vector2(r * (slotW + Gap), 0f);
                s.Root.sizeDelta = new Vector2(slotW, 0f);

                // Animação de pose, suave: a arte escurece de leve, troca de pose e volta.
                var focused = i == _focused;
                if (focused != s.WasFocused) s.FlashT = 1f;
                s.WasFocused = focused;
                s.Focus = Mathf.MoveTowards(s.Focus, focused ? 1f : 0f, dt * 3f);
                s.FlashT = Mathf.MoveTowards(s.FlashT, 0f, dt * 3f);
                var e = s.Focus * s.Focus * (3f - 2f * s.Focus);

                // Troca de imagem no meio da transição (as poses não se alinham, então não se misturam).
                var showAlternate = s.Focus >= 0.5f;
                s.Normal.enabled = !showAlternate;
                s.Alternate.enabled = showAlternate;
                var dip = 1f - 0.45f * Mathf.Sin(s.FlashT * Mathf.PI);
                var artColor = new Color(dip, dip, dip, 1f);
                s.Normal.color = artColor;
                s.Alternate.color = artColor;

                var breathe = 1f + Mathf.Sin(Time.unscaledTime * 1.2f + i) * 0.004f;
                s.Art.localScale = Vector3.one * ((1f + 0.025f * e) * breathe);
                s.Flash.color = Color.clear;
                s.Dim.color = new Color(0.08f, 0.05f, 0.03f, 0.14f * (1f - e));
                var glow = new Color(1f, 0.82f, 0.45f, 0.28f * e);
                s.GlowLeft.color = glow;
                s.GlowRight.color = glow;

                var chosen = i == _selected;
                s.EmblemRing.color = chosen ? MenuTheme.GoldLight : Color.Lerp(new Color32(102, 92, 76, 255), MenuTheme.Gold, e);
                s.NameGroup.alpha = Mathf.Max(e, chosen ? 0.85f : 0f);
            }
        }

        private void ShowInfo(int index, bool instant)
        {
            _shownInfo = index;
            if (instant) _infoAlpha = 1f;
            var c = _strips[index].Data;
            _infoEmblem.sprite = c.RegionEmblem;
            _infoEmblem.enabled = c.RegionEmblem != null;
            _name.text = c.DisplayName.ToUpperInvariant();
            _title.text = c.Title;
            _title.color = Color.Lerp(c.AccentColor, Color.white, 0.3f);
            _accentLine.color = new Color(c.AccentColor.r, c.AccentColor.g, c.AccentColor.b, 0.7f);
            _classRegion.text = $"{c.CharacterClass}  •  {c.Region}".ToUpperInvariant();
            _description.text = c.Description;

            var speed = 100f;
            if (c.Traits != null) foreach (var t in c.Traits) if (t.effect == TraitEffect.MoveSpeedPercent) speed += t.value;
            var mana = c.BaseMana > 0f ? c.BaseMana.ToString("0") : "—";
            var weapons = new StringBuilder();
            if (c.StartingWeapons != null)
                foreach (WeaponData w in c.StartingWeapons)
                    if (w != null) weapons.Append(weapons.Length > 0 ? ", " : "").Append(w.DisplayName);
            if (c.StartingOffHand != null) weapons.Append(weapons.Length > 0 ? ", " : "").Append(c.StartingOffHand.DisplayName);
            _stats.text = $"Vida <color=#7CCB80>{c.BaseHealth:0}</color>   Vigor <color=#E0B85A>{c.BaseStamina:0}</color>   " +
                          $"Mana <color=#8CB4E8>{mana}</color>   Velocidade {speed:0}%   •   {weapons}";

            var ab = c.Ability;
            _ability.text = ab != null ? $"<color=#E8CC80>{ab.DisplayName}  (Q)</color> — {ab.Description}" : "Sem habilidade especial";
        }

        // ================= Ações =================

        private void Play()
        {
            if (_strips.Count == 0) return;
            MenuSounds.Play(MenuSounds.Kind.Confirm);
            SaveSystem.BeginNewGame();
            PlayerSession.SelectedCharacter = _strips[_selected].Data;
            var scene = gameScene;
#if UNITY_EDITOR
            // No editor, volta para a cena que estava aberta ao apertar Play (ex.: Sandbox).
            var target = UnityEditor.SessionState.GetString("Athoria.PlayTarget", string.Empty);
            if (!string.IsNullOrEmpty(target) && Application.CanStreamedLevelBeLoaded(target)) scene = target;
#endif
            _fader.LoadScene(scene);
        }

        private void Back()
        {
            MenuSounds.Play(MenuSounds.Kind.Back);
            _fader.LoadScene(menuScene);
        }

        // ================= Texturas geradas =================

        private static Texture2D Gradient(Color32 top, Color32 bottom)
        {
            var tex = new Texture2D(1, 24, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var i = 0; i < 24; i++) tex.SetPixel(0, 23 - i, Color32.Lerp(top, bottom, i / 23f));
            tex.Apply();
            return tex;
        }

        private static Sprite s_vertical, s_left, s_right;

        /// <summary>Opaco embaixo, transparente em cima.</summary>
        private static Sprite VerticalFade()
        {
            if (s_vertical != null) return s_vertical;
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (var y = 0; y < 64; y++) tex.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - y / 63f, 1.6f)));
            tex.Apply();
            return s_vertical = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Brilho que sai da borda da faixa para dentro.</summary>
        private static Sprite HorizontalFade(bool fromLeft)
        {
            if (fromLeft && s_left != null) return s_left;
            if (!fromLeft && s_right != null) return s_right;
            var tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (var x = 0; x < 64; x++)
            {
                var d = fromLeft ? x / 63f : 1f - x / 63f;
                tex.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Pow(1f - d, 2.2f)));
            }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, 64, 1), new Vector2(0.5f, 0.5f), 100f);
            if (fromLeft) s_left = s;
            else s_right = s;
            return s;
        }
    }
}
