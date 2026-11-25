using System.IO;
using Athoria.UI.Menus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>Gera a cena do menu principal (primeira cena do jogo).</summary>
    public static class MainMenuSceneBuilder
    {
        public const string SceneName = "MenuPrincipal";
        public const string ScenePath = "Assets/_Athoria/Scenes/MenuPrincipal.unity";

        [MenuItem("Athoria/Cenas/Criar ou recriar Menu Principal")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            MenuArtImport.Prepare();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = MenuArtImport.NewMenuScene();
            var menu = canvas.AddComponent<MainMenuScreen>();
            var so = new SerializedObject(menu);
            so.FindProperty("uiTiles").objectReferenceValue = MenuArtImport.Tiles;
            so.FindProperty("glassPane").objectReferenceValue = MenuArtImport.MenuTexture("vidro_botao");
            so.FindProperty("glassSheen").objectReferenceValue = MenuArtImport.MenuTexture("vidro_reflexo");
            so.FindProperty("wall").objectReferenceValue = MenuArtImport.MenuTexture("parede_pedra");
            so.FindProperty("windowGlass").objectReferenceValue = MenuArtImport.MenuTexture("vitral_vidro");
            so.FindProperty("windowFrame").objectReferenceValue = MenuArtImport.MenuTexture("vitral_moldura");
            so.FindProperty("windowMask").objectReferenceValue = MenuArtImport.MenuTexture("vitral_mascara");
            so.FindProperty("sky").objectReferenceValue = MenuArtImport.Ambient("ceu_00");
            so.FindProperty("moon").objectReferenceValue = MenuArtImport.Ambient("lua");
            so.FindProperty("shadow").objectReferenceValue = MenuArtImport.Ambient("sombra");
            so.FindProperty("clouds").objectReferenceValue = MenuArtImport.Ambient("nuvem");
            so.FindProperty("selectionScene").stringValue = Path.GetFileNameWithoutExtension(CharacterSelectSceneBuilder.ScenePath);
            var hover = so.FindProperty("glassHoverSounds");
            hover.arraySize = 3;
            for (var i = 0; i < 3; i++) hover.GetArrayElementAtIndex(i).objectReferenceValue = MenuArtImport.Sound($"vidro_passar_{i + 1}");
            so.FindProperty("confirmSound").objectReferenceValue = MenuArtImport.Sound("vidro_confirmar");
            so.FindProperty("backSound").objectReferenceValue = MenuArtImport.Sound("vidro_voltar");
            so.FindProperty("changeSound").objectReferenceValue = MenuArtImport.Sound("vidro_mudar");
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            MenuArtImport.OrderBuildScenes();
            Debug.Log("[Athoria] Menu principal criado em " + ScenePath);
        }
    }
}
