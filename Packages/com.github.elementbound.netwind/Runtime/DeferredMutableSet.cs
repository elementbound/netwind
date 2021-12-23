using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.github.elementbound.NetWind
{
    [Serializable]
    class DeferredMutableSet<T>: ICollection<T>
    {
        [SerializeField] private bool doClear = false;
#pragma warning disable CA2235 // Mark all non-serializable fields
        [SerializeField] private readonly List<T> itemsToAdd = new List<T>();
        [SerializeField] private readonly List<T> itemsToRemove = new List<T>();
        [SerializeField] private readonly List<T> items = new List<T>();
#pragma warning restore CA2235 // Mark all non-serializable fields

        public int Count => throw new NotImplementedException();

        public bool IsReadOnly => throw new NotImplementedException();

        public void Add(T item)
        {
            if (!itemsToAdd.Contains(item))
                itemsToAdd.Add(item);
        }

        public void Clear()
        {
            doClear = true;
        }

        public bool Contains(T item)
        {
            return items.Contains(item) && !itemsToRemove.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        public bool Remove(T item)
        {
            if (!itemsToRemove.Contains(item))
            {
                itemsToRemove.Add(item);
                return true;
            }

            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        public void AcknowledgeMutations()
        {
            if (doClear)
            {
                doClear = false;
                items.Clear();
                itemsToAdd.Clear();
                itemsToRemove.Clear();

                return;
            }

            foreach (var item in itemsToAdd)
                if (!items.Contains(item))
                    items.Add(item);

            foreach (var item in itemsToRemove)
                items.Remove(item);
        }

        private class Enumerator : IEnumerator, IEnumerator<T>
        {
            private DeferredMutableSet<T> set;
            private int idx;

            public Enumerator(DeferredMutableSet<T> set)
            {
                this.set = set;
                this.idx = -1;
            }

            object IEnumerator.Current => set.items[idx];

            T IEnumerator<T>.Current => set.items[idx];

            public void Dispose()
            {
                Reset();
            }

            public bool MoveNext()
            {
                while (true)
                {
                    ++idx;

                    if (idx >= set.items.Count)
                        return false;

                    if (!set.Contains(set.items[idx]))
                        continue;

                    return true;
                }
            }

            public void Reset()
            {
                idx = -1;
            }
        }
    }
}