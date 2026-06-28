using System;
using System.Collections;
using System.Collections.Generic;

namespace CrudDatastore.Framework.Internal
{
    internal class MaterializeObjectEnumerator<T> : IEnumerator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, T> _materializeObject;

        public MaterializeObjectEnumerator(IEnumerator<T> enumerator, Func<T, T> materializeObject)
        {
            _enumerator = enumerator;
            _materializeObject = materializeObject;
        }

        public T Current
        {
            get
            {
                return typeof(EntityBase).IsAssignableFrom(typeof(T)) ? _materializeObject(_enumerator.Current) : _enumerator.Current;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                return _enumerator.Current;
            }
        }

        public bool MoveNext()
        {
            return _enumerator.MoveNext();
        }

        public void Reset()
        {
            _enumerator.Reset();
        }

        public virtual void Dispose()
        {
            _enumerator.Dispose();
        }
    }
}
