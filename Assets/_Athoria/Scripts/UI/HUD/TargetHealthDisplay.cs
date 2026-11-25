using Athoria.Core.Events;
using Athoria.Gameplay.AI;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Player;
using Athoria.Gameplay.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.HUD
{
    /// <summary>
    /// Barra de vida do alvo no topo da tela (estilo Dark Souls): aparece ao
    /// acertar um inimigo, mostra o nome e some alguns segundos depois.
    /// </summary>
    public sealed class TargetHealthDisplay : MonoBehaviour
    {
        [SerializeField] private ResourceBar bar;
        [SerializeField] private Text nameText;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float showTime = 7f;
        [SerializeField] private float fadeSpeed = 4f;

        private Health _target;
        private float _hideAt;

        private void OnEnable()
        {
            EventBus.Subscribe<DamageApplied>(OnDamage);
            EventBus.Subscribe<HealthChanged>(OnHealthChanged);
            EventBus.Subscribe<EnemyEngaged>(OnEngaged);
            EventBus.Subscribe<Athoria.Gameplay.Bosses.BossEncounterStarted>(OnBossStarted);
            EventBus.Subscribe<Athoria.Gameplay.Bosses.BossDefeated>(OnBossDefeated);
            if (group != null) group.alpha = 0f;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DamageApplied>(OnDamage);
            EventBus.Unsubscribe<HealthChanged>(OnHealthChanged);
            EventBus.Unsubscribe<EnemyEngaged>(OnEngaged);
            EventBus.Unsubscribe<Athoria.Gameplay.Bosses.BossEncounterStarted>(OnBossStarted);
            EventBus.Unsubscribe<Athoria.Gameplay.Bosses.BossDefeated>(OnBossDefeated);
        }

        private Health _boss;

        /// <summary>Chefe desperto: a barra aparece e fica até ele cair.</summary>
        private void OnBossStarted(Athoria.Gameplay.Bosses.BossEncounterStarted e)
        {
            _boss = e.Health;
            if (_boss != null) Show(_boss, 3f);
        }

        private void OnBossDefeated(Athoria.Gameplay.Bosses.BossDefeated e)
        {
            if (e.Health == _boss) _boss = null;
        }

        private void OnDamage(DamageApplied e)
        {
            if (e.Target == null || e.Target.IsPlayer || e.Result == DamageResult.Ignored) return;
            var source = e.Info.Source;
            if (source == null || source.GetComponentInParent<PlayerController>() == null) return;
            if (_boss != null && _boss.IsAlive && e.Target != _boss) return;   // durante o chefe a barra é dele
            Show(e.Target, e.Result == DamageResult.Killed ? 1.5f : showTime);
        }

        /// <summary>Chefes e inimigos fortes mostram a barra assim que entram em combate.</summary>
        private void OnEngaged(EnemyEngaged e)
        {
            if (e.Enemy == null || e.Enemy.Data == null || e.Enemy.Data.maxHealth < 200f) return;
            Show(e.Enemy.Health, showTime);
        }

        private void Show(Health target, float duration)
        {
            if (target != _target)
            {
                _target = target;
                if (nameText != null) nameText.text = NameOf(target).ToUpperInvariant();
                if (bar != null) bar.SnapTo(target.Current, target.Max);
            }
            _hideAt = Time.unscaledTime + duration;
        }

        private void OnHealthChanged(HealthChanged e)
        {
            if (e.Target != _target) return;
            if (bar != null) bar.Set(e.Current, e.Max);
            if (e.Current <= 0f) _hideAt = Mathf.Min(_hideAt, Time.unscaledTime + 1.5f);
            else if (_target.TryGetComponent<EnemyAI>(out var ai) && ai.IsAware) _hideAt = Mathf.Max(_hideAt, Time.unscaledTime + 2f);
        }

        private static string NameOf(Health target)
        {
            if (target.TryGetComponent<EnemyAI>(out var ai)) return ai.DisplayName;
            if (target.TryGetComponent<Athoria.Gameplay.Bosses.MossAncientBrain>(out var boss)) return boss.DisplayName;
            if (target.TryGetComponent<MossSpiritAI>(out var spirit)) return spirit.DisplayName;
            if (target.TryGetComponent<ExperienceReward>(out var reward)) return reward.DisplayName;
            return target.name;
        }

        private void Update()
        {
            if (group == null) return;
            if (_boss != null && _boss.IsAlive && _target == _boss) _hideAt = Time.unscaledTime + 1f;
            var visible = _target != null && Time.unscaledTime < _hideAt;
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
            if (!visible && group.alpha <= 0f) _target = null;
        }
    }
}
