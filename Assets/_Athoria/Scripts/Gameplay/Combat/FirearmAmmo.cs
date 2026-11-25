using System;
using System.Collections;
using System.Collections.Generic;
using Athoria.Core.Events;
using Athoria.Gameplay.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Munição das armas de fogo com carregador (WeaponData.MagazineSize &gt; 0):
    /// conta os tiros de cada arma, recarrega com R e pede a inspeção com V.
    /// Os modelos em primeira pessoa (ex.: RevolverCarbineView) escutam os eventos
    /// para animar tambor, cão, ejeção e recarga.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirearmAmmo : MonoBehaviour
    {
        [SerializeField] private WeaponLoadout loadout;

        private readonly Dictionary<WeaponData, int> _rounds = new();
        private Coroutine _reload;

        public bool IsReloading => _reload != null;
        public WeaponData Current => loadout != null ? loadout.MainHand : null;

        public event Action Fired;
        public event Action DryFired;
        /// <summary>Duração da recarga, em segundos.</summary>
        public event Action<float> ReloadStarted;
        public event Action ReloadFinished;
        public event Action ReloadCancelled;
        public event Action InspectRequested;

        private void Awake()
        {
            if (loadout == null) loadout = GetComponent<WeaponLoadout>();
        }

        private void OnEnable() { if (loadout != null) loadout.Changed += OnLoadoutChanged; }
        private void OnDisable() { if (loadout != null) loadout.Changed -= OnLoadoutChanged; }

        public static int Capacity(WeaponData w) => w != null && w.IsFirearm ? w.MagazineSize : 0;

        /// <summary>Tiros no carregador (armas novas começam cheias).</summary>
        public int Rounds(WeaponData w)
        {
            var cap = Capacity(w);
            if (cap <= 0) return 0;
            return _rounds.TryGetValue(w, out var n) ? n : cap;
        }

        public int CurrentRounds => Rounds(Current);

        public bool HasRounds(WeaponData w) => Capacity(w) <= 0 || Rounds(w) > 0;

        /// <summary>Gasta um tiro (chamado no momento do disparo).</summary>
        public void Consume(WeaponData w)
        {
            if (Capacity(w) <= 0) return;
            _rounds[w] = Mathf.Max(0, Rounds(w) - 1);
            Fired?.Invoke();
        }

        /// <summary>Gatilho sem bala: clique e aviso.</summary>
        public void DryFire(WeaponData w)
        {
            DryFired?.Invoke();
            EventBus.Publish(new GameMessage("Sem munição — R para recarregar"));
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || Time.timeScale <= 0f || Cursor.lockState != CursorLockMode.Locked) return;
            var w = Current;
            if (kb.rKey.wasPressedThisFrame) TryReload(w);
            if (kb.vKey.wasPressedThisFrame && !IsReloading && w != null) InspectRequested?.Invoke();
        }

        public bool TryReload(WeaponData w)
        {
            var cap = Capacity(w);
            if (cap <= 0 || IsReloading || Rounds(w) >= cap) return false;
            _reload = StartCoroutine(Reload(w));
            return true;
        }

        private IEnumerator Reload(WeaponData w)
        {
            var time = Mathf.Max(0.2f, w.ReloadTime);
            ReloadStarted?.Invoke(time);
            yield return new WaitForSeconds(time);
            _rounds[w] = Capacity(w);
            _reload = null;
            ReloadFinished?.Invoke();
        }

        private void OnLoadoutChanged()
        {
            if (_reload == null) return;
            StopCoroutine(_reload);
            _reload = null;
            ReloadCancelled?.Invoke();
        }
    }
}
