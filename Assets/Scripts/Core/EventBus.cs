using System;
using System.Collections.Generic;

namespace RuneArena.Core
{
    /// <summary>Static typed publish/subscribe bus. Publish iterates over a snapshot so handlers may subscribe/unsubscribe during dispatch.</summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> Handlers = new Dictionary<Type, List<Delegate>>();
        private static readonly Dictionary<Type, Delegate[]> Snapshots = new Dictionary<Type, Delegate[]>();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Type type = typeof(T);
            if (!Handlers.TryGetValue(type, out List<Delegate> list))
            {
                list = new List<Delegate>();
                Handlers[type] = list;
            }
            list.Add(handler);
            Snapshots.Remove(type);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            Type type = typeof(T);
            if (!Handlers.TryGetValue(type, out List<Delegate> list)) return;
            if (list.Remove(handler)) Snapshots.Remove(type);
        }

        /// <summary>Dispatches to every subscriber. Exceptions in handlers propagate (never swallowed).</summary>
        public static void Publish<T>(T evt) where T : struct
        {
            Type type = typeof(T);
            if (!Handlers.TryGetValue(type, out List<Delegate> list) || list.Count == 0) return;
            if (!Snapshots.TryGetValue(type, out Delegate[] snapshot))
            {
                snapshot = list.ToArray();
                Snapshots[type] = snapshot;
            }
            for (int i = 0; i < snapshot.Length; i++)
            {
                ((Action<T>)snapshot[i])(evt);
            }
        }

        public static int SubscriberCount<T>() where T : struct
        {
            return Handlers.TryGetValue(typeof(T), out List<Delegate> list) ? list.Count : 0;
        }

        /// <summary>Removes every subscription of every event type.</summary>
        public static void Clear()
        {
            Handlers.Clear();
            Snapshots.Clear();
        }
    }
}
