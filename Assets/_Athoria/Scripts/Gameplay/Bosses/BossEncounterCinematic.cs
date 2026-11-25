using System.Collections;
using Athoria.Core.Events;
using Athoria.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.Gameplay.Bosses
{
    /// <summary>
    /// Cena do encontro: tira o controle da jogadora, mostra o chefe se levantando de um
    /// ângulo baixo com faixas de cinema, aproxima no grito (tremor e zoom) e devolve a
    /// câmera suavemente para a jogadora. A HUD se esconde durante a cena.
    /// </summary>
    public sealed class BossEncounterCinematic : MonoBehaviour
    {
        [SerializeField, Tooltip("Ponto que a câmera enquadra (cabeça do chefe).")] private Transform focus;
        [SerializeField] private float distance = 9f;
        [SerializeField] private float sideOffset = 3f;
        [SerializeField, Tooltip("Altura da câmera em relação aos pés do chefe (baixa = chefe imponente).")] private float height = 1.3f;
        [SerializeField] private float fieldOfView = 55f;
        [SerializeField] private float blendIn = 1f;
        [SerializeField] private float blendOut = 0.9f;

        public bool IsPlaying { get; private set; }

        public void Play(float duration, float screamFraction) => StartCoroutine(Routine(duration, screamFraction));

        private IEnumerator Routine(float duration, float screamFraction)
        {
            var main = Camera.main;
            if (main == null) yield break;
            IsPlaying = true;
            var input = FindAnyObjectByType<PlayerInputReader>();
            if (input != null) input.enabled = false;
            EventBus.Publish(new CinematicStateChanged(true));

            var camGo = new GameObject("Câmera do Encontro");
            var cam = camGo.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.depth = main.depth + 50f;
            camGo.AddComponent<AudioListener>().enabled = false;
            var bars = Letterbox();

            var target = focus != null ? focus : transform;
            Vector3 ShotPos(float push)
            {
                var fwd = transform.forward;
                var right = transform.right;
                return transform.position + fwd * Mathf.Lerp(distance, distance * 0.62f, push) + right * sideOffset * (1f - push * 0.5f) + Vector3.up * height;
            }
            Quaternion Look(Vector3 from) => Quaternion.LookRotation(target.position + Vector3.up * 0.2f - from);

            var startPos = main.transform.position;
            var startRot = main.transform.rotation;
            var startFov = main.fieldOfView;
            var screamAt = duration * screamFraction;
            var outAt = Mathf.Max(blendIn + 0.5f, duration - blendOut);

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                // 0 → blendIn: sai da jogadora para o plano baixo; depois aproxima devagar até o grito
                var push = Mathf.Clamp01(t / Mathf.Max(0.01f, screamAt + 0.6f));
                var shotPos = ShotPos(push * push);
                var shotRot = Look(shotPos);
                var fov = fieldOfView;
                var shake = Vector3.zero;
                if (t > screamAt && t < screamAt + 1.4f)
                {
                    var k = 1f - (t - screamAt) / 1.4f;
                    fov -= 10f * k;
                    shake = Random.insideUnitSphere * 0.12f * k;
                }

                Vector3 pos; Quaternion rot; float f;
                if (t < blendIn)
                {
                    var e = Smooth(t / blendIn);
                    pos = Vector3.Lerp(startPos, shotPos, e); rot = Quaternion.Slerp(startRot, shotRot, e); f = Mathf.Lerp(startFov, fov, e);
                }
                else if (t > outAt)
                {
                    // volta para a câmera da jogadora (que continua onde ela está)
                    var e = Smooth((t - outAt) / blendOut);
                    pos = Vector3.Lerp(shotPos, main.transform.position, e); rot = Quaternion.Slerp(shotRot, main.transform.rotation, e);
                    f = Mathf.Lerp(fov, main.fieldOfView, e);
                    SetBars(bars, 1f - e);
                }
                else { pos = shotPos; rot = shotRot; f = fov; }
                if (t < blendIn) SetBars(bars, Smooth(t / blendIn));

                cam.transform.SetPositionAndRotation(pos + shake, rot);
                cam.fieldOfView = f;
                yield return null;
            }

            Destroy(camGo);
            Destroy(bars.gameObject);
            if (input != null) input.enabled = true;
            EventBus.Publish(new CinematicStateChanged(false));
            IsPlaying = false;
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        // Faixas pretas de cinema em cima e embaixo
        private static Canvas Letterbox()
        {
            var go = new GameObject("Faixas de Cinema", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            foreach (var top in new[] { true, false })
            {
                var bar = new GameObject(top ? "Cima" : "Baixo", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(go.transform, false);
                var rt = (RectTransform)bar.transform;
                rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
                rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
                rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
                rt.sizeDelta = new Vector2(0f, 0f);
                var img = bar.GetComponent<Image>();
                img.color = Color.black;
                img.raycastTarget = false;
            }
            return canvas;
        }

        private static void SetBars(Canvas canvas, float amount)
        {
            if (canvas == null) return;
            var h = Screen.height * 0.11f * Mathf.Clamp01(amount);
            foreach (RectTransform child in canvas.transform) child.sizeDelta = new Vector2(0f, h);
        }
    }
}
