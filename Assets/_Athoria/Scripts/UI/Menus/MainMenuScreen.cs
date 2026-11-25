using System;
using System.Collections.Generic;
using Athoria.Core;
using Athoria.Gameplay.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Athoria.UI.Menus
{
    /// <summary>
    /// Menu principal minimalista (referência: Hollow Knight): um grande vitral
    /// gótico ao fundo — a lua na rosácea, céu e nuvens pelas lancetas —,
    /// título com ornamentos, itens de texto centralizados que ganham uma placa
    /// de vidro ao passar o mouse (com som de vidro) e brilhos dourados no ar.
    /// Jogar → (Continuar / Novo jogo) · Configurações · Sair.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [Header("Arte da interface")]
        [SerializeField] private Texture2D uiTiles;
        [SerializeField] private Texture2D glassPane;
        [SerializeField] private Texture2D glassSheen;

        [Header("Vitral e céu")]
        [SerializeField] private Texture wall;
        [SerializeField] private Texture windowGlass;
        [SerializeField] private Texture windowFrame;
        [SerializeField] private Texture windowMask;
        [SerializeField] private Texture sky;
        [SerializeField] private Texture moon;
        [SerializeField] private Texture shadow;
        [SerializeField] private Texture clouds;

        [Header("Sons")]
        [SerializeField] private AudioClip[] glassHoverSounds = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip confirmSound;
        [SerializeField] private AudioClip backSound;
        [SerializeField] private AudioClip changeSound;

        [Header("Cenas")]
        [SerializeField] private string selectionScene = "SelecaoPersonagem";

        private enum Page { Main, Play, Settings }

        private sealed class SettingRow
        {
            public GlassMenuItem Item;
            public Func<string> GetText;
            public Action<int> Change;
        }

        private const float MenuTop = 690f;
        private const float Gap = 58f;
        private const float ItemW = 440f;
        private const float ItemH = 54f;

        private MenuArt _art;
        private Sprite _pane, _sheen;
        private ScreenFader _fader;
        private RectTransform _canvas;
        private RectTransform _titleBlock;
        private readonly Dictionary<Page, RectTransform> _pages = new();
        private readonly Dictionary<Page, List<GlassMenuItem>> _items = new();
        private readonly List<SettingRow> _rows = new();
        private Page _page;
        private int _index;
        private string _continueSlot;
        private Text _continueInfo;
        private GlassMenuItem _continueItem;

        private void Awake()
        {
            var module = FindAnyObjectByType<InputSystemUIInputModule>();
            if (module != null && (module.actionsAsset == null || module.point == null || module.leftClick == null))
                module.AssignDefaultActions();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            var hover = glassHoverSounds.Length > 0 ? glassHoverSounds[0] : null;
            MenuSounds.Register(hover, confirmSound, confirmSound, backSound, changeSound);
            MenuSounds.RegisterHoverVariants(glassHoverSounds);

            _art = MenuArt.Create(uiTiles);
            _pane = glassPane != null
                ? Sprite.Create(glassPane, new Rect(0, 0, glassPane.width, glassPane.height), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3))
                : _art.Button;
            _sheen = glassSheen != null
                ? Sprite.Create(glassSheen, new Rect(0, 0, glassSheen.width, glassSheen.height), new Vector2(0.5f, 0.5f), 100f)
                : _art.SoftCircle;
            _canvas = (RectTransform)transform;
            Build();
            Show(Page.Main);
        }

        // ================= Montagem =================

        private void Build()
        {
            StainedGlassBackground.Create(_canvas, _art, wall, sky, moon, shadow, clouds, windowGlass, windowFrame, windowMask);
            GoldenMotes.Create(_canvas, _art.SoftCircle, 46);

            _titleBlock = Ui.Stretch(Ui.Rect(_canvas, "Titulo"));
            var glow = Ui.Image(_titleBlock, "Brilho do Titulo", _art.SoftCircle, new Color(1f, 0.8f, 0.45f, 0.16f));
            Ui.Center(glow.rectTransform, 960f, 510f, 1100f, 320f);
            if (_art.Fleuron != null)
            {
                var top = Ui.Image(_titleBlock, "Ornamento Superior", _art.Fleuron, MenuTheme.GoldLight);
                Ui.Center(top.rectTransform, 960f, 420f, 60f * 4f, 20f * 4f);
            }
            var title = Ui.Text(_titleBlock, "Nome", "ATHORIA", _art.Title, 124, new Color(1f, 0.96f, 0.86f), TextAnchor.MiddleCenter);
            Ui.Center(title.rectTransform, 960f, 510f, 1400f, 150f);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var titleGlow = title.gameObject.AddComponent<Outline>();
            titleGlow.effectColor = new Color(1f, 0.8f, 0.4f, 0.35f);
            titleGlow.effectDistance = new Vector2(2f, -2f);
            Ui.Shadow(title, 5f, 0.9f);
            if (_art.FleuronLow != null)
            {
                var bottom = Ui.Image(_titleBlock, "Ornamento Inferior", _art.FleuronLow, MenuTheme.GoldLight);
                Ui.Center(bottom.rectTransform, 960f, 600f, 60f * 5f, 20f * 5f);
            }

            var version = Ui.Text(_canvas, "Versao", "protótipo", _art.Body, 15, MenuTheme.TextMuted, TextAnchor.LowerLeft);
            Ui.TopLeft(version.rectTransform, 40f, 1030f, 400f, 30f);
            var hint = Ui.Text(_canvas, "Dica", "↑ ↓  •  ENTER  •  ESC", _art.Title, 15,
                new Color(MenuTheme.TextDim.r, MenuTheme.TextDim.g, MenuTheme.TextDim.b, 0.55f), TextAnchor.LowerRight);
            Ui.TopLeft(hint.rectTransform, 1480f, 1030f, 400f, 30f);

            BuildMainPage();
            BuildPlayPage();
            BuildSettingsPage();
            _fader = ScreenFader.Create(_canvas);
        }

        private RectTransform NewPage(Page page)
        {
            var root = Ui.Stretch(Ui.Rect(_canvas, "Pagina " + page));
            _pages[page] = root;
            _items[page] = new List<GlassMenuItem>();
            return root;
        }

        private GlassMenuItem AddItem(Page page, RectTransform root, string label, float y, float w, Action action, MenuSounds.Kind sound)
        {
            var item = GlassMenuItem.Create(root, _art, _pane, _sheen, label, 960f, y, w, ItemH, 32);
            var list = _items[page];
            var index = list.Count;
            list.Add(item);
            item.PointerEntered += () =>
            {
                if (_page != page || _index == index) return;
                _index = index;
                MenuSounds.Play(MenuSounds.Kind.Hover, 0.55f);
            };
            item.Clicked += () =>
            {
                MenuSounds.Play(sound);
                action();
            };
            return item;
        }

        private void BuildMainPage()
        {
            var root = NewPage(Page.Main);
            AddItem(Page.Main, root, "JOGAR", MenuTop, ItemW, OnPlay, MenuSounds.Kind.Confirm);
            AddItem(Page.Main, root, "CONFIGURAÇÕES", MenuTop + Gap, ItemW, () => Show(Page.Settings), MenuSounds.Kind.Confirm);
            AddItem(Page.Main, root, "SAIR", MenuTop + Gap * 2f, ItemW, Quit, MenuSounds.Kind.Back);
        }

        private void BuildPlayPage()
        {
            var root = NewPage(Page.Play);
            _continueItem = AddItem(Page.Play, root, "CONTINUAR", MenuTop, ItemW, Continue, MenuSounds.Kind.Confirm);
            AddItem(Page.Play, root, "NOVO JOGO", MenuTop + Gap, ItemW, NewGame, MenuSounds.Kind.Confirm);
            AddItem(Page.Play, root, "VOLTAR", MenuTop + Gap * 2f, ItemW, () => Show(Page.Main), MenuSounds.Kind.Back);
            _continueInfo = Ui.Text(root, "Info do Save", "", _art.Body, 18, MenuTheme.TextDim, TextAnchor.UpperCenter);
            Ui.Center(_continueInfo.rectTransform, 960f, MenuTop + Gap * 3f + 18f, 900f, 56f);
        }

        private void BuildSettingsPage()
        {
            var root = NewPage(Page.Settings);
            var header = Ui.Text(root, "Titulo", "CONFIGURAÇÕES", _art.Title, 48, new Color(1f, 0.96f, 0.86f), TextAnchor.MiddleCenter);
            Ui.Center(header.rectTransform, 960f, 470f, 900f, 64f);
            Ui.Shadow(header, 4f, 0.9f);
            if (_art.FleuronLow != null)
            {
                var orn = Ui.Image(root, "Ornamento", _art.FleuronLow, MenuTheme.GoldLight);
                Ui.Center(orn.rectTransform, 960f, 528f, 60f * 4f, 20f * 4f);
            }

            AddRow(root, 0, "Resolução", () => GameSettings.ResolutionLabel, GameSettings.StepResolution);
            AddRow(root, 1, "Tela cheia", () => GameSettings.Fullscreen ? "Sim" : "Não", _ => GameSettings.ToggleFullscreen());
            AddRow(root, 2, "Qualidade gráfica", () => GameSettings.QualityLabel, GameSettings.StepQuality);
            AddRow(root, 3, "Estilo PS1 / N64", () => GameSettings.PixelLabel, GameSettings.StepPixel);
            AddRow(root, 4, "Volume geral", () => $"{GameSettings.MasterVolume * 100f:0}%", GameSettings.StepVolume);
            AddRow(root, 5, "Sensibilidade do mouse", () => $"{GameSettings.MouseSensitivity:0.0}×", GameSettings.StepMouse);
            AddItem(Page.Settings, root, "VOLTAR", 600f + Gap * 6f + 16f, ItemW, () => Show(Page.Main), MenuSounds.Kind.Back);

            var note = Ui.Text(root, "Nota", "clique ou → para avançar  •  botão direito ou ← para voltar", _art.Body, 15,
                MenuTheme.TextMuted, TextAnchor.MiddleCenter);
            Ui.Center(note.rectTransform, 960f, 600f + Gap * 7f + 6f, 900f, 24f);
        }

        private void AddRow(RectTransform root, int slot, string label, Func<string> text, Action<int> change)
        {
            var row = new SettingRow { GetText = text, Change = change };
            row.Item = AddItem(Page.Settings, root, label.ToUpperInvariant(), 600f + slot * Gap, 820f, () => { }, MenuSounds.Kind.Change);
            row.Item.AddValue(_art.Title, 26);
            row.Item.Clicked += () => ChangeRow(row, 1);
            row.Item.ClickedBack += () =>
            {
                MenuSounds.Play(MenuSounds.Kind.Change);
                ChangeRow(row, -1);
            };
            _rows.Add(row);
            row.Item.Value = $"<   {text()}   >";
        }

        private void ChangeRow(SettingRow row, int delta)
        {
            row.Change(delta);
            foreach (var r in _rows) r.Item.Value = $"<   {r.GetText()}   >";
        }

        // ================= Navegação =================

        private void Show(Page page)
        {
            _page = page;
            foreach (var kv in _pages) kv.Value.gameObject.SetActive(kv.Key == page);
            _titleBlock.gameObject.SetActive(page != Page.Settings);
            if (page == Page.Play) RefreshContinue();
            _index = FirstInteractable();
        }

        private int FirstInteractable()
        {
            var list = _items[_page];
            for (var i = 0; i < list.Count; i++) if (list[i].Interactable) return i;
            return 0;
        }

        private void Update()
        {
            var list = _items[_page];
            for (var i = 0; i < list.Count; i++) list[i].Highlighted = i == _index;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                if (_index >= 0 && _index < list.Count) list[_index].Press();
            if (kb.escapeKey.wasPressedThisFrame && _page != Page.Main)
            {
                MenuSounds.Play(MenuSounds.Kind.Back);
                Show(Page.Main);
            }
            if (_page == Page.Settings && _index < _rows.Count)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                {
                    MenuSounds.Play(MenuSounds.Kind.Change);
                    ChangeRow(_rows[_index], -1);
                }
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                {
                    MenuSounds.Play(MenuSounds.Kind.Change);
                    ChangeRow(_rows[_index], 1);
                }
            }
        }

        private void Move(int delta)
        {
            var list = _items[_page];
            if (list.Count == 0) return;
            for (var step = 0; step < list.Count; step++)
            {
                _index = (_index + delta + list.Count) % list.Count;
                if (list[_index].Interactable) break;
            }
            MenuSounds.Play(MenuSounds.Kind.Hover, 0.55f);
        }

        // ================= Ações =================

        private void OnPlay()
        {
            // Sem jogo salvo: vai direto escolher a cavaleira.
            if (SaveSystem.LatestSlot() == null) NewGame();
            else Show(Page.Play);
        }

        private void RefreshContinue()
        {
            _continueSlot = SaveSystem.LatestSlot();
            var has = _continueSlot != null && SaveSystem.TryPeek(_continueSlot, out _);
            _continueItem.Interactable = has;
            if (!has)
            {
                _continueInfo.text = "";
                return;
            }
            SaveSystem.TryPeek(_continueSlot, out var info);
            var minutes = Mathf.FloorToInt(info.PlayTime / 60f);
            _continueInfo.text = $"<color=#E8CC80>{info.CharacterName}</color>, nível {info.Level}  •  {info.AreaName}\n" +
                                 $"{info.SavedAt}  •  {minutes / 60}h{minutes % 60:00} de jogo";
        }

        private void Continue()
        {
            if (_continueSlot == null) return;
            _fader.FadeThen(() => SaveSystem.Load(_continueSlot));
        }

        private void NewGame() => _fader.LoadScene(selectionScene);

        private void Quit()
        {
            _fader.FadeThen(() =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
    }
}
