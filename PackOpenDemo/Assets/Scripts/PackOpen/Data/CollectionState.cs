using System.Collections.Generic;

namespace PackOpen
{
    /// <summary>In-memory "owned cards" set. No persistence on purpose.</summary>
    public class CollectionState
    {
        readonly HashSet<string> _owned = new HashSet<string>();

        public bool Has(CardData c) => _owned.Contains(c.id);
        public void Add(CardData c) => _owned.Add(c.id);
        public void Clear() => _owned.Clear();
        public HashSet<string> Snapshot() => new HashSet<string>(_owned);

        public void Restore(HashSet<string> snapshot)
        {
            _owned.Clear();
            foreach (var s in snapshot) _owned.Add(s);
        }
    }
}
