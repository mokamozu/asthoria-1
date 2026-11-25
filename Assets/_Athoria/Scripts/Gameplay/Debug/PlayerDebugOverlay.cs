using Athoria.Gameplay.AI;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Interaction;
using Athoria.Gameplay.Player;
using Athoria.Gameplay.Stats;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Athoria.Gameplay.Debugging
{
    /// <summary>
    /// Painel de desenvolvimento (F1) com o estado real do jogador. Ferramenta
    /// de teste: a HUD definitiva vem nas próximas etapas.
    /// </summary>
    public sealed class PlayerDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private Health health;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerMana mana;
        [SerializeField] private PlayerExperience experience;

        private GUIStyle _style;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (stamina == null) stamina = GetComponent<PlayerStamina>();
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
            if (health == null) health = GetComponent<Health>();
            if (combat == null) combat = GetComponent<PlayerCombat>();
            if (mana == null) mana = GetComponent<PlayerMana>();
            if (experience == null) experience = GetComponent<PlayerExperience>();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || controller == null) return;
            _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };

            var v = controller.HorizontalVelocity.magnitude;
            var aim = interactor != null && interactor.LastHit != null
                ? interactor.LastHit.name + " (" + interactor.LastHitDistance.ToString("0.00") + " m)"
                : "-";
            var text =
                "<b>ATHORIA — DEBUG (F1)</b>\n" +
                $"Velocidade: {v:0.00} m/s   Vertical: {controller.VerticalVelocity:0.00}\n" +
                $"No chão: {controller.IsGrounded}   Correndo: {controller.IsSprinting}   Agachado: {controller.IsCrouching}\n" +
                (stamina != null ? $"Stamina: {stamina.Current:0}/{stamina.Max:0}{(stamina.IsExhausted ? "  <color=#ff6060>EXAUSTO</color>" : "")}\n" : "") +
                (health != null ? $"Vida: {health.Current:0}/{health.Max:0}" : "") + (mana != null ? $"   Mana: {mana.Current:0}/{mana.Max:0}" : "") + "\n" +
                (experience != null ? $"Nível: {experience.Level}   XP: {experience.Current}/{experience.Required}   Total: {experience.Total}\n" : "") +
                (combat != null ? $"Combate: {combat.CurrentState}{(combat.IsBlocking ? "  [DEFENDENDO]" : "")}{(combat.IsGuardBroken ? "  <color=#ff6060>GUARDA QUEBRADA</color>" : "")}{(combat.IsInvulnerable ? "  [ESQUIVA]" : "")}  Arma: {(combat.CurrentWeapon != null ? combat.CurrentWeapon.DisplayName : "-")}\n" : "") +
                (interactor != null ? "Mirando: " + aim + "\n" : "") +
                (interactor != null && interactor.CurrentPrompt != null ? $"<color=#ffd27a>[E] {interactor.CurrentPrompt}</color>" : "");

            GUI.Box(new Rect(10, 10, 640, 190), text, _style);

            // Inimigos a até 30 m: estado da IA (patrulha, perseguição, ataque...).
            var enemies = "<b>Inimigos próximos</b>\n";
            var lines = 0;
            foreach (var e in EnemyAI.All)
            {
                if (e == null || lines >= 8) continue;
                var d = Vector3.Distance(e.transform.position, controller.transform.position);
                if (d > 30f) continue;
                enemies += $"{e.DisplayName}: {e.CurrentState}  {e.Health.Current:0}/{e.Health.Max:0}  {d:0.0} m{(e.IsAware ? "  <color=#ff8060>[ALERTA]</color>" : "")}\n";
                lines++;
            }
            if (lines > 0) GUI.Box(new Rect(10, 206, 640, 26 + lines * 18), enemies, _style);

            // Mira simples no centro da tela.
            var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.Box(new Rect(c.x - 2, c.y - 2, 4, 4), GUIContent.none);
        }
    }
}
