using System;
using System.Collections;
using System.Collections.Generic;
using Athoria.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Streaming do mundo aberto: a região é dividida em pedaços (cenas
    /// aditivas). Os pedaços perto do jogador são carregados e os distantes
    /// descarregados — o mundo inteiro nunca fica na memória de uma vez.
    /// Enquanto o pedaço sob o jogador não chega, ele fica parado (não cai no vazio).
    /// </summary>
    public sealed class WorldStreamer : MonoBehaviour
    {
        [Serializable]
        public struct Chunk
        {
            public string scene;
            [Tooltip("Área do pedaço no mundo (x, z, largura, profundidade).")]
            public Rect area;
        }

        [SerializeField] private Chunk[] chunks = Array.Empty<Chunk>();
        [SerializeField, Tooltip("Carrega pedaços a até esta distância (m).")] private float loadDistance = 130f;
        [SerializeField, Tooltip("Descarrega pedaços além desta distância (m).")] private float unloadDistance = 185f;
        [SerializeField] private float checkInterval = 0.35f;

        private readonly HashSet<string> _requested = new();
        private PlayerController _player;
        private bool _frozen;

        public int LoadedCount => _requested.Count;

        private void Start() => StartCoroutine(Loop());

        private void OnDisable() => Unfreeze();

        private IEnumerator Loop()
        {
            var wait = new WaitForSeconds(checkInterval);
            while (true)
            {
                Tick();
                yield return wait;
            }
        }

        private void Tick()
        {
            if (_player == null) _player = FindAnyObjectByType<PlayerController>();
            if (_player == null) return;
            var p = _player.transform.position;
            var pos = new Vector2(p.x, p.z);

            var standingReady = true;
            foreach (var c in chunks)
            {
                var d = Distance(c.area, pos);
                var scene = SceneManager.GetSceneByName(c.scene);
                var loaded = scene.IsValid() && scene.isLoaded;

                if (d <= loadDistance && !loaded && !_requested.Contains(c.scene))
                {
                    _requested.Add(c.scene);
                    SceneManager.LoadSceneAsync(c.scene, LoadSceneMode.Additive);
                }
                else if (d > unloadDistance && (loaded || _requested.Contains(c.scene)))
                {
                    if (loaded)
                    {
                        _requested.Remove(c.scene);
                        SceneManager.UnloadSceneAsync(scene);
                    }
                }
                else if (loaded)
                {
                    _requested.Add(c.scene);
                }

                if (d <= 0.01f && !loaded) standingReady = false;
            }

            if (!standingReady) Freeze();
            else Unfreeze();
        }

        private static float Distance(Rect r, Vector2 p)
        {
            var dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            var dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private void Freeze()
        {
            if (_frozen || _player == null) return;
            _frozen = true;
            _player.enabled = false;
        }

        private void Unfreeze()
        {
            if (!_frozen) return;
            _frozen = false;
            if (_player != null) _player.enabled = true;
        }
    }
}
