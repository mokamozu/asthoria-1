using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Athoria.Core.Events;
using Athoria.Gameplay.Characters;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Inventory;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Player;
using Athoria.Gameplay.Stats;
using Athoria.Gameplay.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Athoria.Gameplay.Save
{
    /// <summary>Resumo de um save (para o botão Continuar).</summary>
    public readonly struct SaveInfo
    {
        public readonly string Slot;
        public readonly string CharacterName;
        public readonly string AreaName;
        public readonly string SavedAt;
        public readonly int Level;
        public readonly float PlayTime;

        public SaveInfo(string slot, SaveData d)
        {
            Slot = slot;
            CharacterName = d.characterName;
            AreaName = d.areaName;
            SavedAt = d.savedAt;
            Level = d.level;
            PlayTime = d.playTime;
        }
    }

    /// <summary>
    /// Salvar e carregar o jogo em JSON (pasta de dados do usuário, "saves").
    /// Guarda personagem, posição, vida/vigor/mana, XP, ouro, inventário (com
    /// itens sorteados), equipamento, armas, atalhos e o estado do mundo.
    /// Slots: "rapido" (F5/F9) e "auto" (ao passar por uma porta).
    /// </summary>
    public static class SaveSystem
    {
        public const string QuickSlot = "rapido";
        public const string AutoSlot = "auto";
        private static readonly string[] Slots = { QuickSlot, AutoSlot };

        private static SaveData s_pending;
        private static float s_playTimeBase;
        private static float s_sessionStart;

        public static bool IsRestoring => s_pending != null;
        public static string Folder => Path.Combine(Application.persistentDataPath, "saves");
        private static string PathFor(string slot) => Path.Combine(Folder, slot + ".json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_pending = null;
            s_playTimeBase = 0f;
            s_sessionStart = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init() => WorldManager.TravelCompleted += () => Save(AutoSlot, true);

        /// <summary>Chamado ao começar um jogo novo pela seleção de personagem.</summary>
        public static void BeginNewGame()
        {
            WorldState.Clear();
            s_pending = null;
            s_playTimeBase = 0f;
            s_sessionStart = Time.unscaledTime;
        }

        public static float PlayTime => s_playTimeBase + (Time.unscaledTime - s_sessionStart);

        // ---------------- Arquivos ----------------

        public static bool Exists(string slot) => File.Exists(PathFor(slot));

        /// <summary>O save mais recente entre todos os slots (ou null).</summary>
        public static string LatestSlot()
        {
            string best = null;
            var bestTime = DateTime.MinValue;
            foreach (var slot in Slots)
            {
                var path = PathFor(slot);
                if (!File.Exists(path)) continue;
                var t = File.GetLastWriteTime(path);
                if (t <= bestTime) continue;
                bestTime = t;
                best = slot;
            }
            return best;
        }

        public static bool TryPeek(string slot, out SaveInfo info)
        {
            info = default;
            var data = Read(slot);
            if (data == null) return false;
            info = new SaveInfo(slot, data);
            return true;
        }

        private static SaveData Read(string slot)
        {
            try
            {
                var path = PathFor(slot);
                return File.Exists(path) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(path)) : null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Athoria] Save '{slot}' corrompido: {e.Message}");
                return null;
            }
        }

        // ---------------- Salvar ----------------

        public static bool Save(string slot, bool automatic = false)
        {
            if (IsRestoring || WorldManager.IsTravelling) return false;
            var player = FindPlayer();
            if (player == null) return false;
            if (player.TryGetComponent<PlayerDeathHandler>(out var death) && death.IsDead)
            {
                if (!automatic) EventBus.Publish(new GameMessage("Não é possível salvar agora"));
                return false;
            }

            var data = Capture(player);
            try
            {
                Directory.CreateDirectory(Folder);
                var path = PathFor(slot);
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogError("[Athoria] Falha ao salvar: " + e.Message);
                EventBus.Publish(new GameMessage("Falha ao salvar o jogo"));
                return false;
            }

            EventBus.Publish(new GameMessage(automatic ? "Jogo salvo automaticamente" : "Jogo salvo"));
            return true;
        }

        private static GameObject FindPlayer()
        {
            var p = PersistAcrossScenes.Get("Player");
            if (p != null) return p;
            var controller = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            return controller != null ? controller.gameObject : null;
        }

        private static SaveData Capture(GameObject player)
        {
            var d = new SaveData
            {
                savedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                playTime = PlayTime,
                scene = SceneManager.GetActiveScene().name,
                position = player.transform.position,
                yaw = player.transform.eulerAngles.y
            };
            var area = UnityEngine.Object.FindAnyObjectByType<AreaInfo>();
            d.areaName = area != null ? area.DisplayName : d.scene;

            if (player.TryGetComponent<PlayerCharacter>(out var character) && character.Current != null)
            {
                d.characterId = character.Current.Id;
                d.characterAsset = character.Current.name;
                d.characterName = character.Current.DisplayName;
            }
            if (player.TryGetComponent<Health>(out var health)) d.health = health.Current;
            if (player.TryGetComponent<PlayerStamina>(out var stamina)) d.stamina = stamina.Current;
            if (player.TryGetComponent<PlayerMana>(out var mana)) d.mana = mana.Current;
            if (player.TryGetComponent<PlayerExperience>(out var xp))
            {
                d.level = xp.Level;
                d.xpCurrent = xp.Current;
                d.xpTotal = xp.Total;
            }

            if (player.TryGetComponent<PlayerInventory>(out var inventory))
            {
                d.gold = inventory.Gold;
                foreach (var e in inventory.Grid.Entries)
                    d.inventory.Add(new SavedStack { item = ToSaved(e.Stack.Item), quantity = e.Stack.Quantity, x = e.X, y = e.Y });
            }

            if (player.TryGetComponent<PlayerEquipment>(out var equipment))
            {
                d.equipment = new SavedItem[PlayerEquipment.SlotCount];
                for (var i = 0; i < PlayerEquipment.SlotCount; i++) d.equipment[i] = ToSaved(equipment.Get((EquipmentSlot)i));
            }
            if (player.TryGetComponent<WeaponLoadout>(out var loadout)) d.activeWeaponSlot = loadout.ActiveSlot;

            if (player.TryGetComponent<QuickItemSlots>(out var quick))
            {
                d.quickItems = new string[QuickItemSlots.Count];
                for (var i = 0; i < QuickItemSlots.Count; i++) d.quickItems[i] = quick.Get(i) != null ? quick.Get(i).Id : string.Empty;
            }

            d.flags = new List<string>(WorldState.All);
            return d;
        }

        private static SavedItem ToSaved(ItemData item)
        {
            if (item == null) return new SavedItem();
            var baseItem = item.BaseItem;
            var s = new SavedItem { id = baseItem.Id, asset = baseItem.name, rolled = item.IsRolled };
            if (!item.IsRolled) return s;
            s.rarity = (int)item.Rarity;
            s.quality = item.Quality;
            s.bonuses = item.BonusModifiers;
            s.name = item.DisplayName;
            s.value = item.Value;
            return s;
        }

        private static ItemData FromSaved(SavedItem s)
        {
            if (s == null || s.IsEmpty) return null;
            var db = GameDatabase.Instance;
            var baseItem = db != null ? db.FindItem(s.id, s.asset) : null;
            if (baseItem == null)
            {
                Debug.LogWarning($"[Athoria] Item do save não encontrado: {s.asset} ({s.id})");
                return null;
            }
            if (!s.rolled) return baseItem;
            var multiplier = baseItem.Value > 0 ? s.value / (float)baseItem.Value : 1f;
            return baseItem.CreateRolled((ItemRarity)s.rarity, s.quality, s.bonuses, s.name, multiplier);
        }

        // ---------------- Carregar ----------------

        public static bool Load(string slot)
        {
            if (IsRestoring || WorldManager.IsTravelling) return false;
            var data = Read(slot);
            if (data == null || string.IsNullOrEmpty(data.scene))
            {
                EventBus.Publish(new GameMessage("Nenhum jogo salvo"));
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(data.scene))
            {
                Debug.LogError($"[Athoria] A cena do save ({data.scene}) não está no Build Settings.");
                return false;
            }

            var db = GameDatabase.Instance;
            var character = db != null ? db.FindCharacter(data.characterId, data.characterAsset) : null;
            if (character != null) PlayerSession.SelectedCharacter = character;

            s_pending = data;
            WorldManager.LoadSave(data.scene, () =>
            {
                // Tela já escura: descarta o jogador/HUD atuais; a cena traz cópias novas.
                WorldState.SetAll(data.flags);
                PersistAcrossScenes.DestroyAll();
            }, Restore);
            return true;
        }

        private static IEnumerator Restore()
        {
            // Dois quadros: todos os Start() rodam (personagem aplica o kit inicial, inventário etc.).
            yield return null;
            yield return null;

            var data = s_pending;
            var player = FindPlayer();
            if (data == null || player == null)
            {
                s_pending = null;
                yield break;
            }

            var rotation = Quaternion.Euler(0f, data.yaw, 0f);
            if (player.TryGetComponent<PlayerController>(out var controller)) controller.Teleport(data.position, rotation);
            if (player.TryGetComponent<PlayerDeathHandler>(out var death)) death.SetRespawnPoint(data.position, rotation);

            if (player.TryGetComponent<PlayerInventory>(out var inventory))
            {
                inventory.Grid.Clear();
                foreach (var s in data.inventory)
                {
                    var item = FromSaved(s.item);
                    if (item == null || s.quantity <= 0) continue;
                    if (!inventory.Grid.Place(new ItemStack(item, s.quantity), s.x, s.y)) inventory.Grid.Add(item, s.quantity);
                }
                inventory.SetGold(data.gold);
            }

            if (player.TryGetComponent<PlayerEquipment>(out var equipment))
            {
                for (var i = 0; i < PlayerEquipment.SlotCount; i++) equipment.Unequip((EquipmentSlot)i);
                for (var i = 0; i < PlayerEquipment.SlotCount && i < data.equipment.Length; i++)
                {
                    var item = FromSaved(data.equipment[i]);
                    if (item != null) equipment.Equip(item, (EquipmentSlot)i);
                }
            }
            if (player.TryGetComponent<WeaponLoadout>(out var loadout) && data.activeWeaponSlot >= 0 &&
                data.activeWeaponSlot < WeaponLoadout.QuickSlotCount && loadout.GetQuickSlot(data.activeWeaponSlot) != null)
                loadout.SelectSlot(data.activeWeaponSlot);

            if (player.TryGetComponent<QuickItemSlots>(out var quick))
                for (var i = 0; i < QuickItemSlots.Count && i < data.quickItems.Length; i++)
                    quick.Assign(i, GameDatabase.Instance != null ? GameDatabase.Instance.FindItem(data.quickItems[i]) as ConsumableData : null, true);

            if (player.TryGetComponent<PlayerExperience>(out var xp)) xp.LoadState(data.level, data.xpCurrent, data.xpTotal);

            // Recursos por último: os máximos já consideram nível e equipamento.
            yield return null;
            if (player.TryGetComponent<Health>(out var health)) health.SetCurrent(data.health);
            if (player.TryGetComponent<PlayerStamina>(out var stamina)) stamina.SetCurrent(data.stamina);
            if (player.TryGetComponent<PlayerMana>(out var mana)) mana.SetCurrent(data.mana);

            s_playTimeBase = data.playTime;
            s_sessionStart = Time.unscaledTime;
            s_pending = null;
            EventBus.Publish(new GameMessage("Jogo carregado"));
        }
    }
}
