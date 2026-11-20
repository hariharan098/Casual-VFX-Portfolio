namespace PackOpen
{
    public struct CardResult
    {
        public CardData Card;
        /// <summary>First copy of this card: show NEW badge.</summary>
        public bool IsNew;
        /// <summary>Duplicate converts to stars equal to rarity. 0 when IsNew.</summary>
        public int StarsFromDuplicate;
    }
}
