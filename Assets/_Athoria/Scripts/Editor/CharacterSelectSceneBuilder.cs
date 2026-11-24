using System.IO;
using System.Linq;
using Athoria.UI.Menus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Athoria.Editor
{
    /// <summary>Gera a cena da tela de seleção de personagens (primeira cena do jogo).</summary>
    public static class CharacterSelectSceneBuilder
    {
        public const string ScenePath = "Assets/_Athoria/Scenes/SelecaoPersonagem.unity";
        private const string GameScenePath = "Assets/_Athoria/Scenes/Sandbox.unity";

        private static readonly Color Bronze = new(0.5f, 0.36f, 0.18f);
        private static readonly Color BronzeDark = new(0.22f, 0.15f, 0.08f);
        private static readonly Color Panel = new(0.06f, 0.05f, 0.05f, 0.94f);
        private static readonly Color Gold = new(0.96f, 0.8f, 0.45f);
        private static readonly Color Parchment = new(0.9f, 0.86f, 0.78f);

        [MenuItem("Athoria/Cenas/Criar ou recriar Seleção de Personagens")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var database = CharacterContentFactory.GenerateAll();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Abrir cena nova descarrega assets sem uso; recarrega o banco.
            database = AssetDatabase.LoadAssetAtPath<Athoria.Gameplay.Characters.CharacterDatabase>(CharacterContentFactory.DatabasePath);

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.035f, 0.03f, 0.03f);
            camGo.tag = "MainCamera";

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            var uiModule = es.AddComponent<InputSystemUIInputModule>();
            ConfigureUiInput(uiModule);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            // Título
            var title = Txt(root, font, "Titulo", "ESCOLHA SUA CAVALEIRA", 42, TextAnchor.MiddleCenter, Gold);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1200, 60));
            var line = Img(root, "Filete Titulo", Bronze);
            Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(700, 3));

            // Lista de cartões (esquerda)
            var list = Child(root, "Lista");
            list.anchorMin = list.anchorMax = new Vector2(0f, 0.5f);
            list.pivot = new Vector2(0f, 0.5f);
            list.anchoredPosition = new Vector2(70, -20);
            list.sizeDelta = new Vector2(380, 560);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            var template = BuildCard(list, font);

            // Arte de corpo inteiro (centro)
            var body = Img(root, "Corpo Inteiro", Color.white);
            body.preserveAspect = true;
            var br = body.rectTransform;
            br.anchorMin = new Vector2(0.24f, 0.06f);
            br.anchorMax = new Vector2(0.56f, 0.9f);
            br.offsetMin = br.offsetMax = Vector2.zero;

            // Painel de detalhes (direita)
            var details = Child(root, "Detalhes");
            details.anchorMin = new Vector2(0.57f, 0.08f);
            details.anchorMax = new Vector2(0.97f, 0.86f);
            details.offsetMin = details.offsetMax = Vector2.zero;
            var frame = Img(details, "Moldura", BronzeDark);
            Stretch(frame.rectTransform, 0);
            var inner = Img(details, "Fundo", Panel);
            Stretch(inner.rectTransform, 3);

            var emblem = Img(details, "Brasao", Color.white);
            emblem.preserveAspect = true;
            Place(emblem.rectTransform, new Vector2(1f, 1f), new Vector2(-80, -80), new Vector2(120, 120));

            var nameText = Txt(details, font, "Nome", "", 46, TextAnchor.LowerLeft, Color.white);
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 24;
            nameText.resizeTextMaxSize = 46;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(nameText.rectTransform, 0.94f, 0.86f);
            var titleText = Txt(details, font, "Titulo", "", 24, TextAnchor.UpperLeft, Gold);
            titleText.fontStyle = FontStyle.Italic;
            Anchor(titleText.rectTransform, 0.855f, 0.81f);
            var classText = Txt(details, font, "Classe e Regiao", "", 19, TextAnchor.UpperLeft, new Color(0.8f, 0.72f, 0.58f));
            Anchor(classText.rectTransform, 0.805f, 0.765f);
            var accent = Img(details, "Filete", Gold);
            var ar = accent.rectTransform;
            ar.anchorMin = new Vector2(0.05f, 0.75f);
            ar.anchorMax = new Vector2(0.95f, 0.75f);
            ar.sizeDelta = new Vector2(0, 2);

            var desc = Txt(details, font, "Descricao", "", 19, TextAnchor.UpperLeft, Parchment);
            desc.fontStyle = FontStyle.Normal;
            Anchor(desc.rectTransform, 0.73f, 0.55f);

            var traitsHead = Txt(details, font, "Cab Caracteristicas", "CARACTERÍSTICAS", 17, TextAnchor.UpperLeft, Gold);
            Anchor(traitsHead.rectTransform, 0.54f, 0.505f);
            var traits = Txt(details, font, "Caracteristicas", "", 17, TextAnchor.UpperLeft, Parchment);
            traits.fontStyle = FontStyle.Normal;
            traits.supportRichText = true;
            Anchor(traits.rectTransform, 0.5f, 0.35f);

            var equipHead = Txt(details, font, "Cab Equipamento", "EQUIPAMENTO INICIAL", 17, TextAnchor.UpperLeft, Gold);
            Anchor(equipHead.rectTransform, 0.34f, 0.305f);
            var equip = Txt(details, font, "Equipamento", "", 17, TextAnchor.UpperLeft, Parchment);
            equip.fontStyle = FontStyle.Normal;
            Anchor(equip.rectTransform, 0.3f, 0.2f);

            var stats = Txt(details, font, "Valores", "", 17, TextAnchor.MiddleLeft, new Color(0.85f, 0.8f, 0.7f));
            Anchor(stats.rectTransform, 0.18f, 0.12f);

            // Botão começar
            var buttonImg = Img(root, "Botao Comecar", Bronze);
            buttonImg.raycastTarget = true;
            Place(buttonImg.rectTransform, new Vector2(1f, 0f), new Vector2(-260, 60), new Vector2(420, 72));
            var btn = buttonImg.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.1f, 0.9f);
            colors.pressedColor = new Color(0.7f, 0.6f, 0.5f);
            btn.colors = colors;
            var btnInner = Img(buttonImg.rectTransform, "Interior", new Color(0.12f, 0.08f, 0.05f));
            Stretch(btnInner.rectTransform, 3);
            var btnText = Txt(buttonImg.rectTransform, font, "Texto", "COMEÇAR JORNADA  (Enter)", 24, TextAnchor.MiddleCenter, Gold);
            Stretch(btnText.rectTransform, 0);

            var hint = Txt(root, font, "Dica", "← / → para trocar  •  clique numa personagem", 16, TextAnchor.MiddleLeft, new Color(0.6f, 0.55f, 0.48f));
            Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(330, 40), new Vector2(600, 30));

            var screen = canvasGo.AddComponent<CharacterSelectScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("database").objectReferenceValue = database;
            so.FindProperty("gameScene").stringValue = "Sandbox";
            so.FindProperty("cardContainer").objectReferenceValue = list;
            so.FindProperty("cardTemplate").objectReferenceValue = template;
            so.FindProperty("fullBody").objectReferenceValue = body;
            so.FindProperty("emblem").objectReferenceValue = emblem;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("classRegionText").objectReferenceValue = classText;
            so.FindProperty("descriptionText").objectReferenceValue = desc;
            so.FindProperty("statsText").objectReferenceValue = stats;
            so.FindProperty("traitsText").objectReferenceValue = traits;
            so.FindProperty("equipmentText").objectReferenceValue = equip;
            so.FindProperty("accentLine").objectReferenceValue = accent;
            so.FindProperty("startButton").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Ordem do build: seleção primeiro, depois o jogo.
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != GameScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            if (File.Exists(GameScenePath)) scenes.Insert(1, new EditorBuildSettingsScene(GameScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[Athoria] Cena de seleção criada em " + ScenePath);
        }

        /// <summary>
        /// Liga o módulo de UI ao mapa "UI" do arquivo de controles do projeto.
        /// (AssignDefaultActions no editor cria objetos que não são salvos na cena.)
        /// </summary>
        internal static void ConfigureUiInput(InputSystemUIInputModule module)
        {
            const string path = "Assets/InputSystem_Actions.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(path);
            if (asset == null)
            {
                Debug.LogWarning("[Athoria] " + path + " não encontrado; a tela usará os controles padrão em tempo de execução.");
                return;
            }

            var refs = AssetDatabase.LoadAllAssetsAtPath(path).OfType<UnityEngine.InputSystem.InputActionReference>().ToList();
            UnityEngine.InputSystem.InputActionReference Find(string action) =>
                refs.FirstOrDefault(r => r.action != null && r.action.actionMap.name == "UI" && r.action.name == action);

            module.actionsAsset = asset;
            module.point = Find("Point");
            module.leftClick = Find("Click");
            module.middleClick = Find("MiddleClick");
            module.rightClick = Find("RightClick");
            module.scrollWheel = Find("ScrollWheel");
            module.move = Find("Navigate");
            module.submit = Find("Submit");
            module.cancel = Find("Cancel");
            module.trackedDevicePosition = Find("TrackedDevicePosition");
            module.trackedDeviceOrientation = Find("TrackedDeviceOrientation");
            EditorUtility.SetDirty(module);
        }

        private static CharacterCard BuildCard(RectTransform parent, Font font)
        {
            var frame = Img(parent, "Cartao (modelo)", Bronze);
            frame.raycastTarget = true;
            frame.rectTransform.sizeDelta = new Vector2(380, 116);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var inner = Img(frame.rectTransform, "Fundo", Panel);
            Stretch(inner.rectTransform, 3);

            var portrait = Img(frame.rectTransform, "Retrato", Color.white);
            portrait.preserveAspect = true;
            var pr = portrait.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f);
            pr.pivot = new Vector2(0f, 0.5f);
            pr.anchoredPosition = new Vector2(10, 0);
            pr.sizeDelta = new Vector2(96, 96);

            var name = Txt(frame.rectTransform, font, "Nome", "", 26, TextAnchor.LowerLeft, Color.white);
            var nr = name.rectTransform;
            nr.anchorMin = new Vector2(0f, 0.5f);
            nr.anchorMax = new Vector2(1f, 1f);
            nr.offsetMin = new Vector2(120, 0);
            nr.offsetMax = new Vector2(-10, -12);
            var cls = Txt(frame.rectTransform, font, "Classe", "", 17, TextAnchor.UpperLeft, new Color(0.85f, 0.72f, 0.5f));
            cls.fontStyle = FontStyle.Normal;
            var cr = cls.rectTransform;
            cr.anchorMin = new Vector2(0f, 0f);
            cr.anchorMax = new Vector2(1f, 0.5f);
            cr.offsetMin = new Vector2(120, 12);
            cr.offsetMax = new Vector2(-10, -4);

            var card = frame.gameObject.AddComponent<CharacterCard>();
            var so = new SerializedObject(card);
            so.FindProperty("button").objectReferenceValue = btn;
            so.FindProperty("portrait").objectReferenceValue = portrait;
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("classText").objectReferenceValue = cls;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        // ---------- utilitários de UI ----------

        private static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image Img(Transform parent, string name, Color color)
        {
            var img = Child(parent, name).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text Txt(Transform parent, Font font, string name, string text, int size, TextAnchor anchor, Color color)
        {
            var t = Child(parent, name).gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        private static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        /// <summary>Faixa horizontal dentro do painel (top/bottom em fração da altura), com margens laterais.</summary>
        private static void Anchor(RectTransform r, float top, float bottom)
        {
            r.anchorMin = new Vector2(0f, bottom);
            r.anchorMax = new Vector2(1f, top);
            r.offsetMin = new Vector2(36, 0);
            r.offsetMax = new Vector2(-170 * (top > 0.8f ? 1 : 0) - 36, 0);
        }

        private static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
