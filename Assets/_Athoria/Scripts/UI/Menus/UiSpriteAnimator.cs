using Athoria.Gameplay.Effects;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.Menus
{
    /// <summary>Toca uma animação PixelFX (SpriteFxAnimation) numa Image da interface, em loop.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class UiSpriteAnimator : MonoBehaviour
    {
        private SpriteFxAnimation _animation;
        private Image _image;
        private int _frame;
        private float _time;

        public static UiSpriteAnimator Create(Transform parent, string name, SpriteFxAnimation animation, Color tint)
        {
            var img = Ui.Image(parent, name, null, tint);
            var a = img.gameObject.AddComponent<UiSpriteAnimator>();
            a._image = img;
            a._animation = animation;
            img.enabled = animation != null && animation.FrameCount > 0;
            if (img.enabled) img.sprite = animation.GetSprite(0);
            return a;
        }

        private void Update()
        {
            if (_animation == null || _animation.FrameCount == 0) return;
            _time += Time.unscaledDeltaTime;
            var d = _animation.FrameDuration(_frame);
            while (_time >= d)
            {
                _time -= d;
                _frame = (_frame + 1) % _animation.FrameCount;
                d = _animation.FrameDuration(_frame);
            }
            _image.sprite = _animation.GetSprite(_frame);
        }
    }
}
