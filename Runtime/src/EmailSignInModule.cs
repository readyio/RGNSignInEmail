using System.Threading.Tasks;
using UnityEngine;

namespace RGN.Modules.SignIn
{
    public class EmailSignInModule : BaseModule<EmailSignInModule>, IRGNModule
    {
        private RGNDeepLink _rgnDeepLink;

        private bool _lastTokenReceived;

        public static void InitializeWindowsDeepLink()
        {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
            if (WindowsDeepLinks.IsCustomUrlRegistered()) { return; }
            WindowsDeepLinks.StartHandling();
#endif
        }

        public override void Init()
        {
            _rgnDeepLink = new RGNDeepLink();
            _rgnDeepLink.Init(_rgnCore);
            _rgnDeepLink.TokenReceived += OnTokenReceivedAsync;
        }
        protected override void Dispose(bool disposing)
        {
            if (_rgnDeepLink != null)
            {
                _rgnDeepLink.TokenReceived -= OnTokenReceivedAsync;
                _rgnDeepLink.Dispose();
                _rgnDeepLink = null;
            }
            base.Dispose(disposing);
        }

        public async void TryToSignIn()
        {
            if (_rgnCore.AuthorizedProviders.HasFlag(EnumAuthProvider.Email))
            {
                _rgnCore.Dependencies.Logger.Log("[EmailSignInModule]: Already logged in with email");
                _rgnCore.SetAuthState(EnumLoginState.Success, EnumLoginResult.Ok);
                return;
            }

            _rgnCore.SetAuthState(EnumLoginState.Processing, EnumLoginResult.None);
            _lastTokenReceived = false;
            string idToken = string.Empty;
            if (_rgnCore.MasterAppUser != null)
            {
                idToken = await _rgnCore.MasterAppUser?.TokenAsync(false);
            }
            _rgnDeepLink.OpenURL(idToken);

            EmailSignInFocusWatcher focusWatcher = EmailSignInFocusWatcher.Create();
            focusWatcher.OnFocusChanged += OnApplicationFocusChanged;
        }

        private void OnApplicationFocusChanged(EmailSignInFocusWatcher watcher, bool hasFocus)
        {
            if (hasFocus && !_lastTokenReceived)
            {
                watcher.OnFocusChanged -= OnApplicationFocusChanged;
                watcher.Destroy();

                _rgnCore.SetAuthState(EnumLoginState.Error, EnumLoginResult.Cancelled);
            }
        }

        private async void OnTokenReceivedAsync(bool cancelled, string token)
        {
            _lastTokenReceived = true;

            if (cancelled)
            {
                _rgnCore.SetAuthState(EnumLoginState.Error, EnumLoginResult.Cancelled);
                _rgnCore.Dependencies.Logger.Log("[EmailSignInModule]: Login cancelled");
                return;
            }

            _rgnCore.Dependencies.Logger.Log("[EmailSignInModule]: Token received: " + token);

            if (string.IsNullOrEmpty(token))
            {
                _rgnCore.SetAuthState(EnumLoginState.Error, EnumLoginResult.Unknown);
            }
            else
            {
                await _rgnCore.ReadyMasterAuth.SignInWithCustomTokenAsync(token);
            }
        }

        internal void TryToSignIn(string email, string password)
        {
            TryToSingInWithoutLink(email, password);
        }

        public void SendPasswordResetEmail(string email)
        {
            _rgnCore.ReadyMasterAuth.SendPasswordResetEmailAsync(email).ContinueWith(task => {
                if (task.IsCanceled)
                {
                    _rgnCore.Dependencies.Logger.LogError("[EmailSignInModule]: SendPasswordResetEmailAsync was canceled.");
                    return;
                }

                if (task.IsFaulted)
                {
                    Utility.ExceptionHelper.PrintToLog(_rgnCore.Dependencies.Logger, task.Exception);
                    _rgnCore.Dependencies.Logger.LogError("[EmailSignInModule]: SendPasswordResetEmailAsync encountered an error: " +
                                   task.Exception);
                    return;
                }

                SignOut();
                _rgnCore.Dependencies.Logger.Log("[EmailSignInModule]: Password reset email sent successfully.");
            },
            TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void SignOut()
        {
            _rgnCore.SignOutRGN();
        }

        private void TryToSingInWithoutLink(string email, string password)
        {
            _rgnCore.ReadyMasterAuth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
                if (task.IsCanceled)
                {
                    _rgnCore.Dependencies.Logger.LogWarning("EmailSignInModule]: SignInWithEmailAndPasswordAsync was cancelled");
                    SignOut();
                    return;
                }

                if (task.IsFaulted)
                {
                    Utility.ExceptionHelper.PrintToLog(_rgnCore.Dependencies.Logger, task.Exception);
                    SignOut();
                    _rgnCore.SetAuthState(EnumLoginState.Error, EnumLoginResult.Unknown);
                    return;
                }

                string email = "not logged in";
                if (_rgnCore.MasterAppUser != null)
                {
                    email = _rgnCore.MasterAppUser.Email;
                }
                _rgnCore.Dependencies.Logger.Log("[EmailSignInModule]: Email/Password, the user successfully signed in: " + email);
            },
            TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
