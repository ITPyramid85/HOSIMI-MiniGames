using System;

namespace RhythmGame
{
    // Persisted values: do not reorder or reuse these numbers.
    public enum AccountProvider { None = 0, Guest = 1, Google = 2, Apple = 3 }

    // Who the save belongs to (client reply v5 §5-3: drumsticks, records, characters and goods requests
    // are kept per account). Until the account server exists every sign-in is a local guest; the
    // server fields stay empty and the whole PlayerData is what gets uploaded once it does.
    [Serializable]
    public sealed class AccountInfo
    {
        public AccountProvider provider = AccountProvider.None;
        public string userId = "";          // local id, kept when the account is later linked to the server
        public string displayName = "";
        public string createdUtc = "";      // ISO 8601 (round-trip "o")
        public string lastSignInUtc = "";
        // Filled in by the server backend; empty means this save has never been uploaded.
        public string serverUserId = "";
        public string lastSyncedUtc = "";

        public bool IsSignedIn { get { return provider != AccountProvider.None && !string.IsNullOrEmpty(userId); } }
        public bool IsGuest { get { return provider == AccountProvider.Guest; } }
        public bool IsLinkedToServer { get { return !string.IsNullOrEmpty(serverUserId); } }

        // Short form for the screen, e.g. "3F9A-C21B".
        public string ShortId
        {
            get
            {
                string id = (userId ?? "").Replace("-", "").ToUpperInvariant();
                return id.Length < 8 ? id : id.Substring(0, 4) + "-" + id.Substring(4, 4);
            }
        }

        public void Normalize()
        {
            if (userId == null) userId = "";
            if (displayName == null) displayName = "";
            if (createdUtc == null) createdUtc = "";
            if (lastSignInUtc == null) lastSignInUtc = "";
            if (serverUserId == null) serverUserId = "";
            if (lastSyncedUtc == null) lastSyncedUtc = "";
        }

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(AccountProvider), provider)) throw new InvalidOperationException("Invalid account provider.");
            Normalize();
            if (provider != AccountProvider.None && userId.Length == 0) throw new InvalidOperationException("Signed-in account has no id.");
        }
    }
}
