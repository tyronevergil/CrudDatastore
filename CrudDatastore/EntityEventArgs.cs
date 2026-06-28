using System;

namespace CrudDatastore
{
    public class EntityEventArgs : EventArgs
    {
        private readonly object _entity;

        public EntityEventArgs(object entity)
        {
            _entity = entity;
        }

        public object Entity
        {
            get
            {
                return _entity;

            }
        }
    }
}
