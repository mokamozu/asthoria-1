using System;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Toca as ações dos braços em primeira pessoa (ViewModelActionSet) por cima das poses e
    /// dos golpes: cada ação tem início, execução (com um evento no momento certo) e
    /// recuperação, e termina sempre de volta na pose de repouso. Também decide o peso da
    /// mão de apoio no cabo e quanto os dedos ficam fechados (o IK dos braços usa isso).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewModelAnimator : MonoBehaviour
    {
        [SerializeField] private ViewModelActionSet defaultSet;
        [SerializeField, Tooltip("Suavização da volta ao repouso quando uma ação é cortada.")] private float blendOut = 10f;
        [SerializeField, Min(0.1f), Tooltip("Velocidade de todas as ações (1 = como no asset; menor = mais lento).")] private float speed = 0.75f;

        private ViewModelActionSet _set;
        private ViewModelAction _current;
        private float _time;
        private bool _eventFired;
        private Action _onEvent;
        private ViewModelKey _sample = ViewModelAction.Neutral(0f);
        private ViewModelKey _fade = ViewModelAction.Neutral(0f);
        private float _fadeWeight;

        public bool IsPlaying => _current != null;
        public ViewModelActionId? Current => _current?.id;
        public ViewModelKey Pose => _sample;
        /// <summary>Tocando uma ação "calma" (inspecionar, pegar, beber) que um ataque deve cortar.</summary>
        public bool IsPlayingNonCombat => _current != null && !_current.interruptsCombat;
        /// <summary>Disparado no momento do efeito de cada ação (pegar, beber, abrir…).</summary>
        public event Action<ViewModelActionId> ActionEvent;

        /// <summary>Conjunto da arma (ou o padrão se a arma não tiver um próprio).</summary>
        public void UseSet(ViewModelActionSet set) => _set = set != null ? set : defaultSet;

        public float DurationOf(ViewModelActionId id) => (Find(id)?.duration ?? 0f) / speed;

        /// <summary>Toca a ação; onEvent roda no momento do efeito. Devolve false se não deu (prioridade/combate).</summary>
        public bool Play(ViewModelActionId id, bool inCombat = false, Action onEvent = null)
        {
            var action = Find(id);
            if (action == null) { onEvent?.Invoke(); return false; }
            if (inCombat && !action.interruptsCombat) return false;
            if (_current != null && _current.priority > action.priority) return false;
            // A ação cortada ainda cumpre o efeito (o item é pego, o frasco é bebido).
            if (_current != null && !_eventFired) { var prev = _onEvent; var id0 = _current.id; _eventFired = true; prev?.Invoke(); ActionEvent?.Invoke(id0); }
            _fade = _sample;                       // a ação nova parte de onde a anterior estava
            _fadeWeight = _current != null ? 1f : 0f;
            _current = action;
            _time = 0f;
            _eventFired = false;
            _onEvent = onEvent;
            return true;
        }

        public void Stop()
        {
            if (_current != null && !_eventFired) { _eventFired = true; _onEvent?.Invoke(); }
            _fade = _sample; _fadeWeight = 1f; _current = null;
        }

        private ViewModelAction Find(ViewModelActionId id) => (_set != null ? _set : defaultSet)?.Get(id);

        /// <summary>Avança a ação atual; chamado pelo PlayerWeaponView a cada quadro.</summary>
        public void Tick(float dt)
        {
            var target = ViewModelAction.Neutral(0f);
            if (_current != null)
            {
                _time += dt * speed / _current.duration;
                if (!_eventFired && _time >= _current.eventTime)
                {
                    _eventFired = true;
                    _onEvent?.Invoke();
                    ActionEvent?.Invoke(_current.id);
                }
                if (_time >= 1f)
                {
                    if (!_eventFired) { _onEvent?.Invoke(); ActionEvent?.Invoke(_current.id); }
                    target = _current.Sample(1f);
                    _current = null;
                }
                else target = _current.Sample(_time);
            }
            // transição suave entre ações cortadas
            _fadeWeight = Mathf.MoveTowards(_fadeWeight, 0f, dt * blendOut);
            _sample = _fadeWeight > 0f ? Lerp(target, _fade, _fadeWeight) : target;
        }

        private static ViewModelKey Lerp(ViewModelKey a, ViewModelKey b, float t) => new()
        {
            rightPosition = Vector3.Lerp(a.rightPosition, b.rightPosition, t),
            rightEuler = Vector3.Lerp(a.rightEuler, b.rightEuler, t),
            leftPosition = Vector3.Lerp(a.leftPosition, b.leftPosition, t),
            leftEuler = Vector3.Lerp(a.leftEuler, b.leftEuler, t),
            supportGrip = Mathf.Lerp(a.supportGrip, b.supportGrip, t),
            rightFingers = Mathf.Lerp(a.rightFingers, b.rightFingers, t),
            leftFingers = Mathf.Lerp(a.leftFingers, b.leftFingers, t),
        };

#if UNITY_EDITOR
        public void EditorSetDefault(ViewModelActionSet set) => defaultSet = set;
#endif
    }
}
