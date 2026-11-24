using System;
using Athoria.Core.Events;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Player;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    public readonly struct WeaponLoadoutChanged : IGameEvent
    {
        public readonly WeaponData MainHand;
        public readonly WeaponData OffHand;
        public readonly int ActiveSlot;

        public WeaponLoadoutChanged(WeaponData mainHand, WeaponData offHand, int activeSlot)
        {
            MainHand = mainHand;
            OffHand = offHand;
            ActiveSlot = activeSlot;
        }
    }

    /// <summary>
    /// Armas prontas para uso: 4 atalhos para a mão principal e um item de mão
    /// secundária (escudo). Apertar o atalho da arma já empunhada guarda a arma.
    /// O sistema de equipamentos (Etapa 9) vai preencher estes slots.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponLoadout : MonoBehaviour
    {
        /// <summary>Atalho 1 = arma principal, atalho 2 = arma secundária.</summary>
        public const int QuickSlotCount = 2;

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private WeaponData[] quickSlots = new WeaponData[QuickSlotCount];
        [SerializeField] private WeaponData offHand;
        [SerializeField, Tooltip("-1 = começa desarmado.")] private int startingSlot;

        private int _activeSlot = -1;

        public int ActiveSlot => _activeSlot;
        public WeaponData MainHand => _activeSlot >= 0 ? quickSlots[_activeSlot] : null;
        public WeaponData OffHand => offHand;
        /// <summary>Escudo efetivamente em uso (nulo se a arma principal for de duas mãos).</summary>
        public WeaponData EffectiveOffHand => MainHand != null && MainHand.IsTwoHanded ? null : offHand;

        public event Action Changed;

        private void Reset() => input = GetComponent<PlayerInputReader>();

        private void OnValidate()
        {
            if (quickSlots == null || quickSlots.Length != QuickSlotCount)
                Array.Resize(ref quickSlots, QuickSlotCount);
        }

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            OnValidate();
            for (var i = 0; i < QuickSlotCount; i++)
            {
                if (quickSlots[i] != null && quickSlots[i].IsShield)
                {
                    Debug.LogWarning($"[Athoria] {quickSlots[i].DisplayName} é de mão secundária e não pode ficar no atalho {i + 1}.", this);
                    quickSlots[i] = null;
                }
            }
            if (offHand != null && !offHand.IsShield)
            {
                Debug.LogWarning($"[Athoria] {offHand.DisplayName} não é item de mão secundária.", this);
                offHand = null;
            }
            if (startingSlot >= 0 && startingSlot < QuickSlotCount && quickSlots[startingSlot] != null)
                _activeSlot = startingSlot;
        }

        private void Start() => Notify();

        private void OnEnable() { if (input != null) input.HotbarPressed += SelectSlot; }
        private void OnDisable() { if (input != null) input.HotbarPressed -= SelectSlot; }

        public WeaponData GetQuickSlot(int index) => quickSlots[index];

        public void SelectSlot(int index)
        {
            if (index < 0 || index >= QuickSlotCount) return;
            if (index == _activeSlot) index = -1;              // guardar a arma
            else if (quickSlots[index] == null) return;        // atalho vazio
            _activeSlot = index;
            Notify();
        }

        public void SetQuickSlot(int index, WeaponData weapon)
        {
            if (index < 0 || index >= QuickSlotCount) return;
            if (weapon != null && weapon.IsShield) return;
            quickSlots[index] = weapon;
            if (index == _activeSlot && weapon == null) _activeSlot = -1;
            Notify();
        }

        public void SetOffHand(WeaponData shield)
        {
            if (shield != null && !shield.IsShield) return;
            offHand = shield;
            Notify();
        }

        private void Notify()
        {
            Changed?.Invoke();
            EventBus.Publish(new WeaponLoadoutChanged(MainHand, EffectiveOffHand, _activeSlot));
        }
    }
}
