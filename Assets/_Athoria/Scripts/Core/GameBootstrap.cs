using Athoria.Core.Events;
using UnityEngine;

namespace Athoria.Core
{
    /// <summary>
    /// Ponto de entrada do jogo. Existe uma única instância, que sobrevive à troca
    /// de cenas. Nas próximas etapas ele passa a inicializar ItemDatabase,
    /// SaveSystem e WorldManager.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        [SerializeField, Tooltip("-1 = sem limite (usa o padrão da plataforma / VSync).")]
        private int targetFrameRate = -1;

        public bool IsPaused { get; private set; }

        /// <summary>
        /// Chamado quando o jogador aperta Pausa/Esc. Se devolver true, a tecla foi
        /// usada por um menu (ex.: voltar de um submenu da pausa) e o jogo não despausa.
        /// </summary>
        public static System.Func<bool> BackRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => BackRequested = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = targetFrameRate;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Time.timeScale = 1f;
            Instance = null;
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            EventBus.Publish(new GamePausedChanged(paused));
        }

        public void TogglePause() => SetPaused(!IsPaused);
    }
}
