using System;

namespace CrudDatastore.Framework.Internal
{
    internal class EntityEntry
    {
        public States State { get; private set; }
        public object Entry { get; private set; }
        public object Entity { get; private set; }
        public Action<object> OnCommit { get; private set; }
        public Action<object> OnCommitted { get; private set; }

        public EntityEntry(States state, object entry, object entity, Action<object> onCommit, Action<object> onCommitted)
        {
            State = state;
            Entry = entry;
            Entity = entity;
            OnCommit = onCommit;
            OnCommitted = onCommitted;
        }

        public void ChangeState(States state)
        {
            if (State != state)
            {
                State = state;
                OnCommit = (e) => { };
                OnCommitted = (e) => { };
            }
        }

        public void Commit()
        {
            State = States.Commited;
        }

        public bool UnCommited
        {
            get { return State != States.Commited; }
        }

        internal enum States
        {
            New = 1,
            Modified,
            Deleted,
            Commited
        }
    }
}
