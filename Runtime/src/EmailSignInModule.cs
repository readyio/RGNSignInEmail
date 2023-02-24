using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;

namespace RGN.Modules.SignIn
{
    public class EmailSignInModule : BaseModule<EmailSignInModule>, IRGNModule
    {
        private IRGNRolesCore _rgnCore;
        private RGNDeepLink _rgnDeepLink;

        public static void InitializeWindowsDeepLink()
        {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
            if (WindowsDeepLinks.IsCustomUrlRegistered()) { return; }
            WindowsDeepLinks.StartHandling();
#endif
        }

        public void SetRGNCore(IRGNRolesCore rgnCore)
        {
            _rgnCore = rgnCore;
        }
        public void Init()
        {
            _rgnDeepLink = new RGNDeepLink();
            _rgnDeepLink.Init(_rgnCore);
            _rgnDeepLink.TokenReceived += OnTokenReceivedAsync;
        }
        public void Dispose()
        {
            if (_rgnDeepLink != null)
            {
                _rgnDeepLink.TokenReceived -= OnTokenReceivedAsync;
                _rgnDeepLink.Dispose();
                _rgnDeepLink = null;
            }
        }

        public void TryToSignIn()
        {
            if (_rgnCore.AuthorizedProviders.HasFlag(EnumAuthProvider.Email))
            {
                _rgnCore.Dependencies.Logger.Log("[EmailSignInModule]: Already logged in with email");
                _rgnCore.SetAuthCompletion(EnumLoginState.Success, EnumLoginError.Ok);
                return;
            }
            _rgnDeepLink.OpenURL();
        }
        private async void OnTokenReceivedAsync(string token)
        {
            _rgnCore.Dependencies.Logger.Log("[EmailSignInModule]: Token received: " + token);
            if (string.IsNullOrEmpty(token))
            {
                _rgnCore.SetAuthCompletion(EnumLoginState.Error, EnumLoginError.Unknown);
            }
            else
            {
                await _rgnCore.ReadyMasterAuth.SignInWithCustomTokenAsync(token);
                _rgnCore.SetAuthCompletion(EnumLoginState.Success, EnumLoginError.Ok);
            }
        }

        public void TryToSignIn(string email, string password, bool tryToLinkToCurrentAccount = false)
        {
            if (tryToLinkToCurrentAccount)
            {
                TryToLink(email, password);
            }
            else
            {
                TryToSingInWithoutLink(email, password);
            }
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

        private void TryToLink(string email, string password)
        {
            _rgnCore.Dependencies.Logger.Log("EmailSignInModule]: TryToSignIn(" + email + ", " + string.IsNullOrEmpty(password) + ")");
            var credential = _rgnCore.ReadyMasterAuth.emailAuthProvider.GetCredential(email, password);

            _rgnCore.ReadyMasterAuth.CurrentUser.LinkAndRetrieveDataWithCredentialAsync(credential).ContinueWith(task => {
                if (task.IsCanceled)
                {
                    _rgnCore.Dependencies.Logger.LogWarning("EmailSignInModule]: LinkAndRetrieveDataWithCredentialAsync was cancelled");
                    return;
                }

                if (task.IsFaulted)
                {
                    Utility.ExceptionHelper.PrintToLog(_rgnCore.Dependencies.Logger, task.Exception);
                    FirebaseAccountLinkException firebaseAccountLinkException = task.Exception.InnerException as FirebaseAccountLinkException;
                    if (firebaseAccountLinkException != null && firebaseAccountLinkException.ErrorCode == (int)AuthError.CredentialAlreadyInUse)
                    {
                        _rgnCore.SetAuthCompletion(EnumLoginState.Error, EnumLoginError.AccountAlreadyLinked);
                        return;
                    }

                    FirebaseException firebaseException = task.Exception.InnerException as FirebaseException;

                    if (firebaseException != null)
                    {
                        EnumLoginError loginError = (AuthError)firebaseException.ErrorCode switch {
                            AuthError.EmailAlreadyInUse => EnumLoginError.AccountAlreadyLinked,
                            AuthError.ProviderAlreadyLinked => EnumLoginError.AccountAlreadyLinked,
                            AuthError.RequiresRecentLogin => EnumLoginError.AccountNeedsRecentLogin,
                            _ => EnumLoginError.Unknown
                        };

                        _rgnCore.SetAuthCompletion(EnumLoginState.Error, loginError);
                        return;
                    }

                    _rgnCore.SetAuthCompletion(EnumLoginState.Error, EnumLoginError.Unknown);
                    return;
                }

                _rgnCore.Dependencies.Logger.Log("[EmailSignInModule]: LinkWith Email/Password Successful. " + _rgnCore.ReadyMasterAuth.CurrentUser.UserId + " ");

                _rgnCore.SetAuthCompletion(EnumLoginState.Success, EnumLoginError.Ok);
            },
            TaskScheduler.FromCurrentSynchronizationContext());
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
                    _rgnCore.SetAuthCompletion(EnumLoginState.Error, EnumLoginError.Unknown);
                    return;
                }

                _rgnCore.Dependencies.Logger.Log("[EmailSignInModule]: Email/Password, signed in");
            },
            TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
