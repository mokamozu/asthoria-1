using System;
using System.Collections;
using Athoria.Core;
using Athoria.Core.Events;
using Athoria.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Viagem entre cenas (região ↔ masmorra): escurece a tela, carrega a cena,
    /// coloca o jogador no ponto de chegada e clareia. O jogador e a interface
    /// sobrevivem à troca (PersistAcrossScenes).
    /// </summary>
    public sealed class WorldManager : MonoBehaviour
    {
        private static WorldManager s_instance;
        private static string s_pendingSpawn;

        private CanvasGroup _fade;
        private bool _travelling;

        public static bool IsTravelling => s_instance != null && s_instance._travelling;

        /// <summary>Viagem concluída por portal (para o salvamento automático).</summary>
        public static event Action TravelCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
            s_pendingSpawn = null;
            TravelCompleted = null;
        }

        private static WorldManager Instance
        {
            get
            {
                if (s_instance != null) return s_instance;
                var go = new GameObject("_WorldManager");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<WorldManager>();
                s_instance.BuildFade();
                return s_instance;
            }
        }

        /// <summary>Viaja para outra cena e chega no SpawnPoint com esse id.</summary>
        public static void TravelTo(string sceneName, string spawnId)
        {
            if (IsTravelling) return;
            Instance.StartCoroutine(Instance.Travel(sceneName, spawnId, null, null));
        }

        /// <summary>
        /// Carregar um save: tela escura → beforeLoad (ex.: descartar o jogador atual)
        /// → carrega a cena → afterLoad (restaura o estado) → clareia.
        /// </summary>
        public static void LoadSave(string sceneName, Action beforeLoad, Func<IEnumerator> afterLoad)
        {
            if (IsTravelling) return;
            Instance.StartCoroutine(Instance.Travel(sceneName, null, beforeLoad, afterLoad));
        }

        /// <summary>Chamado por uma cena recém-aberta direto no editor: aplica o ponto de chegada padrão.</summary>
        public static string ConsumePendingSpawn()
        {
            var id = s_pendingSpawn;
            s_pendingSpawn = null;
            return id;
        }

        private void BuildFade()
        {
            var canvasGo = new GameObject("Fade", typeof(Canvas), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            _fade = canvasGo.GetComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;
            var img = new GameObject("Preto", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            img.GetComponent<Image>().color = Color.black;
        }

        private IEnumerator Fade(float to, float time)
        {
            var from = _fade.alpha;
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / time)
            {
                _fade.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            _fade.alpha = to;
        }

        private IEnumerator Travel(string sceneName, string spawnId, Action beforeLoad, Func<IEnumerator> afterLoad)
        {
            _travelling = true;
            _fade.blocksRaycasts = true;
            yield return Fade(1f, 0.45f);
            beforeLoad?.Invoke();

            s_pendingSpawn = spawnId;
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[Athoria] Cena '{sceneName}' não está no Build Settings.");
            }
            else
            {
                while (!op.isDone) yield return null;
            }
            yield return null;   // um quadro para os objetos da cena acordarem (streaming, NavMesh)

            if (afterLoad != null)
            {
                ConsumePendingSpawn();
                yield return afterLoad();
            }
            else
            {
                PlaceAtSpawn(ConsumePendingSpawn());
            }
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Fade(0f, 0.6f);
            _fade.blocksRaycasts = false;
            _travelling = false;
            if (afterLoad == null) TravelCompleted?.Invoke();
        }

        /// <summary>Coloca o jogador persistente no ponto de chegada e define o respawn.</summary>
        public static void PlaceAtSpawn(string spawnId)
        {
            var player = PersistAcrossScenes.Get("Player");
            if (player == null) player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            SpawnPoint target = null;
            foreach (var sp in FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
            {
                if (!string.IsNullOrEmpty(spawnId) && sp.Id == spawnId) { target = sp; break; }
                if (target == null || sp.IsDefault) target = sp;
            }
            if (target == null) return;

            if (player.TryGetComponent<PlayerController>(out var controller))
                controller.Teleport(target.transform.position, target.transform.rotation);
            else
                player.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            if (player.TryGetComponent<PlayerDeathHandler>(out var death))
                death.SetRespawnPoint(target.transform.position, target.transform.rotation);
        }
    }
}
