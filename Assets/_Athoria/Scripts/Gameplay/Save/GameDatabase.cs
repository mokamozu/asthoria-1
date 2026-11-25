using System.Collections.Generic;
using Athoria.Gameplay.Characters;
using Athoria.Gameplay.Items;
using UnityEngine;

namespace Athoria.Gameplay.Save
{
    /// <summary>
    /// Lista de todos os itens e personagens do jogo, para o save transformar
    /// Ids em assets de volta. Gerado pelo editor em Resources/BancoDeDados.
    /// </summary>
    public sealed class GameDatabase : ScriptableObject
    {
        public const string ResourcePath = "BancoDeDados";

        [SerializeField] private ItemData[] items = System.Array.Empty<ItemData>();
        [SerializeField] private CharacterData[] characters = System.Array.Empty<CharacterData>();

        private Dictionary<string, ItemData> _itemsById;
        private Dictionary<string, ItemData> _itemsByName;
        private static GameDatabase s_instance;

        public static GameDatabase Instance
        {
            get
            {
                if (s_instance == null) s_instance = Resources.Load<GameDatabase>(ResourcePath);
                if (s_instance == null) Debug.LogError("[Athoria] Resources/BancoDeDados não encontrado. Rode Athoria > Reconstruir tudo.");
                return s_instance;
            }
        }

        public ItemData FindItem(string id, string assetName = null)
        {
            if (_itemsById == null)
            {
                _itemsById = new Dictionary<string, ItemData>();
                _itemsByName = new Dictionary<string, ItemData>();
                foreach (var i in items)
                {
                    if (i == null) continue;
                    if (!string.IsNullOrEmpty(i.Id)) _itemsById[i.Id] = i;
                    _itemsByName[i.name] = i;
                }
            }
            if (!string.IsNullOrEmpty(id) && _itemsById.TryGetValue(id, out var item)) return item;
            return !string.IsNullOrEmpty(assetName) && _itemsByName.TryGetValue(assetName, out item) ? item : null;
        }

        public CharacterData FindCharacter(string id, string assetName = null)
        {
            foreach (var c in characters)
                if (c != null && (c.Id == id || c.name == assetName)) return c;
            return null;
        }

#if UNITY_EDITOR
        public void EditorSet(ItemData[] newItems, CharacterData[] newCharacters)
        {
            items = newItems;
            characters = newCharacters;
            _itemsById = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
