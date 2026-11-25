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
            CharacterContentFactory.GenerateAll();
            MenuArtImport.Prepare();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Abrir cena nova descarrega assets sem uso; recarrega o banco.
            var database = AssetDatabase.LoadAssetAtPath<Athoria.Gameplay.Characters.CharacterDatabase>(CharacterContentFactory.DatabasePath);

            var canvas = MenuArtImport.NewMenuScene();
            var screen = canvas.AddComponent<CharacterSelectScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("database").objectReferenceValue = database;
            so.FindProperty("gameScene").stringValue = "TerrasAlagadas";
            so.FindProperty("menuScene").stringValue = MainMenuSceneBuilder.SceneName;
            so.FindProperty("uiTiles").objectReferenceValue = MenuArtImport.Tiles;
            so.FindProperty("glassPane").objectReferenceValue = MenuArtImport.MenuTexture("vidro_botao");
            so.FindProperty("glassSheen").objectReferenceValue = MenuArtImport.MenuTexture("vidro_reflexo");
            PixelFxImporter.ImportAll();
            var petals = PixelFxImporter.Load("fx_petalas_ipe");
            so.FindProperty("petals").objectReferenceValue = petals;
            var hover = so.FindProperty("glassHoverSounds");
            hover.arraySize = 3;
            for (var i = 0; i < 3; i++) hover.GetArrayElementAtIndex(i).objectReferenceValue = MenuArtImport.Sound($"selecao_passar_{i + 1}");
            so.FindProperty("confirmSound").objectReferenceValue = MenuArtImport.Sound("selecao_confirmar");
            so.FindProperty("backSound").objectReferenceValue = MenuArtImport.Sound("selecao_voltar");
            so.FindProperty("changeSound").objectReferenceValue = MenuArtImport.Sound("selecao_girar");
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            MenuArtImport.OrderBuildScenes();
            Debug.Log("[Athoria] Cena de seleção criada em " + ScenePath);
        }

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
    }
}
