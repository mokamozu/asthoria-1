using System.Collections.Generic;
using Athoria.Core.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.HUD
{
    /// <summary>Mensagens curtas no centro inferior da tela ("+1 Poção", "Inventário cheio").</summary>
    public sealed class MessageToast : MonoBehaviour
    {
        [SerializeField] private Text text;
        [SerializeField] private float visibleTime = 2.2f;
        [SerializeField] private int maxLines = 4;

        private readonly List<(string text, float time)> _lines = new();

        private void OnEnable() => EventBus.Subscribe<GameMessage>(OnMessage);
        private void OnDisable() => EventBus.Unsubscribe<GameMessage>(OnMessage);

        private void OnMessage(GameMessage m)
        {
            _lines.Add((m.Text, Time.unscaledTime));
            while (_lines.Count > maxLines) _lines.RemoveAt(0);
            Redraw();
        }

        private void Update()
        {
            if (_lines.Count == 0) return;
            var removed = _lines.RemoveAll(l => Time.unscaledTime - l.time > visibleTime) > 0;
            if (removed) Redraw();
            if (_lines.Count == 0)
            {
                text.text = string.Empty;
                return;
            }
            var newest = Time.unscaledTime - _lines[^1].time;
            var c = text.color;
            c.a = Mathf.Clamp01((visibleTime - newest) / 0.5f);
            text.color = c;
        }

        private void Redraw()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var l in _lines) sb.AppendLine(l.text);
            text.text = sb.ToString().TrimEnd();
        }
    }
}
