using System.Collections.Generic;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Mantém o objeto vivo entre cenas (jogador, HUD, inventário, EventSystem).
    /// Toda cena jogável tem a sua cópia para poder ser testada sozinha; ao
    /// viajar, a cópia da cena nova é descartada e fica a que já existia —
    /// com vida, itens e XP preservados.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class PersistAcrossScenes : MonoBehaviour
    {
        [SerializeField, Tooltip("Objetos com a mesma chave são o mesmo objeto (ex.: Player).")]
        private string key = "Player";

        private static readonly Dictionary<string, PersistAcrossScenes> Alive = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Alive.Clear();

        public string Key => key;

        private void Awake()
        {
            if (Alive.TryGetValue(key, out var existing) && existing != null && existing != this)
            {
                // Desliga antes dos outros componentes acordarem e some.
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            Alive[key] = this;
            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Alive.TryGetValue(key, out var existing) && existing == this) Alive.Remove(key);
        }

        /// <summary>Descarta todos (carregar um save: a cena nova traz cópias limpas).</summary>
        public static void DestroyAll()
        {
            foreach (var p in Alive.Values)
                if (p != null)
                {
                    p.gameObject.SetActive(false);
                    Destroy(p.gameObject);
                }
            Alive.Clear();
        }

        /// <summary>O objeto persistente com essa chave, se existir.</summary>
        public static GameObject Get(string key) =>
            Alive.TryGetValue(key, out var p) && p != null ? p.gameObject : null;
    }
}
