using System.Collections.Generic;
using Athoria.Gameplay.Items;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Movimento dos braços em primeira pessoa tirado da Universal Animation Library.
    /// Um manequim invisível ("fantasma") toca o clipe do golpe; a cada quadro lemos a mão
    /// direita em relação ao peito e ao corpo e passamos esse deslocamento para os braços da
    /// câmera. Assim os golpes têm o movimento das animações da biblioteca, enquanto os
    /// braços, as armas, as pegadas e as poses ajustadas à mão continuam as mesmas.
    /// O tempo do golpe (preparação, varredura, recuperação) continua o do PlayerCombat:
    /// o clipe é "esticado" para o impacto cair no começo da varredura.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UalFirstPersonMotion : MonoBehaviour
    {
        public struct Clip
        {
            public string Name;
            public float Impact;           // 0-1: momento do impacto no clipe
            public string Recover;         // clipe de volta (opcional)
            public Clip(string name, float impact, string recover = null) { Name = name; Impact = impact; Recover = recover; }
            public bool IsValid => !string.IsNullOrEmpty(Name);
        }

        [SerializeField, Tooltip("Animator do manequim invisível (avatar da UAL).")] private Animator ghost;
        [SerializeField] private Transform ghostRoot;
        [SerializeField] private Transform chest;
        [SerializeField] private Transform handRight;
        [SerializeField, Tooltip("Quanto do deslocamento da mão vira deslocamento na tela.")] private float positionScale = 0.3f;
        [SerializeField, Tooltip("Quanto da rotação da mão vira rotação da arma.")] private float rotationScale = 0.8f;
        [SerializeField, Tooltip("Deslocamento máximo da mão (m). Para a frente vale a metade, para o braço não sair do ombro.")] private float maxOffset = 0.2f;
        [SerializeField] private float maxAngle = 95f;

        private readonly Dictionary<string, (Vector3 pos, Quaternion rot)> _start = new();

        public bool IsReady => ghost != null && ghost.runtimeAnimatorController != null && handRight != null && chest != null;

        private void Awake()
        {
            if (ghost == null) return;
            ghost.speed = 0f;                            // parado: amostrado à mão, só quando há golpe
            ghost.applyRootMotion = false;
            foreach (var r in ghost.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            if (ghostRoot == null) ghostRoot = ghost.transform;
        }

        public bool Has(string clip) => IsReady && !string.IsNullOrEmpty(clip) && ghost.HasState(0, Animator.StringToHash(clip));

        /// <summary>Clipe da biblioteca para o golpe desta arma (leve: passo do combo; pesado).</summary>
        public static Clip ClipFor(WeaponData weapon, bool heavy, int combo)
        {
            if (weapon == null || weapon.Category == WeaponCategory.Unarmed)
                return heavy ? new Clip("Melee_Hook", 0.57f, "Melee_Hook_Rec")
                    : combo % 2 == 0 ? new Clip("Punch_Jab", 0.3f) : new Clip("Punch_Cross", 0.25f);
            if (weapon.IsFirearm) return new Clip("Pistol_Shoot", 0.12f);
            if (weapon.Category == WeaponCategory.Magic) return heavy ? new Clip("Spell_Simple_Shoot", 0.25f) : new Clip("Sword_Regular_B", 0.5f, "Sword_Regular_B_Rec");
            if (weapon.Category == WeaponCategory.Spear)
                return heavy ? new Clip("Sword_Attack", 0.4f) : combo % 2 == 0 ? new Clip("Sword_Dash", 0.35f) : new Clip("Melee_Hook", 0.57f, "Melee_Hook_Rec");
            if (heavy) return new Clip("Sword_Attack", 0.4f);
            return (combo % 3) switch
            {
                0 => new Clip("Sword_Regular_A", 0.62f, "Sword_Regular_A_Rec"),
                1 => new Clip("Sword_Regular_B", 0.5f, "Sword_Regular_B_Rec"),
                _ => new Clip("Sword_Regular_C", 0.33f),
            };
        }

        /// <summary>
        /// Deslocamento (posição, euler) da mão no clipe, no instante normalizado t, em relação
        /// ao primeiro quadro do clipe de referência — no espaço da câmera.
        /// </summary>
        public bool Sample(string clip, float t, string reference, out Vector3 position, out Vector3 euler)
        {
            position = euler = Vector3.zero;
            if (!Has(clip)) return false;
            reference = Has(reference) ? reference : clip;
            if (!_start.TryGetValue(reference, out var start))
            {
                start = Read(reference, 0f);
                _start[reference] = start;
            }
            var now = Read(clip, Mathf.Clamp01(t));
            position = Vector3.ClampMagnitude((now.pos - start.pos) * positionScale, maxOffset);
            position.z = Mathf.Min(position.z, maxOffset * 0.5f);   // não empurra o braço para longe da câmera
            var delta = now.rot * Quaternion.Inverse(start.rot);
            delta.ToAngleAxis(out var angle, out var axis);
            if (angle > 180f) angle -= 360f;
            angle = Mathf.Clamp(angle * rotationScale, -maxAngle, maxAngle);
            euler = (Quaternion.AngleAxis(angle, axis)).eulerAngles;
            euler = new Vector3(Wrap(euler.x), Wrap(euler.y), Wrap(euler.z));
            return true;
        }

        private (Vector3 pos, Quaternion rot) Read(string clip, float t)
        {
            ghost.Play(clip, 0, t);
            ghost.Update(0f);
            var inv = Quaternion.Inverse(ghostRoot.rotation);
            return (inv * (handRight.position - chest.position) / Mathf.Max(0.01f, ghostRoot.lossyScale.y), inv * handRight.rotation);
        }

        private static float Wrap(float a) => a > 180f ? a - 360f : a;
    }
}
