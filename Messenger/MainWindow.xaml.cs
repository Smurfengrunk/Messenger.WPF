#pragma warning disable CS8618, CS8602, CS8604, CS8600, CS8603, CS8601, CS8619, CS8625, CS8629, CS0169, CS0414, CS8622

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;


namespace Messenger
{
    /// <summary>
    /// Class representing the main window of the Messenger WPF application.
    /// It handles window state restoration, WebView2 initialization, navigation handling, and inter-process communication for single instance enforcement.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly WindowStateStore _windowState = WindowStateStore.Load();
        private readonly Settings _settings = Settings.Load();
        private bool _reallyClose = false;
        private bool _hasRecreatedWebView = false;
        private IntPtr _hwnd;
        private WebView2 webView;
        private CoreWebView2Environment env;

        /// <summary>
        /// MainWindow constructor. Initializes the main window, restores its position, sets up event handlers, and initializes the WebView2 control.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Återställ fönsterposition
            RestoreWindowState();

            this.Closing += OnWindowClosing;
            this.SourceInitialized += OnSourceInitialized;

            InitializeWebView();
            TaskbarDiagnostics.Dump();
        }

        /// <summary>
        /// Gets the content of an embedded script resource by its name. The script is expected to be located in the "Messenger.Scripts" namespace.
        /// </summary>
        /// <param name="scriptName"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        private string GetEmbeddedScript(string scriptName)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            // Notera: namespace + mappnamn + filnamn
            var resourceName = $"Messenger.Scripts.{scriptName}";

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) throw new FileNotFoundException($"Hittade inte script: {resourceName}");

            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// OnSourceInitialized event handler. This method is called when the window's source is initialized.
        /// It sets the window handle, application ID, icon, initializes the tray icon, attaches a window message hook, and initializes the taskbar badge.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;

            Win32.SetWindowAppId(_hwnd, "Messenger");

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            IntPtr hIcon = Win32.LoadImage(IntPtr.Zero, iconPath, Win32.IMAGE_ICON, 0, 0, Win32.LR_LOADFROMFILE);
            Win32.SendMessage(_hwnd, Win32.WM_SETICON, (IntPtr)Win32.ICON_BIG, hIcon);
            Win32.SendMessage(_hwnd, Win32.WM_SETICON, (IntPtr)Win32.ICON_SMALL, hIcon);

            TrayIcon.Initialize(this, hIcon, "Messenger Wrapper");
            HwndHook.Attach(_hwnd, OnWindowMessage);
            TaskbarBadge.InitializeFor(this);
        }
        /// <summary>
        /// Restores the window state (position and size) from the saved settings.
        /// If the saved coordinates are invalid (e.g., minimized coordinates), it sets default values for left, top, width, and height.
        /// </summary>
        private void RestoreWindowState()
        {
            // Skydda mot sparade minimerat-koordinater (-32000)
            var left = _windowState.Left < -500 ? 100 : _windowState.Left;
            var top = _windowState.Top < -500 ? 100 : _windowState.Top;
            var width = _windowState.Width < 200 ? 1200 : _windowState.Width;
            var height = _windowState.Height < 200 ? 800 : _windowState.Height;

            this.Left = left;
            this.Top = top;
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// OnWindowMessage is a callback method that handles window messages sent to the main window.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private IntPtr OnWindowMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            // Fånga upp signalen från den nya instansen
            if (msg == SingleInstanceHelper.WM_SHOW_MESSENGER)
            {
                Dispatcher.Invoke(() => ShowWindow());
                return IntPtr.Zero;
            }

            TrayIcon.HandleMessage(hwnd, msg, wParam, lParam);

            // Block commented out to prevent hiding the window when minimized. If you want the window to hide when minimized, uncomment the following lines.
            //const uint WM_SIZE = 0x0005;
            //const int SIZE_MINIMIZED = 1;

            //if (msg == WM_SIZE && wParam.ToInt32() == SIZE_MINIMIZED && _settings.MinimizeToTray)
            //{
            //    this.Hide();
            //}

            try
            {
                return HwndHook.CallOld(hwnd, msg, wParam, lParam);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Configures the given WebView2 instance by ensuring its CoreWebView2 is initialized, attaching event handlers, setting the UserAgent, and injecting custom scripts.
        /// </summary>
        /// <param name="targetWebView"></param>
        /// <returns></returns>
        private async Task ConfigureWebViewAsync(Microsoft.Web.WebView2.WinForms.WebView2 targetWebView, CoreWebView2Environment env)
        {
            if (targetWebView.CoreWebView2 != null)
            {
                Log("CoreWebView2 is already initialized, skipping configuration.");
                return; // Redan initierad, vi behöver inte göra något mer
            }
            Log("await targetWebView.EnsureCoreWebView2Async()");
            try
            {
                await targetWebView.EnsureCoreWebView2Async(env);

                // Koppla på alla events igen
                Log("Connecting event handlers to targetWebVew");
                targetWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                targetWebView.CoreWebView2.NavigationStarting += webView_NavigationStarting;
                targetWebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
                targetWebView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;

                // Sätt UserAgent så inte Facebook klagar
                Log("Setting user agent");
                targetWebView.CoreWebView2.Settings.UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/124.0.0.0 Safari/537.36";

                // Injicera dina egna skript
                Log("Adding UnreadCounter script");
                await targetWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetEmbeddedScript("UnReadCounter.js"));
                Log("Adding MessengerMode script");
                await targetWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetEmbeddedScript("MessengerMode.js"));
            }
            catch (Exception ex)
            {
                Log("Krasch i ConfigureWebViewAsync: " + ex.Message + "\n" + ex.StackTrace);
                throw; // Återkasta för att stoppa körningen
            }
        }

        /// <summary>
        /// Writes a log message to a file on the desktop named "messenger_debug.log". Each log entry is prefixed with the current time in "HH:mm:ss" format.
        /// </summary>
        /// <param name="message"></param>
        private void Log(string message)
        {
            // Loggar till en fil på skrivbordet så vi kan läsa den oavsett behörighet
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "messenger_debug.log");
            File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss") + ": " + message + Environment.NewLine);
        }

        /// <summary>
        /// Initializes the WebView2 control by creating a new instance, setting it as the child of the host, configuring it, and navigating to the Facebook Messenger URL.
        /// </summary>
        private async void InitializeWebView()
        {
            Log("Startar InitializeWebView...");

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userDataFolder = Path.Combine(localAppData, "Messenger", "WebView2Data");

            Log("UserDataFolder: " + userDataFolder);

            if (!Directory.Exists(userDataFolder))
            {
                Log("Skapar mapp...");
                Directory.CreateDirectory(userDataFolder);
            }

            try
            {
                Log("Försöker skapa miljö...");
                env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, null);
                Log("Miljö skapad.");

                var Web = new Microsoft.Web.WebView2.WinForms.WebView2();
                Host.Child = Web;
                webView = Web;

                Log("Kallar ConfigureWebViewAsync...");
                await ConfigureWebViewAsync(Web, env);
                Log("ConfigureWebViewAsync klar.");

                Web.CoreWebView2.Navigate("https://www.facebook.com/messages");
                Log("Navigering påbörjad.");
            }
            catch (Exception ex)
            {
                Log("FEL: " + ex.Message);
                Log("Stacktrace: " + ex.StackTrace);
            }
        }

        /// <summary>
        /// NavigationCompleted event handler for the WebView2 control.
        /// This method checks if the navigation URL is a Messenger thread and, if so, recreates the WebView2 instance to ensure proper functionality.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            string url = webView.CoreWebView2.Source.ToString();

            if ((url.Contains("/messages/t/") || url.Contains("/messages/e2ee/t/")) && !_hasRecreatedWebView)
            {
                _hasRecreatedWebView = true;

                Uri targetUri = webView.Source;
                var wfHost = Host;

                if (wfHost != null)
                {
                    wfHost.Child = null;
                    webView.Dispose();

                    var newWebView = new Microsoft.Web.WebView2.WinForms.WebView2();
                    wfHost.Child = newWebView;
                    webView = newWebView;

                    // HÄR: Se till att den nya instansen får exakt samma konfiguration, skript och events!
                    await ConfigureWebViewAsync(webView, env);

                    // Nu navigerar vi in i kaklet
                    webView.Source = targetUri;
                    webView.Focus();
                }
            }
        }

        /// <summary>
        /// Handles the NavigationStarting event of the WebView2 control.
        /// This method checks if the navigation URL can be navigated inside the wrapper or if it should be opened externally.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void webView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
                return;
            if (IsWebViewUtilityNavigation(uri))
                return;
            if (CanNavigateInsideWrapper(uri))
                return;
            e.Cancel = true;
            OpenExternal(uri);
        }

        /// <summary>
        /// Handles the NewWindowRequested event of the WebView2 control.
        /// This method checks if the requested URL can be navigated inside the wrapper or if it should be opened externally.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
                return;

            if (IsWebViewUtilityNavigation(uri))
                return;

            if (CanNavigateInsideWrapper(uri))
            {
                e.Handled = true;

                Debug.WriteLine($"[NAVIGATE TO]: {uri.AbsoluteUri}");
                if (sender is CoreWebView2 webView)
                    webView.Navigate(uri.AbsoluteUri);

                return;
            }

            e.Handled = true;
            OpenExternal(uri);
        }

        /// <summary>
        /// Checks if the given URI can be navigated inside the wrapper application.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static bool CanNavigateInsideWrapper(Uri uri)
        {
            if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IsMessengerPage(uri) || IsFacebookAuthPage(uri) || IsMetaAuthPage(uri);
        }

        /// <summary>
        /// Checks if the given URI is a WebView utility navigation (e.g., "about:", "edge:", or "chrome:").
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static bool IsWebViewUtilityNavigation(Uri uri)
        {
            return uri.Scheme.Equals("about", StringComparison.OrdinalIgnoreCase) ||
                   uri.Scheme.Equals("edge", StringComparison.OrdinalIgnoreCase) ||
                   uri.Scheme.Equals("chrome", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the given URI is a Messenger page (i.e., starts with "/messages" on the Facebook host).
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static bool IsMessengerPage(Uri uri)
        {
            return IsFacebookHost(uri) &&
                   uri.AbsolutePath.StartsWith("/messages", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the given URI is a Facebook authentication page (e.g., login, checkpoint, two-factor authentication, etc.).
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static bool IsFacebookAuthPage(Uri uri)
        {
            if (!IsFacebookHost(uri))
                return false;

            var path = uri.AbsolutePath.TrimEnd('/');

            return path.Equals("/login", StringComparison.OrdinalIgnoreCase) ||
                               path.Equals("/login.php", StringComparison.OrdinalIgnoreCase) ||
                               path.Equals("/r.php", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/login/", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/checkpoint", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/two_factor", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/two_step_verification", StringComparison.OrdinalIgnoreCase) || // <-- LÄGG TILL DENNA
                               path.StartsWith("/security/2fac", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/security/twofactor", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/auth_platform", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/approvals", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/recover", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/confirm", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/privacy/consent", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/cookie/consent", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/device-based", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the given URI is a Meta authentication page (e.g., login, auth, checkpoint, two-factor authentication, etc.) on the Meta accounts or auth hosts.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static bool IsMetaAuthPage(Uri uri)
        {
            if (!IsMetaAuthHost(uri))
                return false;

            var path = uri.AbsolutePath.TrimEnd('/');

            return path.Length == 0 ||
                   path.Equals("/login", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/login", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/auth", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/checkpoint", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/oauth", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/device", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/two_factor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the given URI is a Facebook host (i.e., "facebook.com" or any subdomain of "facebook.com").
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static bool IsFacebookHost(Uri uri)
        {
            return uri.Host.Equals("facebook.com", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.EndsWith(".facebook.com", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the given URI is a Meta authentication host (i.e., "accounts.meta.com", "auth.meta.com", or any subdomain of these).
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static bool IsMetaAuthHost(Uri uri)
        {
            return uri.Host.Equals("accounts.meta.com", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.EndsWith(".accounts.meta.com", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.Equals("auth.meta.com", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.EndsWith(".auth.meta.com", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Opens the given URI in the default external browser. If an exception occurs, it logs the error to the debug output.
        /// </summary>
        /// <param name="uri"></param>
        private static void OpenExternal(Uri uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open external link: {uri} - {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the WebMessageReceived event from the WebView2 control.
        /// This method processes messages sent from the web content, such as external URL requests and unread message counts, and updates the application state accordingly.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.TryGetWebMessageAsString();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("externalUrl", out var externalUrlProp) &&
                            Uri.TryCreate(externalUrlProp.GetString(), UriKind.Absolute, out var externalUri))
                {
                    // --- LÄGG TILL DETTA FÖR FELSÖKNING ---
                    Debug.WriteLine($"[JS VILLE ÖPPNA EXTERNT]: {externalUri.AbsoluteUri}");
                    // --------------------------------------

                    if (!CanNavigateInsideWrapper(externalUri))
                    {
                        OpenExternal(externalUri);
                        return;
                    }
                }
                if (root.TryGetProperty("unread", out var unreadProp))
                {
                    int unread = unreadProp.GetInt32();

                    // WebMessageReceived körs på bakgrundstråd — måste dispatcha till UI-tråden
                    Dispatcher.Invoke(() =>
                    {
                        this.Title = unread > 0 ? $"({unread}) Messenger" : "Messenger";
                        TrayIcon.UpdateUnread(this, unread, _settings.ShowBadges);
                    });
                }
            }
            catch { }
        }

        /// <summary>
        /// Handles the Closing event of the main window. If the user attempts to close the window, it checks if the application should really close or just hide the window.
        /// If it should hide, it cancels the closing event and hides the window instead. If it should really close, it saves the window state and removes the tray icon.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_reallyClose)
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                SaveState();
                TrayIcon.Remove();
            }
        }

        /// <summary>
        /// Handles the Closed event of the main window. This method is called when the window is closed, and it saves the window state and removes the tray icon.
        /// </summary>
        private void SaveState()
        {
            // Spara inte om fönstret är minimerat
            if (this.WindowState == WindowState.Minimized)
                return;

            _windowState.Left = this.Left;
            _windowState.Top = this.Top;
            _windowState.Width = this.Width;
            _windowState.Height = this.Height;

            _windowState.Save();
            _settings.Save();
        }

        /// <summary>
        /// Shows the main window and brings it to the foreground. If the window is minimized, it restores it to its normal state.
        /// </summary>
        public void ShowWindow()
        {
            this.Show();
            this.Activate();
            if (this.WindowState == WindowState.Minimized)
                this.WindowState = WindowState.Normal;
        }

        /// <summary>
        /// Closes the main window and sets a flag indicating that the application should really close, allowing the Closing event to proceed without cancellation.
        /// </summary>
        public void CloseForReal()
        {
            _reallyClose = true;
            this.Close();
        }
    }
}

#pragma warning restore CS8618, CS8602, CS8604, CS8600, CS8603, CS8601, CS8619, CS8625, CS8629