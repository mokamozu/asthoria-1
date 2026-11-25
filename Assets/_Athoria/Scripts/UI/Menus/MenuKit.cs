using System;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.Menus
{
    /// <summary>Paleta da interface (mesma do Asthoria antigo: bronze, ouro velho, carmim).</summary>
    public static class MenuTheme
    {
        public static readonly Color Gold = C(196, 154, 64);
        public static readonly Color GoldLight = C(232, 204, 128);
        public static readonly Color GoldDark = C(92, 64, 22);
        public static readonly Color Text = C(224, 206, 168);
        public static readonly Color TextDim = C(140, 118, 94);
        public static readonly Color TextMuted = C(110, 96, 80);
        public static readonly Color Accent = C(183, 87, 54);
        public static readonly Color Crimson = C(140, 32, 28);
        public static readonly Color Health = C(60, 170, 70);
        public static readonly Color Stamina = C(200, 160, 60);
        public static readonly Color Mana = C(90, 140, 196);
        public static readonly Color Speed = C(120, 170, 220);

        private static Color C(int r, int g, int b, float a = 1f) => new(r / 255f, g / 255f, b / 255f, a);
    }

    /// <summary>
    /// Peças visuais recortadas do ui_tilesheet do Asthoria antigo (molduras,
    /// botões, ornamentos), círculos gerados e as fontes.
    /// </summary>
    public sealed class MenuArt
    {
        public Sprite Panel, Parchment, Button, ButtonHover, ButtonSelected, GoldBar, Ornament, Divider, Diamond, BarTrack;
        public Sprite Circle, Ring, RingThin, Triangle, SoftCircle, FadeUp, FadeLeft;
        public Sprite OrnamentLeft, OrnamentRight, Fleuron, FleuronLow;
        public Font Title, Body;

        /// <summary>Quantos pixels de tela vale cada pixel da arte (estilo pixel art).</summary>
        public const float PixelScale = 3f;

        public static MenuArt Create(Texture2D tiles)
        {
            var art = new MenuArt
            {
                Circle = GenerateCircle(256, 0f),
                Ring = GenerateCircle(256, 0.06f),
                RingThin = GenerateCircle(256, 0.025f),
                Triangle = GenerateTriangle(32),
                SoftCircle = GenerateSoftCircle(128),
                FadeUp = GenerateFade(true),
                FadeLeft = GenerateFade(false),
                Title = TitleFont(),
                Body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            };
            if (tiles == null) return art;
            art.Panel = Cut(tiles, 0, 0, 96, 96, 30);
            art.Parchment = Cut(tiles, 96, 0, 96, 96, 20);
            art.Button = Cut(tiles, 0, 104, 64, 24, 16, 6);
            art.ButtonHover = Cut(tiles, 64, 104, 64, 24, 16, 6);
            art.ButtonSelected = Cut(tiles, 128, 96, 64, 32, 16, 10);
            art.GoldBar = Cut(tiles, 197, 137, 86, 13, 12, 4);
            art.BarTrack = Cut(tiles, 102, 141, 84, 7, 10, 2);
            art.Ornament = Cut(tiles, 322, 190, 62, 30, 0);
            art.Divider = Cut(tiles, 294, 266, 84, 12, 0);
            art.Diamond = Cut(tiles, 10, 266, 13, 12, 0);
            art.OrnamentLeft = Cut(tiles, 203, 192, 20, 9, 0);
            art.OrnamentRight = Cut(tiles, 225, 192, 20, 9, 0);
            art.Fleuron = Cut(tiles, 322, 195, 60, 20, 0);
            art.FleuronLow = Cut(tiles, 322, 227, 60, 20, 0);
            return art;
        }

        /// <summary>Recorta uma peça (coordenadas do canto superior esquerdo, como no GameMaker).</summary>
        private static Sprite Cut(Texture2D t, int x, int y, int w, int h, int borderX, int borderY = -1)
        {
            if (borderY < 0) borderY = borderX;
            var rect = new Rect(x, t.height - y - h, w, h);
            return Sprite.Create(t, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(borderX, borderY, borderX, borderY));
        }

        private static Sprite GenerateCircle(int size, float ringWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            var r = size * 0.5f;
            var inner = r * (1f - ringWidth * 2f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                var a = Mathf.Clamp01(r - d);                                  // borda externa suave
                if (ringWidth > 0f) a *= Mathf.Clamp01(d - inner);             // furo do anel
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Círculo de borda suave (brilhos, partículas, luz).</summary>
        private static Sprite GenerateSoftCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            var r = size * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                var a = Mathf.Exp(-d * d * 4.5f) * Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Degradê: vertical (opaco embaixo) ou horizontal (opaco à esquerda).</summary>
        private static Sprite GenerateFade(bool vertical)
        {
            var w = vertical ? 1 : 64;
            var h = vertical ? 64 : 1;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var i = 0; i < 64; i++)
            {
                var a = Mathf.Pow(1f - i / 63f, 1.6f);
                if (vertical) tex.SetPixel(0, i, new Color(1f, 1f, 1f, a));
                else tex.SetPixel(i, 0, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite GenerateTriangle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var half = (1f - x / (float)size) * size * 0.5f;                 // aponta para a direita
                var inside = Mathf.Abs(y + 0.5f - size * 0.5f) <= half;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(inside ? 255 : 0));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Mesma fonte do Asthoria antigo (Perpetua Titling MT) se estiver instalada; senão, uma serifada parecida.</summary>
        private static Font TitleFont()
        {
            string[] wanted = { "Perpetua Titling MT", "Trajan Pro", "Cinzel", "Perpetua", "Georgia", "Times New Roman" };
            var installed = Font.GetOSInstalledFontNames();
            foreach (var w in wanted)
                if (Array.IndexOf(installed, w) >= 0)
                    return Font.CreateDynamicFontFromOSFont(w, 64);
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    /// <summary>Atalhos para montar a interface em código (posições em pixels a partir do canto superior esquerdo, 1920×1080).</summary>
    public static class Ui
    {
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Posiciona pelo canto superior esquerdo (como no GameMaker).</summary>
        public static RectTransform TopLeft(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        /// <summary>Posiciona pelo centro.</summary>
        public static RectTransform Center(RectTransform r, float cx, float cy, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(cx, -cy);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        public static RectTransform Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
            return r;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
        {
            var img = Rect(parent, name).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sliced && sprite != null)
            {
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f / MenuArt.PixelScale;
            }
            return img;
        }

        public static Image Box(Transform parent, string name, Color color) => Image(parent, name, null, color);

        public static Text Text(Transform parent, string name, string text, Font font, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var t = Rect(parent, name).gameObject.AddComponent<UnityEngine.UI.Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static void Shadow(Graphic g, float distance = 3f, float alpha = 0.85f)
        {
            var s = g.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, alpha);
            s.effectDistance = new Vector2(distance, -distance);
        }

        /// <summary>Moldura fina da tela (dois retângulos dourados e cantoneiras), como o ui_draw_screen_frame antigo.</summary>
        public static void ScreenFrame(Transform parent, float alpha = 0.5f)
        {
            var root = Stretch(Rect(parent, "Moldura da Tela"));
            Outline(root, 8f, 2f, new Color(MenuTheme.GoldDark.r, MenuTheme.GoldDark.g, MenuTheme.GoldDark.b, 0.55f * alpha));
            Outline(root, 14f, 1f, new Color(MenuTheme.GoldDark.r, MenuTheme.GoldDark.g, MenuTheme.GoldDark.b, 0.28f * alpha));
            var gold = new Color(MenuTheme.Gold.r, MenuTheme.Gold.g, MenuTheme.Gold.b, 0.7f * alpha);
            foreach (var corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) })
            {
                var sx = corner.x == 0 ? 1f : -1f;
                var sy = corner.y == 0 ? 1f : -1f;
                Line(root, corner, new Vector2(20f * sx, 20f * sy), new Vector2(58f, 2f), gold, sx);
                Line(root, corner, new Vector2(20f * sx, 20f * sy), new Vector2(2f, 48f), gold, sx, sy);
            }
        }

        private static void Line(RectTransform parent, Vector2 anchor, Vector2 offset, Vector2 size, Color color, float sx, float sy = 1f)
        {
            var img = Box(parent, "Cantoneira", color);
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(sx > 0 ? 0f : 1f, anchor.y == 0 ? 0f : 1f);
            r.anchoredPosition = offset;
            r.sizeDelta = size;
        }

        public static void Outline(RectTransform parent, float inset, float thickness, Color color)
        {
            void Edge(string n, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
            {
                var r = Box(parent, n, color).rectTransform;
                r.anchorMin = min;
                r.anchorMax = max;
                r.offsetMin = offMin;
                r.offsetMax = offMax;
            }
            Edge("Topo", new Vector2(0, 1), new Vector2(1, 1), new Vector2(inset, -inset - thickness), new Vector2(-inset, -inset));
            Edge("Base", new Vector2(0, 0), new Vector2(1, 0), new Vector2(inset, inset), new Vector2(-inset, inset + thickness));
            Edge("Esquerda", new Vector2(0, 0), new Vector2(0, 1), new Vector2(inset, inset), new Vector2(inset + thickness, -inset));
            Edge("Direita", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-inset - thickness, inset), new Vector2(-inset, -inset));
        }

        /// <summary>Imagem recortada em círculo (retratos).</summary>
        public static Image CircleImage(Transform parent, string name, MenuArt art, float diameter, out RectTransform root)
        {
            root = Rect(parent, name);
            root.sizeDelta = new Vector2(diameter, diameter);
            var maskImg = Image(root, "Mascara", art.Circle, Color.white);
            Stretch(maskImg.rectTransform);
            var mask = maskImg.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var picture = Image(maskImg.transform, "Retrato", null, Color.white);
            Stretch(picture.rectTransform);
            picture.preserveAspect = false;
            return picture;
        }
    }
}
