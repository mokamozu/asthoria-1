using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Athoria.UI.Menus
{
    /// <summary>Sons dos menus (os mesmos do Asthoria antigo). Sobrevivem à troca de cena.</summary>
    public static class MenuSounds
    {
        public enum Kind { Hover, Click, Confirm, Back, Change }

        private static AudioSource s_source;
        private static AudioClip[] s_clips = new AudioClip[5];
        private static AudioClip[] s_hoverVariants;

        public static void Register(AudioClip hover, AudioClip click, AudioClip confirm, AudioClip back, AudioClip change)
        {
            s_clips = new[] { hover, click, confirm, back, change };
            s_hoverVariants = null;
        }

        /// <summary>Várias versões do som de passar (ex.: vidro), sorteadas a cada vez.</summary>
        public static void RegisterHoverVariants(AudioClip[] variants) => s_hoverVariants = variants;

        public static void Play(Kind kind, float volume = 0.8f)
        {
            var clip = s_clips[(int)kind];
            if (kind == Kind.Hover && s_hoverVariants != null && s_hoverVariants.Length > 0)
                clip = s_hoverVariants[Random.Range(0, s_hoverVariants.Length)];
            if (clip == null) return;
            if (s_source == null)
            {
                var go = new GameObject("Sons do Menu");
                Object.DontDestroyOnLoad(go);
                s_source = go.AddComponent<AudioSource>();
                s_source.playOnAwake = false;
                s_source.ignoreListenerPause = true;
            }
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main != null ? Camera.main.gameObject : s_source.gameObject;
                cam.AddComponent<AudioListener>();
            }
            s_source.pitch = kind == Kind.Hover ? Random.Range(0.94f, 1.06f) : 1f;
            s_source.PlayOneShot(clip, volume);
        }
    }

    /// <summary>Tela preta que clareia ao abrir e escurece antes de trocar de cena.</summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        private Image _black;
        private bool _busy;

        private bool _fadeIn = true;

        public static ScreenFader Create(Transform canvas, bool fadeInOnStart = true)
        {
            var img = Ui.Box(canvas, "Transicao", fadeInOnStart ? Color.black : Color.clear);
            Ui.Stretch(img.rectTransform);
            img.raycastTarget = fadeInOnStart;
            var f = img.gameObject.AddComponent<ScreenFader>();
            f._black = img;
            f._fadeIn = fadeInOnStart;
            return f;
        }

        private IEnumerator Start()
        {
            if (!_fadeIn) yield break;
            _black.transform.SetAsLastSibling();
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.6f)
            {
                _black.color = new Color(0f, 0f, 0f, 1f - t);
                yield return null;
            }
            _black.color = Color.clear;
            _black.raycastTarget = false;
        }

        public void LoadScene(string scene)
        {
            if (_busy) return;
            StartCoroutine(FadeRoutine(() => SceneManager.LoadScene(scene), 0.45f));
        }

        public void FadeThen(System.Action action)
        {
            if (_busy) return;
            StartCoroutine(FadeRoutine(action, 0.45f));
        }

        private IEnumerator FadeRoutine(System.Action action, float time)
        {
            _busy = true;
            _black.raycastTarget = true;
            _black.transform.SetAsLastSibling();
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / time)
            {
                _black.color = new Color(0f, 0f, 0f, t);
                yield return null;
            }
            _black.color = Color.black;
            action?.Invoke();
        }
    }

    /// <summary>
    /// Fundo do menu principal: céu, lua, sombra e nuvens em paralaxe (as mesmas
    /// camadas do Asthoria antigo), vinheta e a luz de vitral com poeira flutuando.
    /// </summary>
    public sealed class MenuBackground : MonoBehaviour
    {
        private RawImage _moon, _shadow, _clouds, _vitral;
        private RectTransform[] _dust;
        private Vector2[] _dustPos, _dustVel;
        private float[] _dustLife, _dustMax, _dustSize;
        private float _time;

        // Mesmas velocidades relativas do menu antigo (lua 0.15, nuvens 0.35, sombra 0.08).
        private const float MoonSpeed = 0.006f, CloudSpeed = 0.014f, ShadowSpeed = 0.0032f;

        public static MenuBackground Create(Transform canvas, Texture sky, Texture moon, Texture shadow, Texture clouds, Texture vitral)
        {
            var root = Ui.Stretch(Ui.Rect(canvas, "Fundo"));
            var bg = root.gameObject.AddComponent<MenuBackground>();
            Layer(root, "Ceu", sky);
            bg._moon = Layer(root, "Lua", moon);
            bg._shadow = Layer(root, "Sombra", shadow);
            bg._clouds = Layer(root, "Nuvens", clouds);

            // Vinheta: escurece topo, base e lateral direita (o clima "Hades" do menu antigo).
            Vignette(root, new Vector2(0, 0.88f), new Vector2(1, 1), 0.45f);
            Vignette(root, new Vector2(0, 0), new Vector2(1, 0.12f), 0.45f);
            Vignette(root, new Vector2(0.82f, 0), new Vector2(1, 1), 0.3f);

            if (vitral != null)
            {
                bg._vitral = Layer(root, "Luz do Vitral", vitral);
                bg.BuildDust(root, 30);
            }
            return bg;
        }

        private static RawImage Layer(RectTransform parent, string name, Texture tex)
        {
            var r = Ui.Stretch(Ui.Rect(parent, name));
            var img = r.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            img.enabled = tex != null;
            return img;
        }

        private static void Vignette(RectTransform parent, Vector2 min, Vector2 max, float alpha)
        {
            var img = Ui.Box(parent, "Vinheta", new Color(0f, 0f, 0f, alpha));
            img.rectTransform.anchorMin = min;
            img.rectTransform.anchorMax = max;
            img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
        }

        private void BuildDust(RectTransform parent, int count)
        {
            _dust = new RectTransform[count];
            _dustPos = new Vector2[count];
            _dustVel = new Vector2[count];
            _dustLife = new float[count];
            _dustMax = new float[count];
            _dustSize = new float[count];
            for (var i = 0; i < count; i++)
            {
                var img = Ui.Box(parent, "Poeira", new Color(0.86f, 0.81f, 0.69f, 0f));
                _dust[i] = img.rectTransform;
                _dust[i].anchorMin = _dust[i].anchorMax = Vector2.zero;
                ResetDust(i, true);
            }
        }

        private void ResetDust(int i, bool stagger)
        {
            _dustPos[i] = new Vector2(Random.Range(0.58f, 1.02f), Random.Range(-0.2f, 0.2f));
            _dustVel[i] = new Vector2(-Random.Range(0.010f, 0.022f), Random.Range(0.014f, 0.030f));
            _dustMax[i] = Random.Range(7f, 13f);
            _dustLife[i] = stagger ? Random.Range(0f, _dustMax[i]) : _dustMax[i];
            _dustSize[i] = Random.Range(2f, 4f);
        }

        /// <summary>Mesma máscara de feixes do vitral antigo (origem no alto à direita, cinco faixas).</summary>
        private static float BeamMask(float x, float y)
        {
            const float sx = 0.84f, sy = -0.14f;
            var dir = new Vector2(-0.34f, 0.94f).normalized;
            var rx = x - sx;
            var ry = y - sy;
            var along = rx * dir.x + ry * dir.y;
            var across = rx * -dir.y + ry * dir.x;
            if (along <= 0.02f || along >= 1.42f) return 0f;
            var fadeIn = Mathf.Clamp01((along - 0.02f) / 0.2f);
            var fadeOut = Mathf.Clamp01((1.42f - along) / 0.34f);
            float G(float c, float w) => Mathf.Exp(-Mathf.Pow((across - c) / w, 2f));
            var m = G(-0.24f, 0.10f) + G(-0.12f, 0.09f) + G(0f, 0.085f) + G(0.12f, 0.09f) + G(0.24f, 0.10f);
            return Mathf.Clamp01(m * fadeIn * fadeOut);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _time += dt;
            Scroll(_moon, MoonSpeed);
            Scroll(_shadow, ShadowSpeed);
            Scroll(_clouds, CloudSpeed);

            if (_vitral == null) return;
            var pulse = 0.94f + 0.06f * Mathf.Sin(_time * 0.15f * 0.63f * 6f);
            _vitral.color = new Color(1f, 1f, 1f, 0.85f * pulse);

            var size = ((RectTransform)transform).rect.size;
            for (var i = 0; i < _dust.Length; i++)
            {
                _dustLife[i] -= dt;
                _dustPos[i] += _dustVel[i] * dt;
                if (_dustLife[i] <= 0f || _dustPos[i].x < -0.05f || _dustPos[i].y > 1.05f) ResetDust(i, false);
                var mask = BeamMask(_dustPos[i].x, _dustPos[i].y);
                var lifeAlpha = Mathf.Min(1f, _dustLife[i] / 1.5f) * Mathf.Min(1f, (_dustMax[i] - _dustLife[i]) / 1.2f);
                var img = _dust[i].GetComponent<Image>();
                img.color = new Color(0.86f, 0.81f, 0.69f, 0.5f * lifeAlpha * mask);
                _dust[i].anchoredPosition = new Vector2(_dustPos[i].x * size.x, (1f - _dustPos[i].y) * size.y);
                _dust[i].sizeDelta = Vector2.one * _dustSize[i];
            }
        }

        private void Scroll(RawImage img, float speed)
        {
            if (img == null || !img.enabled) return;
            var r = img.uvRect;
            r.x = Mathf.Repeat(r.x + speed * Time.unscaledDeltaTime, 1f);
            img.uvRect = r;
        }
    }
}
