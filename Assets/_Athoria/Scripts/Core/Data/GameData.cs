using UnityEngine;

namespace Athoria.Core.Data
{
    /// <summary>
    /// Base de todos os dados do jogo em ScriptableObject (personagens, armas,
    /// armaduras, itens, inimigos...). O ID é o GUID do próprio asset, então é
    /// único e estável — inclusive ao duplicar um asset no Project.
    /// Saves e bancos de dados devem referenciar itens sempre pelo Id.
    /// </summary>
    public abstract class GameData : ScriptableObject
    {
        [SerializeField, Tooltip("Gerado automaticamente a partir do GUID do asset. Não edite.")]
        private string id;

        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 6)] private string description;
        [SerializeField] private Sprite icon;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;

#if UNITY_EDITOR
        /// <summary>Usado por ferramentas de editor que geram dados automaticamente.</summary>
        public void EditorSetInfo(string newDisplayName, string newDescription, Sprite newIcon = null)
        {
            displayName = newDisplayName;
            description = newDescription;
            if (newIcon != null) icon = newIcon;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        protected virtual void OnValidate()
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(path)) return;

            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            if (id == guid) return;

            id = guid;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
