using System.Collections.Generic;
using Athoria.Core.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Números de dano flutuantes (opcionais — F2 liga/desliga).
    /// Mostra dano causado pelo jogador; dano recebido aparece em vermelho.
    /// </summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        [SerializeField] private bool show = true;
        [SerializeField] private float lifetime = 0.9f;
        [SerializeField] private float riseSpeed = 1.2f;
        [SerializeField] private float characterSize = 0.05f;
        [SerializeField] private Color normalColor = new(1f, 0.95f, 0.85f);
        [SerializeField] private Color heavyColor = new(1f, 0.75f, 0.2f);
        [SerializeField] private Color receivedColor = new(1f, 0.3f, 0.25f);
        [SerializeField] private Color blockedColor = new(0.6f, 0.75f, 1f);

        private struct Entry
        {
            public TextMesh Text;
            public float Born;
            public Vector3 Velocity;
        }

        private readonly List<Entry> _entries = new();
        private Font _font;

        public bool Show { get => show; set => show = value; }

        private void Awake() => _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void OnEnable() => EventBus.Subscribe<DamageApplied>(OnDamage);
        private void OnDisable() => EventBus.Unsubscribe<DamageApplied>(OnDamage);

        private void OnDamage(DamageApplied e)
        {
            if (!show || e.Result == DamageResult.Ignored) return;

            string text;
            Color color;
            if (e.Result == DamageResult.Blocked)
            {
                text = "Bloqueado";
                color = blockedColor;
            }
            else
            {
                text = Mathf.CeilToInt(e.Info.Amount).ToString();
                color = e.Target.IsPlayer ? receivedColor : e.Info.IsHeavy ? heavyColor : normalColor;
                if (e.Info.WasBlocked) color = Color.Lerp(color, blockedColor, 0.5f);
            }

            var pos = e.Target.IsPlayer ? Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 1.2f : e.Info.Point : e.Info.Point;
            Spawn(text, color, pos, e.Info.IsHeavy ? 1.4f : 1f);
        }

        private void Spawn(string text, Color color, Vector3 position, float scale)
        {
            var go = new GameObject("Dano " + text);
            go.transform.position = position;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = _font;
            mesh.fontSize = 64;
            mesh.characterSize = characterSize * scale;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = FontStyle.Bold;
            mesh.color = color;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _font.material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _entries.Add(new Entry
            {
                Text = mesh,
                Born = Time.time,
                Velocity = new Vector3(Random.Range(-0.3f, 0.3f), riseSpeed, 0f)
            });
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame) show = !show;

            var cam = Camera.main;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                var age = (Time.time - e.Born) / lifetime;
                if (age >= 1f || e.Text == null)
                {
                    if (e.Text != null) Destroy(e.Text.gameObject);
                    _entries.RemoveAt(i);
                    continue;
                }

                var t = e.Text.transform;
                t.position += e.Velocity * Time.deltaTime;
                if (cam != null) t.rotation = Quaternion.LookRotation(t.position - cam.transform.position);
                var c = e.Text.color;
                c.a = 1f - age * age;
                e.Text.color = c;
            }
        }
    }
}
