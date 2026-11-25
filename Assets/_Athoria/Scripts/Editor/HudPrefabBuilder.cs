using System.IO;
using Athoria.UI.HUD;
using Athoria.UI.Menus;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.Editor
{
    /// <summary>
    /// Monta o Prefab da HUD (Canvas). Visual provisório em estilo medieval
    /// escuro: molduras de bronze envelhecido sobre fundo quase preto.
    /// </summary>
    public static class HudPrefabBuilder
    {
        public const string PrefabPath = "Assets/_Athoria/Prefabs/UI/HUD.prefab";

        private static readonly Color Bronze = new(0.5f, 0.36f, 0.18f, 1f);
        private static readonly Color BronzeDark = new(0.22f, 0.15f, 0.08f, 1f);
        private static readonly Color Back = new(0.05f, 0.045f, 0.045f, 0.92f);

        [MenuItem("Athoria/Prefabs/Recriar Prefab da HUD")]
        public static void RebuildMenu()
        {
            Build();
            Debug.Log("[Athoria] Prefab da HUD recriado em " + PrefabPath);
        }

        public static GameObject GetOrBuild()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return existing != null ? existing : Build();
        }

        public static GameObject Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("HUD", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                // Vinheta de dano (tela inteira, atrás do resto)
                var vignetteGo = Child(root.transform, "Vinheta de Dano");
                Stretch(vignetteGo);
                vignetteGo.gameObject.AddComponent<RawImage>();
                var vignette = vignetteGo.gameObject.AddComponent<DamageVignette>();

                // Painel de recursos — canto inferior esquerdo
                var panel = Child(root.transform, "Recursos");
                panel.anchorMin = panel.anchorMax = Vector2.zero;
                panel.pivot = Vector2.zero;
                panel.anchoredPosition = new Vector2(36, 32);
                panel.sizeDelta = new Vector2(430, 112);
                var group = panel.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;

                var plate = Img(panel, "Placa", BronzeDark);
                Stretch(plate.rectTransform);
                var plateInner = Img(plate.rectTransform, "Fundo", Back);
                Stretch(plateInner.rectTransform, 3);
                // Detalhe ornamental: filetes de bronze em cima e embaixo
                Line(panel, "Filete Superior", 1f, Bronze);
                Line(panel, "Filete Inferior", 0f, Bronze);

                var health = Bar(panel, font, "Vida", new Vector2(18, 64), new Vector2(394, 28),
                    new Color(0.62f, 0.08f, 0.06f), new Color(0.95f, 0.82f, 0.55f), 15);
                var stamina = Bar(panel, font, "Vigor", new Vector2(18, 38), new Vector2(340, 18),
                    new Color(0.42f, 0.56f, 0.2f), new Color(0.85f, 0.85f, 0.6f), 12);
                var mana = Bar(panel, font, "Mana", new Vector2(18, 14), new Vector2(340, 18),
                    new Color(0.16f, 0.32f, 0.72f), new Color(0.7f, 0.8f, 1f), 12);

                // Contaminação da peste (Terras Alagadas): acima dos recursos, só aparece quando há contaminação.
                var plaguePanel = Child(root.transform, "Peste");
                plaguePanel.anchorMin = plaguePanel.anchorMax = Vector2.zero;
                plaguePanel.pivot = Vector2.zero;
                plaguePanel.anchoredPosition = new Vector2(36, 152);
                plaguePanel.sizeDelta = new Vector2(430, 40);
                var plagueGroup = plaguePanel.gameObject.AddComponent<CanvasGroup>();
                plagueGroup.interactable = false;
                plagueGroup.blocksRaycasts = false;
                plagueGroup.alpha = 0f;
                var plaguePlate = Img(plaguePanel, "Placa", BronzeDark);
                Stretch(plaguePlate.rectTransform);
                Stretch(Img(plaguePlate.rectTransform, "Fundo", Back).rectTransform, 3);
                var plagueBar = Bar(plaguePanel, font, "Peste", new Vector2(18, 10), new Vector2(394, 20),
                    new Color(0.24f, 0.36f, 0.1f), new Color(0.55f, 0.62f, 0.3f), 12);
                var plague = plaguePanel.gameObject.AddComponent<PlagueStatusDisplay>();
                var pso2 = new SerializedObject(plague);
                pso2.FindProperty("bar").objectReferenceValue = plagueBar;
                pso2.FindProperty("group").objectReferenceValue = plagueGroup;
                pso2.ApplyModifiedPropertiesWithoutUndo();

                BuildExperience(root.transform, font);
                BuildTargetBar(root.transform, font);
                BuildBanner(root, font);

                // Mensagens curtas (centro inferior)
                var toast = Txt(root.transform, font, "Mensagens", "", 20, TextAnchor.LowerCenter, new Color(0.95f, 0.9f, 0.78f));
                var tr = toast.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0f);
                tr.pivot = new Vector2(0.5f, 0f);
                tr.anchoredPosition = new Vector2(0, 175);
                tr.sizeDelta = new Vector2(900, 120);
                BuildHotbar(root.transform, font);

                // Mira: ponto central discreto
                var dot = Img(root.transform, "Mira", new Color(0.95f, 0.9f, 0.8f, 0.75f));
                var dr = dot.rectTransform;
                dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f);
                dr.sizeDelta = new Vector2(4, 4);
                var dotShadow = dot.gameObject.AddComponent<Outline>();
                dotShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
                dotShadow.effectDistance = new Vector2(1, -1);

                // Aviso de interação (abaixo da mira)
                var prompt = Txt(root.transform, font, "Interacao", "", 20, TextAnchor.UpperCenter, new Color(1f, 0.88f, 0.55f));
                var ir = prompt.rectTransform;
                ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
                ir.pivot = new Vector2(0.5f, 1f);
                ir.anchoredPosition = new Vector2(0, -40);
                ir.sizeDelta = new Vector2(700, 40);
                var ip = prompt.gameObject.AddComponent<InteractionPromptView>();
                var ipSo = new SerializedObject(ip);
                ipSo.FindProperty("text").objectReferenceValue = prompt;
                ipSo.ApplyModifiedPropertiesWithoutUndo();

                var mt = toast.gameObject.AddComponent<MessageToast>();
                var mtSo = new SerializedObject(mt);
                mtSo.FindProperty("text").objectReferenceValue = toast;
                mtSo.ApplyModifiedPropertiesWithoutUndo();

                var hud = root.AddComponent<HUDController>();
                var so = new SerializedObject(hud);
                so.FindProperty("healthBar").objectReferenceValue = health;
                so.FindProperty("staminaBar").objectReferenceValue = stamina;
                so.FindProperty("manaBar").objectReferenceValue = mana;
                so.FindProperty("vignette").objectReferenceValue = vignette;
                so.FindProperty("resourcesGroup").objectReferenceValue = group;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Menu de pausa (Esc): mesmo estilo de vidro do menu principal.
                MenuArtImport.Prepare();
                var pause = root.AddComponent<PauseMenuScreen>();
                var pso = new SerializedObject(pause);
                pso.FindProperty("uiTiles").objectReferenceValue = MenuArtImport.Tiles;
                pso.FindProperty("glassPane").objectReferenceValue = MenuArtImport.MenuTexture("vidro_botao");
                pso.FindProperty("glassSheen").objectReferenceValue = MenuArtImport.MenuTexture("vidro_reflexo");
                var hover = pso.FindProperty("glassHoverSounds");
                hover.arraySize = 3;
                for (var i = 0; i < 3; i++) hover.GetArrayElementAtIndex(i).objectReferenceValue = MenuArtImport.Sound($"vidro_passar_{i + 1}");
                pso.FindProperty("confirmSound").objectReferenceValue = MenuArtImport.Sound("vidro_confirmar");
                pso.FindProperty("backSound").objectReferenceValue = MenuArtImport.Sound("vidro_voltar");
                pso.FindProperty("changeSound").objectReferenceValue = MenuArtImport.Sound("vidro_mudar");
                pso.FindProperty("mainMenuScene").stringValue = MainMenuSceneBuilder.SceneName;
                pso.ApplyModifiedPropertiesWithoutUndo();

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Topo central: nome e vida do inimigo atingido.</summary>
        private static void BuildTargetBar(Transform root, Font font)
        {
            var panel = Child(root, "Alvo");
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0, -40);
            panel.sizeDelta = new Vector2(620, 58);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            var title = Txt(panel, font, "Nome do Alvo", "", 20, TextAnchor.LowerCenter, new Color(0.95f, 0.88f, 0.72f));
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = Vector2.one;
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = Vector2.zero;
            tr.sizeDelta = new Vector2(0, 28);
            var shadow = title.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1, -1);

            var bar = Bar(panel, font, "Vida do Alvo", new Vector2(0, 4), new Vector2(620, 20),
                new Color(0.55f, 0.07f, 0.05f), new Color(0.95f, 0.82f, 0.55f), 12);
            // O nome fica em cima; dentro da barra só os números.
            var inner = bar.transform.Find("Nome");
            if (inner != null) Object.DestroyImmediate(inner.gameObject);

            var display = panel.gameObject.AddComponent<TargetHealthDisplay>();
            var so = new SerializedObject(display);
            so.FindProperty("bar").objectReferenceValue = bar;
            so.FindProperty("nameText").objectReferenceValue = title;
            so.FindProperty("group").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Centro inferior: [1][2] armas  [Q] habilidade  [3][4] consumíveis.</summary>
        private static void BuildHotbar(Transform root, Font font)
        {
            var bar = Child(root, "Atalhos");
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0, 22);
            bar.sizeDelta = new Vector2(430, 100);

            var w1 = HotSlot(bar, font, "Arma 1", "1", -170f, 64f, 84f, false);
            var w2 = HotSlot(bar, font, "Arma 2", "2", -95f, 64f, 84f, false);
            var ab = HotSlot(bar, font, "Habilidade", "Q", 0f, 84f, 84f, true);
            var i3 = HotSlot(bar, font, "Item 3", "3", 95f, 64f, 64f, false);
            var i4 = HotSlot(bar, font, "Item 4", "4", 170f, 64f, 64f, false);

            var weaponName = Txt(bar, font, "Nome da Arma", "", 15, TextAnchor.LowerCenter, new Color(0.9f, 0.84f, 0.7f));
            var wr = weaponName.rectTransform;
            wr.anchorMin = wr.anchorMax = new Vector2(0.5f, 1f);
            wr.pivot = new Vector2(0.5f, 0f);
            wr.anchoredPosition = new Vector2(-132f, 4f);
            wr.sizeDelta = new Vector2(220, 22);
            var abilityName = Txt(bar, font, "Nome da Habilidade", "", 13, TextAnchor.LowerCenter, new Color(0.75f, 0.68f, 0.9f));
            var ar = abilityName.rectTransform;
            ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 1f);
            ar.pivot = new Vector2(0.5f, 0f);
            ar.anchoredPosition = new Vector2(0f, 4f);
            ar.sizeDelta = new Vector2(200, 20);

            var view = bar.gameObject.AddComponent<HotbarView>();
            var so = new SerializedObject(view);
            so.FindProperty("weapon1").objectReferenceValue = w1;
            so.FindProperty("weapon2").objectReferenceValue = w2;
            so.FindProperty("ability").objectReferenceValue = ab;
            so.FindProperty("item3").objectReferenceValue = i3;
            so.FindProperty("item4").objectReferenceValue = i4;
            so.FindProperty("weaponName").objectReferenceValue = weaponName;
            so.FindProperty("abilityName").objectReferenceValue = abilityName;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static HotbarSlotView HotSlot(RectTransform parent, Font font, string name, string key, float x, float w, float h, bool withCooldown)
        {
            var frame = Img(parent, name, new Color(0.4f, 0.29f, 0.15f));
            var r = frame.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(x, 0f);
            r.sizeDelta = new Vector2(w, h);
            var bg = Img(r, "Fundo", new Color(0.05f, 0.045f, 0.045f, 0.92f));
            Stretch(bg.rectTransform, 2);
            var icon = Img(r, "Icone", Color.white);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform, 6);

            Image fill = null;
            Text cdText = null;
            if (withCooldown)
            {
                fill = Img(r, "Recarga", new Color(0f, 0f, 0f, 0.7f));
                Stretch(fill.rectTransform, 2);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Radial360;
                fill.fillOrigin = (int)Image.Origin360.Top;
                fill.fillClockwise = false;
                fill.fillAmount = 0f;
                cdText = Txt(r, font, "Segundos", "", 22, TextAnchor.MiddleCenter, Color.white);
                Stretch(cdText.rectTransform, 0);
            }

            var keyText = Txt(r, font, "Tecla", key, 13, TextAnchor.UpperLeft, new Color(0.95f, 0.85f, 0.6f));
            Stretch(keyText.rectTransform, 4);
            var count = Txt(r, font, "Quantidade", "", 14, TextAnchor.LowerRight, Color.white);
            Stretch(count.rectTransform, 4);

            var view = frame.gameObject.AddComponent<HotbarSlotView>();
            var so = new SerializedObject(view);
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("count").objectReferenceValue = count;
            so.FindProperty("cooldownFill").objectReferenceValue = fill;
            so.FindProperty("cooldownText").objectReferenceValue = cdText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private const string Sfx = "Assets/_Athoria/Audio/SFX/Provisorios/";

        /// <summary>Canto inferior direito: emblema do nível + barra de XP + "+XP".</summary>
        private static void BuildExperience(Transform root, Font font)
        {
            var panel = Child(root, "Experiencia");
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = new Vector2(-36, 32);
            panel.sizeDelta = new Vector2(340, 70);

            var plate = Img(panel, "Placa", BronzeDark);
            Stretch(plate.rectTransform);
            var plateInner = Img(plate.rectTransform, "Fundo", Back);
            Stretch(plateInner.rectTransform, 3);

            // Emblema do nível
            var emblem = Img(panel, "Emblema", Bronze);
            var er = emblem.rectTransform;
            er.anchorMin = er.anchorMax = new Vector2(0f, 0.5f);
            er.pivot = new Vector2(0f, 0.5f);
            er.anchoredPosition = new Vector2(10, 0);
            er.sizeDelta = new Vector2(54, 54);
            var emblemInner = Img(er, "Interior", new Color(0.1f, 0.07f, 0.05f, 1f));
            Stretch(emblemInner.rectTransform, 3);
            var levelCaption = Txt(er, font, "Rotulo", "NÍVEL", 10, TextAnchor.UpperCenter, new Color(0.85f, 0.7f, 0.45f));
            Stretch(levelCaption.rectTransform, 2, 4);
            var levelText = Txt(er, font, "Numero", "1", 26, TextAnchor.LowerCenter, new Color(0.96f, 0.82f, 0.5f));
            Stretch(levelText.rectTransform, 2, 3);

            var xpBar = Bar(panel, font, "XP", new Vector2(76, 16), new Vector2(252, 18),
                new Color(0.72f, 0.55f, 0.18f), new Color(1f, 0.95f, 0.75f), 12);

            var gain = Txt(panel, font, "Ganho", "", 16, TextAnchor.MiddleRight, new Color(1f, 0.88f, 0.5f));
            var gr = gain.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(1f, 1f);
            gr.pivot = new Vector2(1f, 0f);
            gr.anchoredPosition = new Vector2(-12, -30);
            gr.sizeDelta = new Vector2(200, 22);

            var display = panel.gameObject.AddComponent<ExperienceDisplay>();
            var so = new SerializedObject(display);
            so.FindProperty("xpBar").objectReferenceValue = xpBar;
            so.FindProperty("levelText").objectReferenceValue = levelText;
            so.FindProperty("gainText").objectReferenceValue = gain;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Faixa central de avisos (nível, descobertas).</summary>
        private static void BuildBanner(GameObject root, Font font)
        {
            var banner = Child(root.transform, "Aviso Central");
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0.74f);
            banner.pivot = new Vector2(0.5f, 0.5f);
            banner.sizeDelta = new Vector2(900, 120);
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            var shade = Img(banner, "Sombra", new Color(0f, 0f, 0f, 0.45f));
            var sr = shade.rectTransform;
            sr.anchorMin = new Vector2(0.1f, 0.1f);
            sr.anchorMax = new Vector2(0.9f, 0.9f);
            sr.offsetMin = sr.offsetMax = Vector2.zero;
            Line(banner, "Filete Superior", 0.92f, Bronze);
            Line(banner, "Filete Inferior", 0.08f, Bronze);

            var title = Txt(banner, font, "Titulo", "", 44, TextAnchor.MiddleCenter, new Color(0.96f, 0.8f, 0.45f));
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 0.42f);
            tr.anchorMax = new Vector2(1f, 0.92f);
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            var subtitle = Txt(banner, font, "Subtitulo", "", 20, TextAnchor.MiddleCenter, new Color(0.9f, 0.86f, 0.78f));
            var str = subtitle.rectTransform;
            str.anchorMin = new Vector2(0f, 0.12f);
            str.anchorMax = new Vector2(1f, 0.45f);
            str.offsetMin = str.offsetMax = Vector2.zero;

            if (!root.TryGetComponent<AudioSource>(out var audio)) audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var nb = banner.gameObject.AddComponent<NotificationBanner>();
            var so = new SerializedObject(nb);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("subtitle").objectReferenceValue = subtitle;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("levelUpClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "subir_nivel.wav");
            so.FindProperty("discoveryClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "local_descoberto.wav");
            so.FindProperty("enemyXpClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "ganho_xp.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ResourceBar Bar(RectTransform parent, Font font, string name, Vector2 pos, Vector2 size, Color fillColor, Color trailColor, int fontSize)
        {
            var bar = Child(parent, "Barra " + name);
            bar.anchorMin = bar.anchorMax = Vector2.zero;
            bar.pivot = Vector2.zero;
            bar.anchoredPosition = pos;
            bar.sizeDelta = size;

            var frame = Img(bar, "Moldura", Bronze);
            Stretch(frame.rectTransform);
            var back = Img(bar, "Fundo", new Color(0.07f, 0.06f, 0.06f, 1f));
            Stretch(back.rectTransform, 2);

            var area = Child(bar, "Area");
            Stretch(area, 3);

            var trail = Img(area, "Trilha", trailColor);
            FillRect(trail.rectTransform);
            var fill = Img(area, "Preenchimento", fillColor);
            FillRect(fill.rectTransform);

            var shine = Img(area, "Brilho", new Color(1f, 1f, 1f, 0.12f));
            shine.rectTransform.anchorMin = new Vector2(0, 0.55f);
            shine.rectTransform.anchorMax = Vector2.one;
            shine.rectTransform.offsetMin = shine.rectTransform.offsetMax = Vector2.zero;

            var nameText = Txt(bar, font, "Nome", name.ToUpperInvariant(), fontSize, TextAnchor.MiddleLeft, new Color(0.95f, 0.88f, 0.72f));
            Stretch(nameText.rectTransform, 8, 0);
            var valueText = Txt(bar, font, "Valor", "", fontSize, TextAnchor.MiddleRight, Color.white);
            Stretch(valueText.rectTransform, 8, 0);

            var rb = bar.gameObject.AddComponent<ResourceBar>();
            var so = new SerializedObject(rb);
            so.FindProperty("fill").objectReferenceValue = fill.rectTransform;
            so.FindProperty("trail").objectReferenceValue = trail.rectTransform;
            so.FindProperty("fillImage").objectReferenceValue = fill;
            so.FindProperty("label").objectReferenceValue = valueText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return rb;
        }

        private static void Line(RectTransform parent, string name, float y, Color color)
        {
            var img = Img(parent, name, color);
            var r = img.rectTransform;
            r.anchorMin = new Vector2(0.08f, y);
            r.anchorMax = new Vector2(0.92f, y);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(0, 3);
            r.anchoredPosition = Vector2.zero;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image Img(Transform parent, string name, Color color)
        {
            var r = Child(parent, name);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text Txt(Transform parent, Font font, string name, string text, int size, TextAnchor anchor, Color color)
        {
            var r = Child(parent, name);
            var t = r.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            var shadow = r.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.9f);
            shadow.effectDistance = new Vector2(1, -1);
            return t;
        }

        private static void Stretch(RectTransform r, float inset = 0f) => Stretch(r, inset, inset);

        private static void Stretch(RectTransform r, float insetX, float insetY)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(insetX, insetY);
            r.offsetMax = new Vector2(-insetX, -insetY);
        }

        private static void FillRect(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0f, 0.5f);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
