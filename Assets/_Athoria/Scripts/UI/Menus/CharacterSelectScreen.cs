using System.Collections.Generic;
using System.Text;
using Athoria.Gameplay.Characters;
using Athoria.Gameplay.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Athoria.UI.Menus
{
    /// <summary>
    /// Tela de seleção de personagens. Lê o CharacterDatabase e cria um cartão
    /// por personagem — novas personagens aparecem sozinhas.
    /// Teclado: ←/→ trocam, Enter começa.
    /// </summary>
    public sealed class CharacterSelectScreen : MonoBehaviour
    {
        [SerializeField] private CharacterDatabase database;
        [SerializeField] private string gameScene = "Sandbox";

        [Header("Lista")]
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private CharacterCard cardTemplate;

        [Header("Detalhes")]
        [SerializeField] private Image fullBody;
        [SerializeField] private Image portrait;
        [SerializeField] private Image emblem;
        [SerializeField] private Text nameText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text classRegionText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text traitsText;
        [SerializeField] private Text equipmentText;
        [SerializeField] private Image accentLine;
        [SerializeField] private Button startButton;

        private readonly List<CharacterCard> _cards = new();
        private int _index;

        private void Awake()
        {
            // Proteção: se o módulo de UI ficou sem ações, usa os controles padrão.
            var module = FindAnyObjectByType<InputSystemUIInputModule>();
            if (module != null && (module.actionsAsset == null || module.point == null || module.leftClick == null))
                module.AssignDefaultActions();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            cardTemplate.gameObject.SetActive(false);
            if (database == null || database.Count == 0)
            {
                Debug.LogError("[Athoria] Banco de personagens vazio.", this);
                return;
            }

            foreach (var c in database.Characters)
            {
                if (c == null) continue;
                var card = Instantiate(cardTemplate, cardContainer);
                card.gameObject.SetActive(true);
                card.name = "Cartao " + c.DisplayName;
                card.Setup(c, Select);
                _cards.Add(card);
            }

            startButton.onClick.AddListener(StartGame);
            Select(_cards[0].Data);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _cards.Count == 0) return;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Step(1);
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Step(-1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) StartGame();
        }

        private void Step(int delta)
        {
            _index = (_index + delta + _cards.Count) % _cards.Count;
            Select(_cards[_index].Data);
        }

        private void Select(CharacterData c)
        {
            for (var i = 0; i < _cards.Count; i++)
            {
                var selected = _cards[i].Data == c;
                _cards[i].SetSelected(selected);
                if (selected) _index = i;
            }

            SetSprite(fullBody, c.FullBody != null ? c.FullBody : c.Portrait);
            SetSprite(portrait, c.Portrait);
            SetSprite(emblem, c.RegionEmblem);
            nameText.text = c.DisplayName.ToUpperInvariant();
            titleText.text = c.Title;
            titleText.color = c.AccentColor;
            if (accentLine != null) accentLine.color = c.AccentColor;
            classRegionText.text = $"{c.CharacterClass}  •  {c.Region}";
            descriptionText.text = c.Description;
            statsText.text = BuildStats(c);
            traitsText.text = BuildTraits(c);
            equipmentText.text = BuildEquipment(c);
        }

        private static void SetSprite(Image img, Sprite sprite)
        {
            if (img == null) return;
            img.sprite = sprite;
            img.enabled = sprite != null;
        }

        private static string BuildStats(CharacterData c)
        {
            var speed = 100f;
            foreach (var t in c.Traits) if (t.effect == TraitEffect.MoveSpeedPercent) speed += t.value;
            var mana = c.BaseMana > 0f ? c.BaseMana.ToString("0") : "—  (não usa magia)";
            return $"Vida  {c.BaseHealth:0}      Vigor  {c.BaseStamina:0}      Mana  {mana}      Velocidade  {speed:0}%";
        }

        private static string BuildTraits(CharacterData c)
        {
            if (c.Traits == null || c.Traits.Length == 0) return "—";
            var sb = new StringBuilder();
            foreach (var t in c.Traits) sb.Append("<b>").Append(t.name).Append("</b> — ").Append(t.description).Append('\n');
            return sb.ToString().TrimEnd();
        }

        private static string BuildEquipment(CharacterData c)
        {
            var sb = new StringBuilder();
            var slot = 1;
            if (c.StartingWeapons != null)
                foreach (WeaponData w in c.StartingWeapons)
                {
                    if (w == null) continue;
                    sb.Append('[').Append(slot++).Append("] ").Append(w.DisplayName).Append('\n');
                }
            if (c.StartingOffHand != null) sb.Append("[Mão secundária] ").Append(c.StartingOffHand.DisplayName);
            return sb.Length == 0 ? "—" : sb.ToString().TrimEnd();
        }

        public void StartGame()
        {
            if (_cards.Count == 0) return;
            PlayerSession.SelectedCharacter = _cards[_index].Data;
            SceneManager.LoadScene(gameScene);
        }
    }
}
