using System;
using System.Collections.Generic;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.Save
{
    /// <summary>Referência a um item no save: o asset base + o que foi sorteado (raridade, bônus).</summary>
    [Serializable]
    public sealed class SavedItem
    {
        public string id;
        public string asset;
        public bool rolled;
        public int rarity;
        public float quality = 1f;
        public StatModifier[] bonuses = Array.Empty<StatModifier>();
        public string name;
        public int value;

        public bool IsEmpty => string.IsNullOrEmpty(id) && string.IsNullOrEmpty(asset);
    }

    [Serializable]
    public sealed class SavedStack
    {
        public SavedItem item;
        public int quantity;
        public int x, y;
    }

    /// <summary>Tudo que um jogo salvo guarda. Vira JSON legível em disco.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public string savedAt;
        public float playTime;

        [Header("Onde")]
        public string scene;
        public string areaName;
        public Vector3 position;
        public float yaw;

        [Header("Personagem")]
        public string characterId;
        public string characterAsset;
        public string characterName;
        public float health, stamina, mana;
        public int level = 1, xpCurrent, xpTotal;

        [Header("Itens")]
        public int gold;
        public List<SavedStack> inventory = new();
        public SavedItem[] equipment = Array.Empty<SavedItem>();
        public int activeWeaponSlot;
        public string[] quickItems = Array.Empty<string>();

        [Header("Mundo")]
        public List<string> flags = new();
    }
}
