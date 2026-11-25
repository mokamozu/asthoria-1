using System.Collections.Generic;
using System.IO;
using Athoria.Gameplay.Characters;
using Athoria.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Ferramenta de teste: com o jogo rodando (Play), passa por todas as cavaleiras e
    /// fotografa a tela com cada arma na mão (pasta HandsShots/ na raiz do projeto).
    /// Serve para revisar mãos e empunhaduras sem trocar de personagem pelo menu.
    /// </summary>
    public static class HandPhotoBooth
    {
        private const string OutFolder = "HandsShots";
        private static readonly Queue<System.Action> Steps = new();
        private static double _nextAt;

        [MenuItem("Athoria/Debug/Fotografar mãos de todas as cavaleiras (Play)")]
        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Athoria] Dê Play numa cena com a jogadora antes."); return; }
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            var db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharacterContentFactory.DatabasePath);
            if (player == null || db == null) { Debug.LogWarning("[Athoria] Jogadora ou banco de personagens não encontrado."); return; }
            Directory.CreateDirectory(OutFolder);
            if (Athoria.Core.GameBootstrap.Instance != null) Athoria.Core.GameBootstrap.Instance.SetPaused(false);   // tira a pausa
            var loadout = player.GetComponent<WeaponLoadout>();
            Steps.Clear();
            foreach (var c in db.Characters)
            {
                var character = c;
                Steps.Enqueue(() => player.ApplyCharacter(character));
                for (var slot = 0; slot < WeaponLoadout.QuickSlotCount; slot++)
                {
                    var s = slot;
                    var hasWeapon = character.StartingWeapons != null && s < character.StartingWeapons.Length && character.StartingWeapons[s] != null;
                    if (!hasWeapon) continue;
                    Steps.Enqueue(() => { if (loadout.ActiveSlot != s) loadout.SelectSlot(s); });
                    Steps.Enqueue(() => ScreenCapture.CaptureScreenshot($"{OutFolder}/{character.name}_{s + 1}.png"));
                }
            }
            Steps.Enqueue(() => Debug.Log($"[Athoria] Fotos das mãos salvas em {Path.GetFullPath(OutFolder)}"));
            _nextAt = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!Application.isPlaying) { Steps.Clear(); EditorApplication.update -= Tick; return; }
            if (EditorApplication.timeSinceStartup < _nextAt) return;
            if (Steps.Count == 0) { EditorApplication.update -= Tick; return; }
            Steps.Dequeue()();
            _nextAt = EditorApplication.timeSinceStartup + 1.6;   // tempo de abaixar/sacar a arma
        }
    }
}
