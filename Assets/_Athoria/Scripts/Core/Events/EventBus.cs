using System;
using System.Collections.Generic;
using UnityEngine;

namespace Athoria.Core.Events
{
    /// <summary>Marca structs que podem circular pelo <see cref="EventBus"/>.</summary>
    public interface IGameEvent { }

    /// <summary>
    /// Barramento de eventos tipado. Sistemas publicam e assinam eventos sem
    /// precisar conhecer uns aos outros (ex.: PlayerExperience publica XpGained,
    /// HUDController escuta e atualiza a barra).
    /// Regra: quem assina em OnEnable deve cancelar a assinatura em OnDisable.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> s_Handlers = new();

        public static void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null) return;
            var type = typeof(T);
            s_Handlers.TryGetValue(type, out var existing);
            s_Handlers[type] = Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null) return;
            var type = typeof(T);
            if (!s_Handlers.TryGetValue(type, out var existing)) return;

            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) s_Handlers.Remove(type);
            else s_Handlers[type] = remaining;
        }

        public static void Publish<T>(T evt) where T : struct, IGameEvent
        {
            if (!s_Handlers.TryGetValue(typeof(T), out var del)) return;

            // Um ouvinte com erro não impede os outros de receberem o evento.
            foreach (var handler in del.GetInvocationList())
            {
                try { ((Action<T>)handler).Invoke(evt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public static int ListenerCount<T>() where T : struct, IGameEvent =>
            s_Handlers.TryGetValue(typeof(T), out var del) ? del.GetInvocationList().Length : 0;

        // Limpa assinaturas antigas ao entrar em Play com "Enter Play Mode Options" ativado.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => s_Handlers.Clear();
    }
}
