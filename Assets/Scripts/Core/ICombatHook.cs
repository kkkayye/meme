using System;
using RuneArena.Combat;

namespace RuneArena.Core
{
    /// <summary>A rune/item mechanic attached to one unit. Attach subscribes to EventBus events (filtering by owner); Detach unsubscribes.</summary>
    public interface ICombatHook
    {
        void Attach(Unit owner);
        void Detach();
    }

    /// <summary>Convenience base for hooks: stores Owner, guards double attach, and offers IsOwner/IsAlly helpers. Override OnAttach/OnDetach.</summary>
    public abstract class CombatHookBase : ICombatHook
    {
        public Unit Owner { get; private set; }
        public bool IsAttached => Owner != null;

        public void Attach(Unit owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (Owner != null) Detach();
            Owner = owner;
            OnAttach();
        }

        public void Detach()
        {
            if (Owner == null) return;
            OnDetach();
            Owner = null;
        }

        /// <summary>Subscribe to EventBus here.</summary>
        protected abstract void OnAttach();

        /// <summary>Unsubscribe from EventBus here.</summary>
        protected abstract void OnDetach();

        protected bool IsOwner(Unit unit)
        {
            return unit != null && ReferenceEquals(unit, Owner);
        }

        protected bool IsOwnerAlive()
        {
            return Owner != null && Owner.IsAlive;
        }
    }
}
