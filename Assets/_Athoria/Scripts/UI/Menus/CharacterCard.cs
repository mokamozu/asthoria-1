using System;
using Athoria.Gameplay.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.Menus
{
    /// <summary>Cartão clicável de uma personagem na lista da seleção.</summary>
    public sealed class CharacterCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portrait;
        [SerializeField] private Text nameText;
        [SerializeField] private Text classText;
        [SerializeField] private Image frame;
        [SerializeField] private Color normalFrame = new(0.3f, 0.21f, 0.11f);
        [SerializeField] private Color selectedFrame = new(0.95f, 0.75f, 0.35f);

        public CharacterData Data { get; private set; }

        public void Setup(CharacterData data, Action<CharacterData> onClick)
        {
            Data = data;
            if (portrait != null) portrait.sprite = data.PortraitSmall;
            if (nameText != null) nameText.text = data.DisplayName;
            if (classText != null) classText.text = data.CharacterClass;
            button.onClick.AddListener(() => onClick(data));
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (frame != null) frame.color = selected ? selectedFrame : normalFrame;
            transform.localScale = selected ? Vector3.one * 1.04f : Vector3.one;
        }
    }
}
