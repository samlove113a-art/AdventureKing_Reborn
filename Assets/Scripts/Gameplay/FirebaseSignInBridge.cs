using System;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>The kinds of Firebase sign-in the login screen offers.</summary>
    public enum FirebaseProviderKind { Google, Facebook, Guest }

    /// <summary>
    /// Device side of Firebase Auth. The Firebase Unity SDK (FirebaseAuth.unitypackage, an external package) signs the player in and hands back a Firebase ID token;
    /// the game server verifies that token itself (POST /auth/firebase) and issues the normal game session. Nothing about the player's data lives in Firebase.
    /// Until the SDK is imported and FIREBASE_AUTH is defined (Project Settings -> Scripting Define Symbols), <see cref="Provider"/> is null and the login screen says so honestly.
    /// </summary>
    public interface IFirebaseSignIn
    {
        /// <summary>Signs in (or links, for a guest upgrade) and returns a fresh ID token, or an error text.</summary>
        void GetIdToken(FirebaseProviderKind kind, Action<bool, string, string> done);
        void SignOut();
    }

    public static class FirebaseSignInBridge
    {
        public static IFirebaseSignIn Provider { get; set; }

#if FIREBASE_AUTH
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() { Provider = new FirebaseSdkSignIn(); }

        private sealed class FirebaseSdkSignIn : IFirebaseSignIn
        {
            private Firebase.Auth.FirebaseAuth Auth => Firebase.Auth.FirebaseAuth.DefaultInstance;

            public void GetIdToken(FirebaseProviderKind kind, Action<bool, string, string> done)
            {
                Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(dep =>
                {
                    if (dep.Result != Firebase.DependencyStatus.Available) { done(false, null, "Firebase chưa sẵn sàng: " + dep.Result); return; }
                    Firebase.Auth.FirebaseUser user = Auth.CurrentUser;
                    switch (kind)
                    {
                        case FirebaseProviderKind.Guest:
                            if (user != null) { Token(user, done); return; }
                            Auth.SignInAnonymouslyAsync().ContinueWith(t => Result(t.IsCompletedSuccessfully ? t.Result.User : null, t.Exception, done));
                            return;
                        default:
                            // Google / Facebook: the platform sign-in plugin (Google Sign-In / Facebook SDK) returns a provider token; Firebase turns it into a credential.
                            // A guest that signs in with a provider is LINKED, so the uid (and all progress) stays the same.
                            string providerToken = ProviderTokens.Get(kind);
                            if (string.IsNullOrEmpty(providerToken)) { done(false, null, "Chưa gắn plugin đăng nhập " + kind + " (xem hướng dẫn)."); return; }
                            Firebase.Auth.Credential cred = kind == FirebaseProviderKind.Google
                                ? Firebase.Auth.GoogleAuthProvider.GetCredential(providerToken, null)
                                : Firebase.Auth.FacebookAuthProvider.GetCredential(providerToken);
                            if (user != null && user.IsAnonymous)
                                user.LinkWithCredentialAsync(cred).ContinueWith(t => Result(t.IsCompletedSuccessfully ? t.Result.User : null, t.Exception, done));
                            else
                                Auth.SignInWithCredentialAsync(cred).ContinueWith(t => Result(t.IsCompletedSuccessfully ? t.Result : null, t.Exception, done));
                            return;
                    }
                });
            }

            private static void Result(Firebase.Auth.FirebaseUser u, Exception e, Action<bool, string, string> done)
            { if (u == null) done(false, null, e != null ? e.GetBaseException().Message : "Đăng nhập thất bại."); else Token(u, done); }

            private static void Token(Firebase.Auth.FirebaseUser u, Action<bool, string, string> done) =>
                u.TokenAsync(true).ContinueWith(t => { if (t.IsCompletedSuccessfully) done(true, t.Result, null); else done(false, null, "Không lấy được token Firebase."); });

            public void SignOut() { if (Auth != null) Auth.SignOut(); }
        }

        /// <summary>Hook for the Google Sign-In / Facebook SDK plugin: set Google / Facebook to their provider token before calling GetIdToken.</summary>
        public static class ProviderTokens
        {
            public static string Google, Facebook;
            public static string Get(FirebaseProviderKind k) => k == FirebaseProviderKind.Google ? Google : Facebook;
        }
#endif
    }
}
