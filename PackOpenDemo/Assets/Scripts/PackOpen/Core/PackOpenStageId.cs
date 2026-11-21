namespace PackOpen
{
    /// <summary>Ordered hook points of the sequence. Order here is play order.</summary>
    public enum PackOpenStageId
    {
        PackAppear,     // pack enters the screen
        PackTear,       // foil strip tears off
        PackMoveDown,   // pack drops down and out
        CardsMoveOut,   // card backs come out one by one into their slots
        CardsFlip,      // cards flip face up
        Hold,           // short delay
        FlyToWallet     // cards fly to the wallet
    }
}
