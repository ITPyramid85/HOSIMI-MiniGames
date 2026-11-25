using System;

namespace RhythmGame
{
    public struct AccountResult
    {
        public bool Ok;
        public AccountInfo Account;
        public string ErrorKey;   // string-table key shown to the player when Ok is false

        public static AccountResult Success(AccountInfo account) { return new AccountResult { Ok = true, Account = account }; }
        public static AccountResult Fail(string errorKey) { return new AccountResult { Ok = false, ErrorKey = errorKey }; }
    }

    // Where accounts live. The login screen and RhythmGameData only talk to this interface, so the
    // account server is added by writing one more backend (Google sign-in, upload, download) and
    // assigning it to RhythmGameData.Accounts; screens and save format stay the same.
    public interface IAccountBackend
    {
        bool Supports(AccountProvider provider);
        // current is the account already in the save (may be signed out). Callbacks may arrive later
        // (network); the local backend answers at once.
        void SignIn(AccountProvider provider, AccountInfo current, Action<AccountResult> done);
        // Upload the whole save for the signed-in account. done(false) = not uploaded (offline / no server).
        void Upload(PlayerData player, Action<bool> done);
    }

    // No server yet: guest accounts are created on the device and everything stays in the local save.
    public sealed class LocalAccountBackend : IAccountBackend
    {
        private readonly Func<DateTime> clock;

        public LocalAccountBackend(Func<DateTime> clock = null) { this.clock = clock ?? (() => DateTime.UtcNow); }

        public bool Supports(AccountProvider provider) { return provider == AccountProvider.Guest; }

        public void SignIn(AccountProvider provider, AccountInfo current, Action<AccountResult> done)
        {
            if (!Supports(provider))
            {
                done(AccountResult.Fail(provider == AccountProvider.Google ? "login.google_not_ready" :
                    provider == AccountProvider.Apple ? "login.apple_not_ready" : "login.failed"));
                return;
            }
            string now = clock().ToString("o");
            // Returning guest (also after signing out): keep the id so the save stays the same account.
            bool returning = current != null && !string.IsNullOrEmpty(current.userId) &&
                (current.provider == provider || current.provider == AccountProvider.None);
            AccountInfo account = returning ? Copy(current)
                : new AccountInfo { userId = Guid.NewGuid().ToString("N"), createdUtc = now };
            account.provider = provider;
            account.lastSignInUtc = now;
            done(AccountResult.Success(account));
        }

        public void Upload(PlayerData player, Action<bool> done) { done(false); }

        private static AccountInfo Copy(AccountInfo source)
        {
            return new AccountInfo
            {
                provider = source.provider, userId = source.userId, displayName = source.displayName, createdUtc = source.createdUtc,
                lastSignInUtc = source.lastSignInUtc, serverUserId = source.serverUserId, lastSyncedUtc = source.lastSyncedUtc
            };
        }
    }
}
