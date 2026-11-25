using System.IO;
using Athoria.UI.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.Editor
{
    /// <summary>Monta o Prefab da tela de inventário (Canvas próprio, acima da HUD).</summary>
    public static class InventoryUiBuilder
    {
        public const string PrefabPath = "Assets/_Athoria/Prefabs/UI/Inventario.prefab";
        private const float Cell = 56f;
        private const int Columns = 10, Rows = 6;

        private static readonly Color Bronze = new(0.5f, 0.36f, 0.18f);
        private static readonly Color BronzeDark = new(0.22f, 0.15f, 0.08f);
        private static readonly Color Back = new(0.05f, 0.045f, 0.045f, 0.97f);
        private static readonly Color Gold = new(0.96f, 0.8f, 0.45f);

        [MenuItem("Athoria/Prefabs/Recriar Prefab do Inventário")]
        public static void RebuildMenu()
        {
            Build();
            Debug.Log("[Athoria] Prefab do inventário recriado em " + PrefabPath);
        }

        public static GameObject GetOrBuild()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return existing != null ? existing : Build();
        }

        public static GameObject Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("Inventario", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                root.AddComponent<GraphicRaycaster>();
                var group = root.AddComponent<CanvasGroup>();
                var screen = root.AddComponent<InventoryScreen>();

                // Fundo escuro clicável (largar item no chão)
                var backdrop = Img(root.transform, "Fundo", new Color(0f, 0f, 0f, 0.55f), true);
                Stretch(backdrop.rectTransform, 0);
                var bd = backdrop.gameObject.AddComponent<InventoryBackdrop>();
                SetRef(bd, "screen", screen);

                // Painel: equipamento à esquerda, grade à direita
                var gridW = Columns * Cell;
                var gridH = Rows * Cell;
                const float panelW = 1240f, panelH = 730f, gridLeft = 640f, gridTop = 90f;
                var panel = Img(root.transform, "Painel", BronzeDark, true);   // bloqueia clique no fundo
                var pr = panel.rectTransform;
                pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
                pr.sizeDelta = new Vector2(panelW, panelH);
                var inner = Img(pr, "Interior", Back, false);
                Stretch(inner.rectTransform, 3);

                var title = Txt(pr, font, "Titulo", "INVENTÁRIO  •  ANÉIS E AMULETO", 28, TextAnchor.MiddleCenter, Gold);
                TopBand(title.rectTransform, 14, 40);
                var line = Img(pr, "Filete", Bronze, false);
                line.rectTransform.anchorMin = new Vector2(0.2f, 1f);
                line.rectTransform.anchorMax = new Vector2(0.8f, 1f);
                line.rectTransform.sizeDelta = new Vector2(0, 2);
                line.rectTransform.anchoredPosition = new Vector2(0, -60);
                var divider = Img(pr, "Divisoria", Bronze, false);
                At(divider.rectTransform, 615f, 400f, 2f, 600f);

                // --- Equipamento ---
                var charName = Txt(pr, font, "Nome da Personagem", "", 22, TextAnchor.MiddleCenter, Color.white);
                At(charName.rectTransform, 310f, 105f, 500f, 50f);
                var art = Img(pr, "Arte da Personagem", Color.white, false);
                art.preserveAspect = true;
                At(art.rectTransform, 310f, 365f, 230f, 400f);

                // Só acessórios: as armas são fixas de cada cavaleira e as armaduras, estéticas.
                var slots = new System.Collections.Generic.List<EquipmentSlotView>
                {
                    Slot(pr, font, Gameplay.Inventory.EquipmentSlot.Amulet, "Amuleto", 310f, 618f, 80f, 80f),
                    Slot(pr, font, Gameplay.Inventory.EquipmentSlot.Ring1, "Anel", 110f, 280f, 74f, 74f),
                    Slot(pr, font, Gameplay.Inventory.EquipmentSlot.Ring2, "Anel", 110f, 420f, 74f, 74f),
                    Slot(pr, font, Gameplay.Inventory.EquipmentSlot.Ring3, "Anel", 510f, 280f, 74f, 74f),
                    Slot(pr, font, Gameplay.Inventory.EquipmentSlot.Ring4, "Anel", 510f, 420f, 74f, 74f),
                };

                // --- Grade ---
                var gridFrame = Img(pr, "Moldura da Grade", Bronze, false);
                TopLeft(gridFrame.rectTransform, gridLeft - 3f, gridTop - 3f, gridW + 6f, gridH + 6f);
                var grid = Child(pr, "Grade");
                TopLeft(grid, gridLeft, gridTop, gridW, gridH);
                var itemsLayer = Child(pr, "Itens");
                TopLeft(itemsLayer, gridLeft, gridTop, gridW, gridH);

                // Modelo de célula
                var cellImg = Img(grid, "Celula (modelo)", new Color(0.09f, 0.08f, 0.075f), true);
                var cell = cellImg.gameObject.AddComponent<InventoryCell>();
                SetRef(cell, "image", cellImg);

                // Modelo de item
                var itemTemplate = BuildItemView(itemsLayer, font, "Item (modelo)");
                var held = BuildItemView(root.transform, font, "Item na Mao");

                // Estatísticas (abaixo da grade)
                var statsHead = Txt(pr, font, "Cab Atributos", "ATRIBUTOS", 16, TextAnchor.MiddleLeft, Gold);
                TopLeft(statsHead.rectTransform, gridLeft, gridTop + gridH + 16f, gridW, 24f);
                var statsText = Txt(pr, font, "Atributos", "", 15, TextAnchor.UpperLeft, new Color(0.88f, 0.84f, 0.76f));
                statsText.fontStyle = FontStyle.Normal;
                TopLeft(statsText.rectTransform, gridLeft, gridTop + gridH + 42f, gridW, 110f);

                // Rodapé
                var goldText = Txt(pr, font, "Ouro", "Ouro: 0", 18, TextAnchor.MiddleLeft, Gold);
                TopLeft(goldText.rectTransform, gridLeft, panelH - 92f, gridW * 0.5f, 26f);
                var weightText = Txt(pr, font, "Peso", "Peso: 0 / 60", 18, TextAnchor.MiddleRight, new Color(0.85f, 0.8f, 0.7f));
                TopLeft(weightText.rectTransform, gridLeft + gridW * 0.5f, panelH - 92f, gridW * 0.5f, 26f);
                var hint = Txt(pr, font, "Dica", "Clique: pegar / soltar  •  Botão direito: usar / equipar / guardar  •  Clique fora: largar no chão  •  I, Tab ou K: fechar",
                    13, TextAnchor.MiddleCenter, new Color(0.65f, 0.6f, 0.52f));
                BottomBand(hint.rectTransform, 18, 22, 20);

                // Dica do item (tooltip)
                var tooltipRoot = Child(root.transform, "Dica do Item");
                Stretch(tooltipRoot, 0);
                var tp = Img(tooltipRoot, "Painel", new Color(0.04f, 0.035f, 0.035f, 0.97f), false);
                var tpr = tp.rectTransform;
                tpr.anchorMin = tpr.anchorMax = new Vector2(0.5f, 0.5f);
                tpr.pivot = new Vector2(0f, 1f);
                tpr.sizeDelta = new Vector2(360, 100);
                var outline = tp.gameObject.AddComponent<Outline>();
                outline.effectColor = Bronze;
                outline.effectDistance = new Vector2(2, -2);
                var layout = tp.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(14, 14, 12, 12);
                layout.spacing = 6;
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;
                var fitter = tp.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var tName = Txt(tpr, font, "Nome", "", 22, TextAnchor.UpperLeft, Color.white);
                var tType = Txt(tpr, font, "Tipo", "", 14, TextAnchor.UpperLeft, new Color(0.7f, 0.65f, 0.55f));
                var tStats = Txt(tpr, font, "Atributos", "", 16, TextAnchor.UpperLeft, new Color(0.9f, 0.88f, 0.8f));
                var tDesc = Txt(tpr, font, "Descricao", "", 15, TextAnchor.UpperLeft, new Color(0.78f, 0.74f, 0.66f));
                tDesc.fontStyle = FontStyle.Italic;
                var tCompare = Txt(tpr, font, "Comparacao", "", 15, TextAnchor.UpperLeft, new Color(0.9f, 0.88f, 0.8f));
                var tFoot = Txt(tpr, font, "Rodape", "", 14, TextAnchor.UpperLeft, new Color(0.85f, 0.72f, 0.45f));
                var tooltip = tooltipRoot.gameObject.AddComponent<ItemTooltip>();
                var tso = new SerializedObject(tooltip);
                tso.FindProperty("panel").objectReferenceValue = tpr;
                tso.FindProperty("nameText").objectReferenceValue = tName;
                tso.FindProperty("typeText").objectReferenceValue = tType;
                tso.FindProperty("statsText").objectReferenceValue = tStats;
                tso.FindProperty("descriptionText").objectReferenceValue = tDesc;
                tso.FindProperty("footerText").objectReferenceValue = tFoot;
                tso.FindProperty("compareText").objectReferenceValue = tCompare;
                tso.ApplyModifiedPropertiesWithoutUndo();

                var so = new SerializedObject(screen);
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("canvasRect").objectReferenceValue = root.transform;
                so.FindProperty("grid").objectReferenceValue = grid;
                so.FindProperty("cellTemplate").objectReferenceValue = cell;
                so.FindProperty("itemsLayer").objectReferenceValue = itemsLayer;
                so.FindProperty("itemTemplate").objectReferenceValue = itemTemplate;
                so.FindProperty("heldView").objectReferenceValue = held;
                so.FindProperty("tooltip").objectReferenceValue = tooltip;
                so.FindProperty("goldText").objectReferenceValue = goldText;
                so.FindProperty("weightText").objectReferenceValue = weightText;
                so.FindProperty("cellSize").floatValue = Cell;
                var slotsProp = so.FindProperty("equipmentSlots");
                slotsProp.arraySize = slots.Count;
                for (var i = 0; i < slots.Count; i++) slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                so.FindProperty("characterArt").objectReferenceValue = art;
                so.FindProperty("characterName").objectReferenceValue = charName;
                so.FindProperty("statsText").objectReferenceValue = statsText;
                so.ApplyModifiedPropertiesWithoutUndo();

                group.alpha = 0f;
                group.blocksRaycasts = false;
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static EquipmentSlotView Slot(RectTransform parent, Font font, Gameplay.Inventory.EquipmentSlot slot, string label,
            float x, float y, float w, float h)
        {
            var frame = Img(parent, "Espaco " + label, new Color(0.3f, 0.22f, 0.12f), true);
            At(frame.rectTransform, x, y, w, h);
            var bg = Img(frame.rectTransform, "Fundo", new Color(0.07f, 0.06f, 0.055f, 0.95f), false);
            Stretch(bg.rectTransform, 2);
            var icon = Img(frame.rectTransform, "Icone", Color.white, false);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform, 6);
            var text = Txt(frame.rectTransform, font, "Rotulo", label, 11, TextAnchor.MiddleCenter, new Color(0.55f, 0.48f, 0.38f));
            text.fontStyle = FontStyle.Normal;
            Stretch(text.rectTransform, 4);
            var view = frame.gameObject.AddComponent<EquipmentSlotView>();
            var so = new SerializedObject(view);
            so.FindProperty("slot").enumValueIndex = (int)slot;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("label").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>Posiciona pelo centro, em pixels a partir do canto superior esquerdo do pai.</summary>
        private static void At(RectTransform r, float centerX, float centerY, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(centerX, -centerY);
            r.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Posiciona pelo canto superior esquerdo, em pixels a partir do canto superior esquerdo do pai.</summary>
        private static void TopLeft(RectTransform r, float left, float top, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(left, -top);
            r.sizeDelta = new Vector2(w, h);
        }

        private static InventoryItemView BuildItemView(Transform parent, Font font, string name)
        {
            var rootRect = Child(parent, name);
            rootRect.sizeDelta = new Vector2(Cell, Cell);
            var border = Img(rootRect, "Borda", Color.white, false);
            Stretch(border.rectTransform, 0);
            var bg = Img(rootRect, "Fundo", Color.black, false);
            Stretch(bg.rectTransform, 2);
            var icon = Img(rootRect, "Icone", Color.white, false);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform, 5);
            var qty = Txt(rootRect, font, "Quantidade", "", 16, TextAnchor.LowerRight, Color.white);
            Stretch(qty.rectTransform, 5);
            var view = rootRect.gameObject.AddComponent<InventoryItemView>();
            var so = new SerializedObject(view);
            so.FindProperty("border").objectReferenceValue = border;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("quantity").objectReferenceValue = qty;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ---------- utilitários ----------

        private static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image Img(Transform parent, string name, Color color, bool raycast)
        {
            var img = Child(parent, name).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
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
            shadow.effectDistance = new Vector2(1, -1);
            return t;
        }

        private static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        private static void TopBand(RectTransform r, float top, float height)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0, -top);
            r.sizeDelta = new Vector2(-40, height);
        }

        private static void BottomBand(RectTransform r, float bottom, float height, float sideMargin)
        {
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0, bottom);
            r.sizeDelta = new Vector2(-2 * sideMargin, height);
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
