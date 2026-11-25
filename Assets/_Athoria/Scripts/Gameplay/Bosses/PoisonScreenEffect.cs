using UnityEngine;
using UnityEngine.UI;

namespace Athoria.Gameplay.Bosses
{
    /// <summary>
    /// Intoxicação na câmera da jogadora: tela esverdeada que pulsa, visão turva nas bordas
    /// que "respiram". Criado sob demanda na própria jogadora.
    /// </summary>
    public sealed class PoisonScreenEffect : MonoBehaviour
    {
        private Image _tint, _edges;
        private float _until;
        private float _amount;

        public static PoisonScreenEffect For(GameObject player)
        {
            var fx = player.GetComponent<PoisonScreenEffect>();
            return fx != null ? fx : player.AddComponent<PoisonScreenEffect>();
        }

        public void Pulse(float seconds) => _until = Mathf.Max(_until, Time.time + seconds);

        private void Awake()
        {
            var canvasGo = new GameObject("Tela Envenenada", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;   // abaixo dos menus
            _tint = NewImage(canvasGo.transform, "Tinta", null);
            _edges = NewImage(canvasGo.transform, "Bordas", VignetteSprite());
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.color = new Color(0, 0, 0, 0);
            return img;
        }

        private static Sprite VignetteSprite()
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (var y = 0; y < S; y++)
            for (var x = 0; x < S; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((d - 0.45f) / 0.55f)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            var target = Time.time < _until ? 1f : 0f;
            _amount = Mathf.MoveTowards(_amount, target, Time.deltaTime * (target > _amount ? 2.5f : 0.8f));
            var pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
            _tint.color = new Color(0.45f, 0.65f, 0.1f, 0.18f * _amount * pulse);
            _edges.color = new Color(0.2f, 0.35f, 0.05f, 0.75f * _amount);
            // tontura leve: a tela "respira" puxando a borda para dentro
            _edges.rectTransform.localScale = Vector3.one * (1f + 0.04f * _amount * Mathf.Sin(Time.time * 1.7f));
        }
    }
}
