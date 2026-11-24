using System.Collections.Generic;
using Athoria.Core.Events;
using Athoria.Gameplay.Stats;
using Athoria.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.HUD
{
    /// <summary>
    /// Faixa central para acontecimentos importantes: subir de nível, local
    /// descoberto, segredo encontrado. Mensagens entram numa fila.
    /// </summary>
    public sealed class NotificationBanner : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text title;
        [SerializeField] private Text subtitle;
        [SerializeField] private AudioSource audioSource;

        [Header("Sons")]
        [SerializeField] private AudioClip levelUpClip;
        [SerializeField] private AudioClip discoveryClip;
        [SerializeField] private AudioClip enemyXpClip;

        [Header("Tempo")]
        [SerializeField] private float fadeIn = 0.25f;
        [SerializeField] private float hold = 2.2f;
        [SerializeField] private float fadeOut = 0.6f;

        private struct Message
        {
            public string Title, Subtitle;
            public AudioClip Clip;
            public Color Color;
        }

        private readonly Queue<Message> _queue = new();
        private float _shownAt = float.NegativeInfinity;
        private bool _showing;

        private static readonly Color Gold = new(0.96f, 0.8f, 0.45f);
        private static readonly Color Pale = new(0.85f, 0.82f, 0.75f);
        private static readonly Color Secret = new(0.75f, 0.6f, 1f);

        private void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LevelUp>(OnLevelUp);
            EventBus.Subscribe<LocationDiscovered>(OnDiscovered);
            EventBus.Subscribe<ExperienceGained>(OnXp);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LevelUp>(OnLevelUp);
            EventBus.Unsubscribe<LocationDiscovered>(OnDiscovered);
            EventBus.Unsubscribe<ExperienceGained>(OnXp);
        }

        private void OnLevelUp(LevelUp e) => Enqueue($"NÍVEL {e.NewLevel}", "Vida, vigor e mana aumentaram", levelUpClip, Gold);

        private void OnDiscovered(LocationDiscovered e)
        {
            var head = e.IsSecret ? "SEGREDO ENCONTRADO" : "LOCAL DESCOBERTO";
            var sub = e.Experience > 0 ? $"{e.Name}   +{e.Experience} XP" : e.Name;
            Enqueue(head, sub, discoveryClip, e.IsSecret ? Secret : Pale);
        }

        private void OnXp(ExperienceGained e)
        {
            if (e.Source == XpSource.Enemy && audioSource != null && enemyXpClip != null)
                audioSource.PlayOneShot(enemyXpClip, 0.5f);
        }

        public void Enqueue(string head, string sub, AudioClip clip, Color color) =>
            _queue.Enqueue(new Message { Title = head, Subtitle = sub, Clip = clip, Color = color });

        private void Update()
        {
            if (group == null) return;

            if (!_showing)
            {
                if (_queue.Count == 0) return;
                var m = _queue.Dequeue();
                title.text = m.Title;
                title.color = m.Color;
                subtitle.text = m.Subtitle;
                if (audioSource != null && m.Clip != null) audioSource.PlayOneShot(m.Clip);
                _shownAt = Time.unscaledTime;
                _showing = true;
            }

            var t = Time.unscaledTime - _shownAt;
            if (t < fadeIn) group.alpha = t / fadeIn;
            else if (t < fadeIn + hold) group.alpha = 1f;
            else if (t < fadeIn + hold + fadeOut) group.alpha = 1f - (t - fadeIn - hold) / fadeOut;
            else
            {
                group.alpha = 0f;
                _showing = false;
            }
        }
    }
}
