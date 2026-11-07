namespace AdventureKing.Gameplay
{
    /// <summary>
    /// In-memory read-only client view of the character authorized by the server.
    /// Not a persistent save. Server owns mutations, balances and progression.
    /// Logout/disconnect should Clear(); reconnect requires fresh server profile.
    /// </summary>
    public static class AdventurePlayerSession
    {
        public static AdventureCharacterSnapshot Character { get; private set; }
        public static bool IsActive => Character != null;

        internal static void Activate(AdventureCharacterSnapshot serverCharacter)
        {
            Character = serverCharacter;
        }

        public static void Clear()
        {
            Character = null;
        }
    }
}
