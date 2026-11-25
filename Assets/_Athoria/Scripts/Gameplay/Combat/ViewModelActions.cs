using System;
using System.Collections.Generic;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    public enum ViewModelActionId
    {
        Draw, Sheathe, Inspect, Parry, DodgeLeft, DodgeRight, DodgeBack, HitReaction,
        Interact, PickUp, OpenChest, UseConsumable
    }

    /// <summary>
    /// Um quadro-chave de uma ação dos braços em primeira pessoa. Posições/rotações são
    /// deslocamentos somados à pose atual de cada braço (espaço da câmera: X direita, Y cima, Z frente).
    /// </summary>
    [Serializable]
    public struct ViewModelKey
    {
        [Range(0f, 1f)] public float time;
        public Vector3 rightPosition, rightEuler;
        public Vector3 leftPosition, leftEuler;
        [Range(0f, 1f), Tooltip("Mão esquerda presa ao cabo (armas de duas mãos). 0 = solta.")] public float supportGrip;
        [Range(0f, 1f), Tooltip("Dedos da mão direita: 0 aberta, 1 fechada.")] public float rightFingers;
        [Range(0f, 1f), Tooltip("Dedos da mão esquerda: 0 aberta, 1 fechada.")] public float leftFingers;
    }

    /// <summary>Ação completa: duração, quadros-chave (início → execução → recuperação) e o momento do efeito.</summary>
    [Serializable]
    public sealed class ViewModelAction
    {
        public ViewModelActionId id;
        [Min(0.05f)] public float duration = 0.5f;
        [Range(0f, 1f), Tooltip("Momento (0-1) em que a ação 'acontece' (pegar, beber, abrir).")] public float eventTime = 0.5f;
        [Tooltip("Pode acontecer no meio de um ataque (dano, aparar, esquiva).")] public bool interruptsCombat;
        [Tooltip("Prioridade: uma ação só corta outra de prioridade menor ou igual.")] public int priority;
        public ViewModelKey[] keys = Array.Empty<ViewModelKey>();

        /// <summary>Amostra suave (Catmull-Rom) entre os quadros-chave.</summary>
        public ViewModelKey Sample(float t)
        {
            if (keys.Length == 0) return Neutral(t);
            if (t <= keys[0].time) return keys[0];
            for (var i = 0; i < keys.Length - 1; i++)
            {
                var a = keys[i];
                var b = keys[i + 1];
                if (t > b.time) continue;
                var u = Mathf.InverseLerp(a.time, b.time, t);
                var p = keys[Mathf.Max(0, i - 1)];
                var n = keys[Mathf.Min(keys.Length - 1, i + 2)];
                return new ViewModelKey
                {
                    time = t,
                    rightPosition = CR(p.rightPosition, a.rightPosition, b.rightPosition, n.rightPosition, u),
                    rightEuler = CR(p.rightEuler, a.rightEuler, b.rightEuler, n.rightEuler, u),
                    leftPosition = CR(p.leftPosition, a.leftPosition, b.leftPosition, n.leftPosition, u),
                    leftEuler = CR(p.leftEuler, a.leftEuler, b.leftEuler, n.leftEuler, u),
                    supportGrip = Mathf.Lerp(a.supportGrip, b.supportGrip, Smooth(u)),
                    rightFingers = Mathf.Lerp(a.rightFingers, b.rightFingers, Smooth(u)),
                    leftFingers = Mathf.Lerp(a.leftFingers, b.leftFingers, Smooth(u)),
                };
            }
            return keys[keys.Length - 1];
        }

        public static ViewModelKey Neutral(float t) => new() { time = t, supportGrip = 1f, rightFingers = 1f, leftFingers = 1f };

        private static float Smooth(float u) => u * u * (3f - 2f * u);

        private static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }

    /// <summary>
    /// Conjunto de ações dos braços (sacar, guardar, inspecionar, aparar, esquiva, dano,
    /// interagir, pegar, abrir baú, usar consumível). Um conjunto padrão é gerado pelo editor;
    /// cada arma pode apontar para outro (ex.: armas de duas mãos sacam das costas).
    /// </summary>
    [CreateAssetMenu(menuName = "Athoria/Combate/Conjunto de ações dos braços", fileName = "AcoesBracos_")]
    public sealed class ViewModelActionSet : ScriptableObject
    {
        public ViewModelAction[] actions = Array.Empty<ViewModelAction>();

        private Dictionary<ViewModelActionId, ViewModelAction> _map;

        public ViewModelAction Get(ViewModelActionId id)
        {
            if (_map == null || _map.Count != actions.Length)
            {
                _map = new Dictionary<ViewModelActionId, ViewModelAction>();
                foreach (var a in actions) if (a != null) _map[a.id] = a;
            }
            return _map.TryGetValue(id, out var action) ? action : null;
        }

        private void OnValidate() => _map = null;
    }
}
