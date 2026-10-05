using System;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ClubStack.Desktop;

/// <summary>
/// Thin WebView2 host for the ClubStack client app. Loads the SPA served by the
/// ClubStack API; override the URL with the CLUBSTACK_WEB_URL environment variable.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly Uri HomeUri = new(
        Environment.GetEnvironmentVariable("CLUBSTACK_WEB_URL") ?? "https://localhost:5001/");

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();

            // Dev-shell only: trust the local ASP.NET Core development certificate.
            WebView.CoreWebView2.ServerCertificateErrorDetected += (_, args) =>
            {
                if (HomeUri.IsLoopback)
                {
                    args.Action = CoreWebView2ServerCertificateErrorAction.AlwaysAllow;
                }
            };

            WebView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;
            WebView.CoreWebView2.NavigationStarting += WebView_NavigationStarting;

            WebView.Source = HomeUri;
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not start ClubStack: {exception.Message}";
        }
    }

    private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        StatusText.Text = $"Loading {e.Uri}…";
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        StatusText.Text = e.IsSuccess
            ? "ClubStack ready."
            : $"ClubStack could not be reached ({e.WebErrorStatus}). Is the API running on {HomeUri}?";
    }
}