using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace RhythmGame.Tests
{
    public sealed class AccountTests
    {
        private string directory, path;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "RhythmAccountTests-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "player.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static AccountResult SignIn(RhythmGameData data, AccountProvider provider)
        {
            AccountResult result = default(AccountResult);
            bool called = false;
            data.SignIn(provider, r => { result = r; called = true; });
            Assert.IsTrue(called, "The local backend answers at once.");
            return result;
        }

        [Test]
        public void NewPlayerIsSignedOut()
        {
            RhythmGameData data = RhythmGameData.Open(path);
            Assert.IsFalse(data.Account.IsSignedIn);
            Assert.AreEqual(AccountProvider.None, data.Account.provider);
        }

        [Test]
        public void GuestSignInCreatesAnIdAndIsSaved()
        {
            RhythmGameData data = RhythmGameData.Open(path);
            AccountResult result = SignIn(data, AccountProvider.Guest);
            Assert.IsTrue(result.Ok);
            Assert.IsTrue(data.Account.IsSignedIn);
            Assert.IsTrue(data.Account.IsGuest);
            Assert.AreEqual(32, data.Account.userId.Length);
            Assert.IsFalse(data.Account.IsLinkedToServer, "No server yet.");
            Assert.IsTrue(DateTime.TryParse(data.Account.createdUtc, out _));
            Assert.AreEqual(9, data.Account.ShortId.Length);

            AccountInfo saved = RhythmGameData.Open(path).Account;
            Assert.IsTrue(saved.IsSignedIn);
            Assert.AreEqual(data.Account.userId, saved.userId);
        }

        [Test]
        public void SigningInAgainKeepsTheSameAccountAndProgress()
        {
            RhythmGameData data = RhythmGameData.Open(path);
            SignIn(data, AccountProvider.Guest);
            string id = data.Account.userId;
            data.AddDrumsticks(250);
            long coins = data.Player.coins;

            RhythmGameData reopened = RhythmGameData.Open(path);
            Assert.IsTrue(SignIn(reopened, AccountProvider.Guest).Ok);
            Assert.AreEqual(id, reopened.Account.userId);
            Assert.AreEqual(coins, reopened.Player.coins);

            reopened.SignOut();
            Assert.IsFalse(RhythmGameData.Open(path).Account.IsSignedIn, "Sign-out is saved.");
            SignIn(reopened, AccountProvider.Guest);
            Assert.AreEqual(id, reopened.Account.userId, "Signing back in on this device returns to the same save.");
            Assert.AreEqual(coins, reopened.Player.coins);
        }

        [Test]
        public void GoogleIsNotAvailableUntilTheServerExists()
        {
            RhythmGameData data = RhythmGameData.Open(path);
            AccountResult result = SignIn(data, AccountProvider.Google);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("login.google_not_ready", result.ErrorKey);
            Assert.IsFalse(data.Account.IsSignedIn);
            Assert.IsNotEmpty(Strings.Get(result.ErrorKey, GameLanguage.Korean));
        }

        // Stand-in for the future server: proves screens and save need no change to add one.
        private sealed class FakeServer : IAccountBackend
        {
            public PlayerData Uploaded;
            public bool Supports(AccountProvider provider) { return provider == AccountProvider.Google; }
            public void SignIn(AccountProvider provider, AccountInfo current, Action<AccountResult> done)
            {
                done(AccountResult.Success(new AccountInfo { provider = provider, userId = current.userId.Length > 0 ? current.userId : "local",
                    serverUserId = "server-42", displayName = "Kim" }));
            }
            public void Upload(PlayerData player, Action<bool> done) { Uploaded = player; done(true); }
        }

        [Test]
        public void AServerBackendCanBeSwappedInWithoutChangingTheSave()
        {
            RhythmGameData data = RhythmGameData.Open(path);
            SignIn(data, AccountProvider.Guest);
            string localId = data.Account.userId;
            var server = new FakeServer();
            data.Accounts = server;
            Assert.IsTrue(SignIn(data, AccountProvider.Google).Ok);
            Assert.AreSame(data.Player, server.Uploaded, "The whole save is uploaded on sign-in.");
            AccountInfo saved = RhythmGameData.Open(path).Account;
            Assert.AreEqual(AccountProvider.Google, saved.provider);
            Assert.AreEqual(localId, saved.userId, "The guest save is carried over to the linked account.");
            Assert.AreEqual("server-42", saved.serverUserId);
            Assert.IsTrue(saved.IsLinkedToServer);
            Assert.IsNotEmpty(saved.lastSyncedUtc);
        }

        [Test]
        public void SavesFromBeforeTheLoginScreenLoadSignedOut()
        {
            RhythmGameData data = RhythmGameData.Open(path);
            data.AddDrumsticks(10);
            string json = File.ReadAllText(path);
            int start = json.IndexOf("\"account\"", StringComparison.Ordinal);
            Assert.Greater(start, 0);
            // Cut the account block out, as an older build would have written it.
            int end = json.IndexOf('}', start);
            string old = json.Remove(start, end - start + 1).Replace(",\n    \n", "\n").Replace(",\n}", "\n}");
            old = System.Text.RegularExpressions.Regex.Replace(old, ",\\s*}", "\n}");
            File.WriteAllText(path, old);
            File.Delete(path + ".bak");
            RhythmGameData reopened = RhythmGameData.Open(path);
            Assert.IsFalse(reopened.Account.IsSignedIn);
            Assert.AreEqual(data.Player.coins, reopened.Player.coins);
            Assert.IsTrue(SignIn(reopened, AccountProvider.Guest).Ok);
        }

        [Test]
        public void InvalidProviderIsRejected()
        {
            var info = JsonUtility.FromJson<AccountInfo>("{\"provider\":9,\"userId\":\"x\"}");
            Assert.Throws<InvalidOperationException>(info.Validate);
            Assert.Throws<InvalidOperationException>(new AccountInfo { provider = AccountProvider.Guest }.Validate);
        }
    }
}
