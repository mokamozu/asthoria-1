using System;
using System.Collections.Generic;
using Athoria.Core;
using Athoria.Core.Events;
using Athoria.Gameplay.Save;
using Athoria.Gameplay.World;
using Athoria.UI.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Athoria.UI.Menus
{
    /// <summary>
    /// Menu de pausa (Esc), no mesmo estilo de vidro do menu principal:
    /// Continuar · Salvar jogo · Carregar jogo · Configurações · Menu principal · Sair do jogo.
    /// Fica no HUD (persiste entre cenas). Não aparece quando a pausa vem do inventário.
    /// </summary>
    public sealed class PauseMenuScreen : MonoBehaviour
    {
        [Header("Arte")]
        [SerializeField] private Texture2D uiTiles;
        [SerializeField] private Texture2D glassPane;
        [SerializeField] private Texture2D glassSheen;

        [Header("Sons")]
        [SerializeField] private AudioClip[] glassHoverSounds = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip confirmSound;
        [SerializeField] private AudioClip backSound;
        [SerializeField] private AudioClip changeSound;

        [SerializeField] private string mainMenuScene = "MenuPrincipal";

        private enum Page { Main, Load, Settings, Confirm }

        private sealed class SettingRow
        {
            public GlassMenuItem Item;
            public Func<string> GetText;
            public Action<int> Change;
        }

        private const float ItemW = 460f, ItemH = 52f, Gap = 60f, Top = 410f;

        private MenuArt _art;
        private Sprite _pane, _sheen;
        private RectTransform _root;
        private CanvasGroup _group;
        private ScreenFader _fader;
        private Text _status, _area, _confirmText;
        private readonly Dictionary<Page, RectTransform> _pages = new();
        private readonly Dictionary<Page, List<GlassMenuItem>> _items = new();
        private readonly List<SettingRow> _rows = new();
        private GlassMenuItem _saveItem, _loadQuick, _loadAuto;
        private Action _confirmAction;
        private Page _page;
        private int _index;
        private bool _open;
        private float _statusUntil;

        private void Awake()
        {
            _art = MenuArt.Create(uiTiles);
            _pane = glassPane != null
                ? Sprite.Create(glassPane, new Rect(0, 0, glassPane.width, glassPane.height), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3))
                : _art.Button;
            _sheen = glassSheen != null
                ? Sprite.Create(glassSheen, new Rect(0, 0, glassSheen.width, glassSheen.height), new Vector2(0.5f, 0.5f), 100f)
                : _art.SoftCircle;
            Build();
            SetVisible(false, true);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GamePausedChanged>(OnPauseChanged);
            GameBootstrap.BackRequested = HandleBack;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GamePausedChanged>(OnPauseChanged);
            if (GameBootstrap.BackRequested == HandleBack) GameBootstrap.BackRequested = null;
        }

        // ================= Montagem =================

        private void Build()
        {
            // Canvas próprio, acima do HUD e do inventário.
            var canvasGo = new GameObject("Menu de Pausa", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 60;
            canvasGo.AddComponent<GraphicRaycaster>();
            _root = Ui.Stretch((RectTransform)canvasGo.transform);
            _group = canvasGo.AddComponent<CanvasGroup>();

            var veil = Ui.Box(_root, "Veu", new Color(0.02f, 0.015f, 0.02f, 0.78f));
            Ui.Stretch(veil.rectTransform);
            veil.raycastTarget = true;   // bloqueia cliques no jogo
            var glow = Ui.Image(_root, "Brilho", _art.SoftCircle, new Color(1f, 0.78f, 0.42f, 0.10f));
            Ui.Center(glow.rectTransform, 960f, 480f, 1400f, 1000f);
            GoldenMotes.Create(_root, _art.SoftCircle, 22);

            if (_art.Fleuron != null)
            {
                var top = Ui.Image(_root, "Ornamento", _art.Fleuron, MenuTheme.GoldLight);
                Ui.Center(top.rectTransform, 960f, 170f, 60f * 4f, 20f * 4f);
            }
            var title = Ui.Text(_root, "Titulo", "PAUSA", _art.Title, 80, new Color(1f, 0.96f, 0.86f), TextAnchor.MiddleCenter);
            Ui.Center(title.rectTransform, 960f, 250f, 900f, 100f);
            Ui.Shadow(title, 4f, 0.9f);
            if (_art.FleuronLow != null)
            {
                var low = Ui.Image(_root, "Ornamento Inferior", _art.FleuronLow, MenuTheme.GoldLight);
                Ui.Center(low.rectTransform, 960f, 322f, 60f * 4f, 20f * 4f);
            }
            _area = Ui.Text(_root, "Area", "", _art.Body, 18, MenuTheme.TextDim, TextAnchor.MiddleCenter);
            Ui.Center(_area.rectTransform, 960f, 362f, 900f, 26f);
            _status = Ui.Text(_root, "Status", "", _art.Body, 19, MenuTheme.GoldLight, TextAnchor.MiddleCenter);
            Ui.Center(_status.rectTransform, 960f, 1000f, 1200f, 30f);

            BuildMain();
            BuildLoad();
            BuildSettings();
            BuildConfirm();
            _fader = ScreenFader.Create(_root, false);
        }

        private RectTransform NewPage(Page page)
        {
            var root = Ui.Stretch(Ui.Rect(_root, "Pagina " + page));
            _pages[page] = root;
            _items[page] = new List<GlassMenuItem>();
            return root;
        }

        private GlassMenuItem AddItem(Page page, RectTransform root, string label, float y, float w, Action action,
            MenuSounds.Kind sound, int fontSize = 30)
        {
            var item = GlassMenuItem.Create(root, _art, _pane, _sheen, label, 960f, y, w, ItemH, fontSize);
            var list = _items[page];
            var index = list.Count;
            list.Add(item);
            item.PointerEntered += () =>
            {
                if (_page != page || _index == index) return;
                _index = index;
                Sound(MenuSounds.Kind.Hover, 0.5f);
            };
            item.Clicked += () =>
            {
                Sound(sound);
                action();
            };
            return item;
        }

        private void BuildMain()
        {
            var root = NewPage(Page.Main);
            AddItem(Page.Main, root, "CONTINUAR", Top, ItemW, Resume, MenuSounds.Kind.Confirm);
            _saveItem = AddItem(Page.Main, root, "SALVAR JOGO", Top + Gap, ItemW, SaveGame, MenuSounds.Kind.Confirm);
            AddItem(Page.Main, root, "CARREGAR JOGO", Top + Gap * 2f, ItemW, () => Show(Page.Load), MenuSounds.Kind.Confirm);
            AddItem(Page.Main, root, "CONFIGURAÇÕES", Top + Gap * 3f, ItemW, () => Show(Page.Settings), MenuSounds.Kind.Confirm);
            AddItem(Page.Main, root, "MENU PRINCIPAL", Top + Gap * 4f, ItemW,
                () => Ask("Voltar ao menu principal?\n<size=18>O que não foi salvo será perdido.</size>", GoToMainMenu), MenuSounds.Kind.Confirm);
            AddItem(Page.Main, root, "SAIR DO JOGO", Top + Gap * 5f, ItemW,
                () => Ask("Sair do jogo?\n<size=18>O que não foi salvo será perdido.</size>", QuitGame), MenuSounds.Kind.Back);
        }

        private void BuildLoad()
        {
            var root = NewPage(Page.Load);
            var header = Ui.Text(root, "Cabecalho", "CARREGAR JOGO", _art.Title, 34, MenuTheme.GoldLight, TextAnchor.MiddleCenter);
            Ui.Center(header.rectTransform, 960f, Top, 900f, 44f);
            _loadQuick = AddItem(Page.Load, root, "", Top + Gap * 1.3f, 980f, () => LoadSlot(SaveSystem.QuickSlot), MenuSounds.Kind.Confirm, 22);
            _loadAuto = AddItem(Page.Load, root, "", Top + Gap * 2.3f, 980f, () => LoadSlot(SaveSystem.AutoSlot), MenuSounds.Kind.Confirm, 22);
            AddItem(Page.Load, root, "VOLTAR", Top + Gap * 3.8f, ItemW, () => Show(Page.Main), MenuSounds.Kind.Back);
            var note = Ui.Text(root, "Nota", "F5 salva rápido e F9 carrega a qualquer momento  •  o automático é feito ao passar por portas",
                _art.Body, 15, MenuTheme.TextMuted, TextAnchor.MiddleCenter);
            Ui.Center(note.rectTransform, 960f, Top + Gap * 4.6f, 1200f, 24f);
        }

        private void BuildSettings()
        {
            var root = NewPage(Page.Settings);
            var header = Ui.Text(root, "Cabecalho", "CONFIGURAÇÕES", _art.Title, 34, MenuTheme.GoldLight, TextAnchor.MiddleCenter);
            Ui.Center(header.rectTransform, 960f, Top, 900f, 44f);
            AddRow(root, 0, "Resolução", () => GameSettings.ResolutionLabel, GameSettings.StepResolution);
            AddRow(root, 1, "Tela cheia", () => GameSettings.Fullscreen ? "Sim" : "Não", _ => GameSettings.ToggleFullscreen());
            AddRow(root, 2, "Qualidade gráfica", () => GameSettings.QualityLabel, GameSettings.StepQuality);
            AddRow(root, 3, "Estilo PS1 / N64", () => GameSettings.PixelLabel, GameSettings.StepPixel);
            AddRow(root, 4, "Volume geral", () => $"{GameSettings.MasterVolume * 100f:0}%", GameSettings.StepVolume);
            AddRow(root, 5, "Sensibilidade do mouse", () => $"{GameSettings.MouseSensitivity:0.0}×", GameSettings.StepMouse);
            AddItem(Page.Settings, root, "VOLTAR", Top + Gap * 7.3f, ItemW, () => Show(Page.Main), MenuSounds.Kind.Back);
            var note = Ui.Text(root, "Nota", "clique ou → para avançar  •  botão direito ou ← para voltar", _art.Body, 15,
                MenuTheme.TextMuted, TextAnchor.MiddleCenter);
            Ui.Center(note.rectTransform, 960f, Top + Gap * 8.1f, 900f, 24f);
        }

        private void AddRow(RectTransform root, int slot, string label, Func<string> text, Action<int> change)
        {
            var row = new SettingRow { GetText = text, Change = change };
            row.Item = AddItem(Page.Settings, root, label.ToUpperInvariant(), Top + Gap * (1.2f + slot), 820f, () => { }, MenuSounds.Kind.Change, 24);
            row.Item.AddValue(_art.Title, 24);
            row.Item.Clicked += () => ChangeRow(row, 1);
            row.Item.ClickedBack += () =>
            {
                Sound(MenuSounds.Kind.Change);
                ChangeRow(row, -1);
            };
            _rows.Add(row);
        }

        private void ChangeRow(SettingRow row, int delta)
        {
            row.Change(delta);
            RefreshRows();
        }

        private void RefreshRows()
        {
            foreach (var r in _rows) r.Item.Value = $"<   {r.GetText()}   >";
        }

        private void BuildConfirm()
        {
            var root = NewPage(Page.Confirm);
            _confirmText = Ui.Text(root, "Pergunta", "", _art.Title, 34, MenuTheme.Text, TextAnchor.MiddleCenter);
            Ui.Center(_confirmText.rectTransform, 960f, Top + 30f, 1200f, 110f);
            AddItem(Page.Confirm, root, "SIM", Top + Gap * 2.2f, 300f, () => _confirmAction?.Invoke(), MenuSounds.Kind.Confirm);
            AddItem(Page.Confirm, root, "NÃO", Top + Gap * 3.2f, 300f, () => Show(Page.Main), MenuSounds.Kind.Back);
        }

        // ================= Abrir / fechar =================

        private void OnPauseChanged(GamePausedChanged e)
        {
            if (e.IsPaused)
            {
                // A pausa do inventário não abre este menu.
                var inventory = FindAnyObjectByType<InventoryScreen>();
                if (inventory != null && inventory.IsOpen) return;
                if (WorldManager.IsTravelling || SaveSystem.IsRestoring) return;
                Open();
            }
            else if (_open) SetVisible(false, false);
        }

        private void Open()
        {
            MenuSounds.Register(glassHoverSounds.Length > 0 ? glassHoverSounds[0] : null, changeSound, confirmSound, backSound, changeSound);
            MenuSounds.RegisterHoverVariants(glassHoverSounds);
            var area = FindAnyObjectByType<AreaInfo>();
            _area.text = area != null ? area.DisplayName : SceneManager.GetActiveScene().name;
            _status.text = "";
            Show(Page.Main);
            SetVisible(true, false);
        }

        private void SetVisible(bool visible, bool instant)
        {
            _open = visible;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
            if (instant) _group.alpha = visible ? 1f : 0f;
        }

        private void Resume()
        {
            if (GameBootstrap.Instance != null) GameBootstrap.Instance.SetPaused(false);
        }

        /// <summary>Esc dentro de um submenu volta para a página principal (não despausa).</summary>
        private bool HandleBack()
        {
            if (!_open || _page == Page.Main) return false;
            Sound(MenuSounds.Kind.Back);
            Show(Page.Main);
            return true;
        }

        // ================= Páginas =================

        private void Show(Page page)
        {
            _page = page;
            foreach (var kv in _pages) kv.Value.gameObject.SetActive(kv.Key == page);
            if (page == Page.Load) RefreshLoad();
            if (page == Page.Settings) RefreshRows();
            _index = FirstInteractable();
        }

        private int FirstInteractable()
        {
            var list = _items[_page];
            for (var i = 0; i < list.Count; i++) if (list[i].Interactable) return i;
            return list.Count - 1;
        }

        private void RefreshLoad()
        {
            Describe(_loadQuick, SaveSystem.QuickSlot, "SAVE RÁPIDO");
            Describe(_loadAuto, SaveSystem.AutoSlot, "AUTOMÁTICO");
        }

        private static void Describe(GlassMenuItem item, string slot, string label)
        {
            if (SaveSystem.TryPeek(slot, out var info))
            {
                var minutes = Mathf.FloorToInt(info.PlayTime / 60f);
                item.Label = $"{label}  —  {info.CharacterName}, nível {info.Level}  •  {info.AreaName}  •  {info.SavedAt}  •  {minutes / 60}h{minutes % 60:00}";
                item.Interactable = true;
            }
            else
            {
                item.Label = $"{label}  —  vazio";
                item.Interactable = false;
            }
        }

        private void Ask(string question, Action onYes)
        {
            _confirmText.text = question;
            _confirmAction = onYes;
            Show(Page.Confirm);
            _index = 1;   // "NÃO" por padrão, para não sair sem querer
        }

        // ================= Ações =================

        private void SaveGame()
        {
            var ok = SaveSystem.Save(SaveSystem.QuickSlot);
            ShowStatus(ok ? $"Jogo salvo  •  {DateTime.Now:HH:mm}" : "Não foi possível salvar agora");
        }

        private void LoadSlot(string slot)
        {
            Resume();
            SaveSystem.Load(slot);
        }

        private void GoToMainMenu()
        {
            _fader.FadeThen(() =>
            {
                Resume();
                Time.timeScale = 1f;
                PersistAcrossScenes.DestroyAll();
                SceneManager.LoadScene(mainMenuScene);
            });
        }

        private void QuitGame()
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

        private void ShowStatus(string text)
        {
            _status.text = text;
            _statusUntil = Time.unscaledTime + 3f;
        }

        private void Sound(MenuSounds.Kind kind, float volume = 0.8f) => MenuSounds.Play(kind, volume);

        // ================= Atualização =================

        private void Update()
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, _open ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            if (!_open) return;

            if (_status.text.Length > 0 && Time.unscaledTime > _statusUntil) _status.text = "";

            var list = _items[_page];
            for (var i = 0; i < list.Count; i++) list[i].Highlighted = i == _index;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                if (_index >= 0 && _index < list.Count) list[_index].Press();
            if (_page == Page.Settings && _index < _rows.Count)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                {
                    Sound(MenuSounds.Kind.Change);
                    ChangeRow(_rows[_index], -1);
                }
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                {
                    Sound(MenuSounds.Kind.Change);
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
            Sound(MenuSounds.Kind.Hover, 0.5f);
        }
    }
}
