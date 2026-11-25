using System.IO;
using System.Linq;
using Athoria.Core.Data;
using Athoria.Gameplay.Characters;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Save;
using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Gera Resources/BancoDeDados com todos os itens e personagens, para o
    /// save converter Ids de volta em assets. Também garante que todo dado do
    /// jogo tenha o Id = GUID do asset.
    /// </summary>
    public static class GameDatabaseBuilder
    {
        private const string Folder = "Assets/_Athoria/Resources";
        public const string Path = Folder + "/" + GameDatabase.ResourcePath + ".asset";

        [MenuItem("Athoria/Dados/Atualizar banco de dados do save")]
        public static void BuildMenu()
        {
            var db = Build();
            Debug.Log($"[Athoria] Banco de dados atualizado: {Path}");
            Selection.activeObject = db;
        }

        public static GameDatabase Build()
        {
            Directory.CreateDirectory(Folder);
            foreach (var guid in AssetDatabase.FindAssets("t:GameData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<GameData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null) data.EditorEnsureId();
            }

            var items = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/_Athoria" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(i => i != null).OrderBy(i => i.name).ToArray();
            var characters = AssetDatabase.FindAssets("t:CharacterData", new[] { "Assets/_Athoria" })
                .Select(g => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null).OrderBy(c => c.name).ToArray();

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(Path);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(db, Path);
            }
            db.EditorSet(items, characters);
            AssetDatabase.SaveAssets();
            return db;
        }
    }
}
