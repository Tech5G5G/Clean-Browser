using Windows.System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Windows.AppLifecycle;
using CommunityToolkit.WinUI.Animations;

namespace Clean_Browser
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        OverlappedPresenter presenter;
        InputNonClientPointerSource nonClientInputSrc;
        ObservableCollection<WebView2> webViews = [];

        public const string GoogleSearch = "google.com/search?q=";

        public MainWindow()
        {
            this.InitializeComponent();
            presenter = AppWindow.Presenter as OverlappedPresenter;
            nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

            Activated += (s, e) => UpdateButtonBounds();
            SizeChanged += Window_SizeChanged;
            (Content as Grid).Loaded += (s, e) => UpdateButtonBounds();
            (Content as Grid).SizeChanged += (s, e) => UpdateButtonBounds();

            nonClientInputSrc.ExitedMoveSize += (s, e) => UpdateButtonBounds();
            nonClientInputSrc.PointerEntered += Show_TitleBar;
            nonClientInputSrc.PointerExited += Hide_TitleBar;

            if (Environment.OSVersion.Version.Build >= 22000)
                maximizeToolTip.Visibility = Visibility.Collapsed;

            GenerateWebView();
        }
        private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            var icon = maximizeButton.Content as FontIcon;
            bool presenterIsRestored = presenter.State == OverlappedPresenterState.Restored;
            maximizeToolTip.Content = presenterIsRestored ? "Maximize" : "Restore Down";
            icon.Glyph = presenterIsRestored ? "\uE922" : "\uE923";
        }

        private async void GenerateWebView(Uri uri = null)
        {
            var view = new WebView2() { Visibility = Visibility.Collapsed };
            await view.EnsureCoreWebView2Async();
            view.Source = uri;
            view.CoreProcessFailed += (s, e) =>
            {
                switch (e.ExitCode)
                {
                    case 259:
                        AppInstance.Restart(string.Empty);
                        break;
                    default:
                        Close();
                        break;
                }
            };
            webViews.Add(view);
            tabList.SelectedIndex = webViews.Count - 1;
        }

        private void UpdateButtonBounds()
        {
            if (!AppWindow.IsVisible || Content.XamlRoot is not XamlRoot xamlRoot)
                return;

            var scale = xamlRoot.RasterizationScale;
            var width = AppWindow.Size.Width;

            Rect minButtonRect = new(new(width - (154 * scale), 0), new Size(46 * scale, 32 * scale));
            Rect maxButtonRect = new(new(width - (108 * scale), 0), new Size(46 * scale, 32 * scale));
            Rect closeButtonRect = new(new(width - (62 * scale), 0), new Size(46 * scale, 32 * scale));

            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Maximize, [maxButtonRect.ToRectInt32()]);
            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Minimize, [minButtonRect.ToRectInt32(), closeButtonRect.ToRectInt32()]);
        }
        private void Close(object sender, RoutedEventArgs e) => this.Close();

        bool changingRegions = false;
        private void Show_TitleBar(InputNonClientPointerSource sender, NonClientPointerEventArgs args)
        {
            if (args.RegionKind != NonClientRegionKind.Caption && args.RegionKind != NonClientRegionKind.Maximize && args.RegionKind != NonClientRegionKind.Minimize)
                return;

            if (!changingRegions)
                AnimationBuilder.Create().Size(Axis.Y, 32, 0, duration: TimeSpan.FromMilliseconds(300), layer: FrameworkLayer.Xaml).Start(titleBarContainer);
            else
                changingRegions = false;
        }
        private void Hide_TitleBar(InputNonClientPointerSource sender, NonClientPointerEventArgs args)
        {
            if (args.RegionKind != NonClientRegionKind.Caption && args.RegionKind != NonClientRegionKind.Maximize && args.RegionKind != NonClientRegionKind.Minimize)
                return;

            if (new Rect(new(sideBar.Width, 0), new Size((uint)(AppWindow.Size.Width - sideBar.Width), 26)).Contains(new(args.Point.X / Content.XamlRoot.RasterizationScale, (args.Point.Y - 5) / Content.XamlRoot.RasterizationScale)))
                changingRegions = true;
            else
                AnimationBuilder.Create().Size(Axis.Y, 0, 32, duration: TimeSpan.FromMilliseconds(300), layer: FrameworkLayer.Xaml).Start(titleBarContainer);
        }

        bool sideBarPinned = true;
        bool sideBarCooldown = false;
        bool sideBarContainsPointer = false;
        private void PinButton_Click(object sender, RoutedEventArgs e) => Pin_SideBar(pinButton.IsChecked == true, raisedByButton: true);
        private void Pin_SideBar(bool pin, bool force = false, bool raisedByButton = false)
        {
            if (force || raisedByButton || pinButton.IsChecked == false)
                sideBarPinned = pin;
            pinButton.IsEnabled = !force;

            if (force && pinButton.IsChecked == true)
                pinButton.Tag = pinButton.IsChecked;
            else if (!force && pinButton.Tag is bool pinned)
            {
                sideBarPinned = pinned;
                pinButton.Tag = null;
                return;
            }

            if (!sideBarContainsPointer && (!force || sideBar.Width == 0) && (raisedByButton || pinButton.IsChecked == false))
                AnimationBuilder.Create().Size(Axis.X, pin ? 300 : 0, pin ? 0 : 300, duration: TimeSpan.FromMilliseconds(300), layer: FrameworkLayer.Xaml).Start(sideBar);
        }
        private void SideBarOpener_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sideBarPinned)
                return;

            if (sideBarCooldown)
            {
                sideBarCooldown = false;
                return;
            }

            if (sideBar.Width == 0)
                AnimationBuilder.Create().Size(Axis.X, 300, 0, duration: TimeSpan.FromMilliseconds(300), layer: FrameworkLayer.Xaml).Start(sideBar);
        }
        private void SideBar_PointerEntered(object sender, PointerRoutedEventArgs e) => sideBarContainsPointer = true;
        private void SideBar_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            sideBarContainsPointer = false;

            if (sideBarPinned)
                return;

            if (sideBar.Width == 300)
            {
                AnimationBuilder.Create().Size(Axis.X, 0, 300, duration: TimeSpan.FromMilliseconds(300), layer: FrameworkLayer.Xaml).Start(sideBar);
                if (e.OriginalSource is Rectangle)
                    sideBarCooldown = true;
            }
        }

        private void TabIcon_Loaded(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            var view = image.DataContext as WebView2;
            view.CoreWebView2.FaviconChanged += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(view.CoreWebView2.FaviconUri))
                    image.Source = new BitmapImage() { UriSource = new Uri(view.CoreWebView2.FaviconUri) };
            };
        }
        private void TabTitle_Loaded(object sender, RoutedEventArgs e)
        {
            var textBlock = sender as TextBlock;
            var view = textBlock.DataContext as WebView2;
            textBlock.Text = "New tab";
            view.CoreWebView2.DocumentTitleChanged += (s, e) => textBlock.Text = view.CoreWebView2.DocumentTitle == "about:blank" ? "New tab" : view.CoreWebView2.DocumentTitle;
        }

        private void NewTab(object sender, RoutedEventArgs e) => GenerateWebView();
        private void SwitchTabs(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
        {
            int i = tabList.SelectedIndex + 1;
            if (i == webViews.Count)
                i = 0;

            tabList.SelectedIndex = i;
            e.Handled = true;
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e) => CloseTab((sender as FrameworkElement).DataContext as WebView2);
        private void CloseTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (webViews.Count < 1)
                Close();
            if (tabList.SelectedItem is WebView2 view)
                CloseTab(view);
            e.Handled = true;
        }
        private void CloseTab(WebView2 view)
        {
            int i = -1;
            if (tabList.SelectedItem as WebView2 == view)
            {
                i = tabList.SelectedIndex - 1;
                if (i < 0 && tabList.SelectedIndex + 1 <= webViews.Count - 1)
                    i++;
                else if (i < 0)
                    i = -1;
            }
            url.Text = string.Empty;

            view.Close();
            frame.Content = null;
            webViews.Remove(view);

            if (i >= 0)
                tabList.SelectedItem = webViews[i];
            else
                Pin_SideBar(true, true);
        }

        private void TabList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            tabList.SelectedItem = e.Items[0];
            frame.Content = null;
            Pin_SideBar(true, true);
        }
        private void TabList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs e)
        {
            if (e.Items[0] is not WebView2 view)
                return;

            var container = (sender.ContainerFromItem(view) as ListViewItem).ContentTemplateRoot as Grid;
            var image = container.Children[0] as Image;
            var textBlock = container.Children[1] as TextBlock;

            if (!string.IsNullOrWhiteSpace(view.CoreWebView2.FaviconUri))
                image.Source = new BitmapImage() { UriSource = new Uri(view.CoreWebView2.FaviconUri) };
            textBlock.Text = view.CoreWebView2.DocumentTitle == "about:blank" || string.IsNullOrWhiteSpace(view.CoreWebView2.DocumentTitle) ? "New tab" : view.CoreWebView2.DocumentTitle;

            frame.Content = view;
            bool isBlank = view.CoreWebView2.Source == "about:blank";
            Pin_SideBar(isBlank, isBlank);
        }

        private void TabList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (frame.Content is WebView2 webView)
                AssignWebView_EventHandlers(webView, true);

            if (tabList.SelectedItem is not WebView2 view)
                return;

            frame.Content = view;
            loading.Visibility = Visibility.Collapsed;

            url.Text = view.CoreWebView2.Source.RemoveURLFeatures();
            url.Select(url.Text.Length, 0);

            bool isBlank = view.CoreWebView2.Source == "about:blank";
            Pin_SideBar(isBlank, isBlank);

            AssignWebView_EventHandlers(view, false);
            AssignButton_EnabledStatus(view.CoreWebView2);
        }
        private void AssignButton_EnabledStatus(CoreWebView2 sender)
        {
            back.IsEnabled = sender.CanGoBack;
            forward.IsEnabled = sender.CanGoForward;
        }

        private void TabList_ProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
        {
            if (!args.Key.IsOneOf(EnumExtensions.IntToEnumArray<VirtualKey>(Enumerable.Range(49, 10))) || args.Modifiers != VirtualKeyModifiers.Control)
                return;

            int i = (int)args.Key - 49;
            if (i <= webViews.Count - 1)
                tabList.SelectedItem = webViews[i];
        }

        private void AssignWebView_EventHandlers(WebView2 view, bool remove)
        {
            if (remove)
            {
                view.CoreWebView2.NavigationStarting -= View_NavigationStarting;
                view.CoreWebView2.NavigationCompleted -= View_NavigationCompleted;
                view.CoreWebView2.ContainsFullScreenElementChanged -= View_ContainsFullScreenElementChanged;
            }
            else
            {
                view.CoreWebView2.NavigationStarting += View_NavigationStarting;
                view.CoreWebView2.NavigationCompleted += View_NavigationCompleted;
                view.CoreWebView2.ContainsFullScreenElementChanged += View_ContainsFullScreenElementChanged;
            }
        }
        private void View_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args) => loading.Visibility = Visibility.Visible;
        private void View_ContainsFullScreenElementChanged(CoreWebView2 sender, object e) => AppWindow.SetPresenter(sender.ContainsFullScreenElement ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Default);
        private void View_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            url.Text = sender.Source.RemoveURLFeatures();
            url.Select(url.Text.Length, 0);

            bool isBlank = sender.Source == "about:blank";
            loading.Visibility = Visibility.Collapsed;
            if (frame.Content is WebView2 view)
                view.Visibility = isBlank ? Visibility.Collapsed : Visibility.Visible;

            Pin_SideBar(isBlank, isBlank);
            AssignButton_EnabledStatus(sender);
        }

        private void FocusURL_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (url.FocusState.IsOneOf(FocusState.Pointer, FocusState.Keyboard, FocusState.Programmatic))
                tabList.Focus(FocusState.Keyboard);
            else
                url.Focus(FocusState.Keyboard);
            e.Handled = true;
        }
        private void URL_GotFocus(object sender, RoutedEventArgs e) => AnimationBuilder.Create().Size(Axis.X, 0, 108, duration: TimeSpan.FromMilliseconds(300), layer: FrameworkLayer.Xaml).Start(buttonContainer);
        private void URL_LostFocus(object sender, RoutedEventArgs e) => AnimationBuilder.Create().Size(Axis.X, 108, 0, duration: TimeSpan.FromMilliseconds(300), layer: FrameworkLayer.Xaml).Start(buttonContainer);
        private void URL_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
                return;

            string requestedUrl = url.Text;
            if (!Uri.IsWellFormedUriString(requestedUrl, UriKind.Absolute))
            {
                if (!Uri.TryCreate(requestedUrl = $"https://{requestedUrl}", UriKind.Absolute, out Uri searchUri) || searchUri.Host.Split('.').RemoveEmptyStrings().Length < 2)
                    requestedUrl = $"https://www.{GoogleSearch}{UrlEncoder.Default.Encode(url.Text)}";
            }

            var uri = new Uri(requestedUrl);
            if (frame.Content is not WebView2 view)
                GenerateWebView(uri);
            else
            {
                view.Source = uri;
                frame.Content = view;
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => (frame.Content as WebView2)!.CoreWebView2?.Reload();
        private void Back_Click(object sender, RoutedEventArgs e) => (frame.Content as WebView2)!.CoreWebView2?.GoBack();
        private void Forward_Click(object sender, RoutedEventArgs e) => (frame.Content as WebView2)!.CoreWebView2?.GoForward();
    }

    public static class RectExtensions
    {
        public static Windows.Graphics.RectInt32 ToRectInt32(this Rect rect) => new((int)Math.Round(rect.X), (int)Math.Round(rect.Y), (int)Math.Round(rect.Width), (int)Math.Round(rect.Height));
    }

    public static class EnumExtensions
    {
        public static bool IsOneOf<T>(this T sender, params T[] enums) where T : Enum => enums.Contains(sender);

        public static T[] IntToEnumArray<T>(IEnumerable<int> array) where T : Enum
        {
            T[] enums = [];
            foreach (int integer in array)
                enums = [.. enums, (T)(object)integer];
            return enums;
        }
    }

    public static class StringExtensions
    {
        public static string[] RemoveEmptyStrings(this string[] str)
        {
            var list = str.ToList();
            foreach (var item in str)
            {
                if (string.IsNullOrEmpty(item))
                    list.Remove(item);
            }
            return [.. list];
        }

        public static string RemoveURLFeatures(this string url)
        {
            url = url.Replace("https://www.", null).Replace("http://www.", null).Replace("https://", null).Replace("http://", null).Replace("about:blank", null);
            if (url.Length > 0 && url[^1] == '/')
                url = url.Remove(url.Length - 1);
            if (url.Contains(MainWindow.GoogleSearch, StringComparison.InvariantCultureIgnoreCase))
                url = Uri.UnescapeDataString(url.Replace(MainWindow.GoogleSearch, null).Split('&')[0]).Replace('+', ' ');
            return url;
        }
    }
}
