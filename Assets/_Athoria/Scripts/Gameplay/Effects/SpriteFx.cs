using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Gameplay.Effects
{
    /// <summary>
    /// Toca uma SpriteFxAnimation no mundo 3D como um sprite que sempre vira
    /// para a câmera (girando só no eixo vertical, para efeitos de chão
    /// continuarem no chão) — o estilo dos efeitos do DOOM.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFx : MonoBehaviour
    {
        private SpriteFxAnimation _anim;
        private SpriteRenderer _renderer;
        private int _frame;
        private float _timer;

        /// <summary>Cria o efeito na posição (pivô do efeito no ponto). Retorna null sem animação.</summary>
        public static SpriteFx Spawn(SpriteFxAnimation animation, Vector3 position, float scale = 1f)
        {
            if (animation == null || animation.FrameCount == 0) return null;
            var go = new GameObject("FX " + animation.name);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (animation.WorldScale * scale);
            var r = go.AddComponent<SpriteRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var fx = go.AddComponent<SpriteFx>();
            fx.Play(animation);
            return fx;
        }

        /// <summary>Cria o efeito no chão abaixo do ponto (se houver chão perto).</summary>
        public static SpriteFx SpawnOnGround(SpriteFxAnimation animation, Vector3 point, float scale = 1f)
        {
            if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                point = hit.point;
            return Spawn(animation, point + Vector3.up * 0.02f, scale);
        }

        public void Play(SpriteFxAnimation animation)
        {
            _anim = animation;
            _renderer = GetComponent<SpriteRenderer>();
            _frame = 0;
            _timer = 0f;
            _renderer.sprite = _anim.GetSprite(0);
            FaceCamera();
        }

        private void LateUpdate()
        {
            if (_anim == null) return;
            _timer += Time.deltaTime;
            while (_timer >= _anim.FrameDuration(_frame))
            {
                _timer -= _anim.FrameDuration(_frame);
                _frame++;
                if (_frame >= _anim.FrameCount)
                {
                    if (!_anim.Loop)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    _frame = 0;
                }
            }
            _renderer.sprite = _anim.GetSprite(_frame);
            FaceCamera();
        }

        private void FaceCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var dir = transform.position - cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
