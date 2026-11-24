using UnityEngine;

namespace Athoria.Gameplay.Characters
{
    /// <summary>Lista de personagens jogáveis mostradas na tela de seleção.</summary>
    [CreateAssetMenu(menuName = "Athoria/Personagens/Banco de Personagens", fileName = "BancoDePersonagens")]
    public sealed class CharacterDatabase : ScriptableObject
    {
        [SerializeField] private CharacterData[] characters;

        public CharacterData[] Characters => characters;
        public int Count => characters != null ? characters.Length : 0;
        public CharacterData Default => Count > 0 ? characters[0] : null;

#if UNITY_EDITOR
        public void EditorSet(CharacterData[] list)
        {
            characters = list;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>Guarda a escolha feita no menu até a cena do jogo carregar.</summary>
    public static class PlayerSession
    {
        public static CharacterData SelectedCharacter { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => SelectedCharacter = null;
    }
}
