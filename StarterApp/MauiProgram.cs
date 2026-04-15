using Microsoft.Extensions.Logging;
using StarterApp.ViewModels;
using StarterApp.Database.Data;
using StarterApp.Views;
using System.Diagnostics;
using StarterApp.Services;
using System.Net.Http.Headers;

namespace StarterApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddDbContext<AppDbContext>();

        // Register the HTTP Interceptor that will automatically attach and refresh JWTs 
        builder.Services.AddTransient<AuthenticationInterceptor>();

        var apiBaseUrl = Environment.GetEnvironmentVariable("AUTH_API_BASE_URL") ?? "http://localhost:8080";

        // AuthClient: Used specifically for Login/Register/Refresh operations. 
        // This explicitly bypasses the interceptor to prevent infinite loop refresh attempts.
        builder.Services.AddHttpClient("AuthClient", client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // ApiClient: Used for all standard domain data operations (e.g. Items, Users). 
        // Automatically routes through AuthenticationInterceptor for JWT bearer attachment.
        builder.Services.AddHttpClient("ApiClient", client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddHttpMessageHandler<AuthenticationInterceptor>();

        builder.Services.AddSingleton<IAuthenticationService>(sp => 
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new JWTAuthenticationService(factory.CreateClient("AuthClient"));
        });

        builder.Services.AddSingleton<IItemApiService>(sp => 
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new ItemApiService(factory.CreateClient("ApiClient"));
        });
        
        builder.Services.AddSingleton<IRentalApiService>(sp => 
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new RentalApiService(factory.CreateClient("ApiClient"));
        });

        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IUserNotificationService, UserNotificationService>();

        builder.Services.AddSingleton<AppShellViewModel>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<App>();

        builder.Services.AddTransient<AboutViewModel>();
        builder.Services.AddTransient<AboutPage>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddSingleton<RegisterViewModel>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<UserListViewModel>();
        builder.Services.AddTransient<UserListPage>();
        builder.Services.AddTransient<UserDetailPage>();
        builder.Services.AddTransient<UserDetailViewModel>();
        builder.Services.AddTransient<ItemListViewModel>();
        builder.Services.AddTransient<ItemListPage>();
        builder.Services.AddTransient<ItemDetailViewModel>();
        builder.Services.AddTransient<ItemDetailPage>();
        builder.Services.AddTransient<RentalListViewModel>();
        builder.Services.AddTransient<RentalListPage>();
        builder.Services.AddTransient<RentalRequestViewModel>();
        builder.Services.AddTransient<RentalRequestPage>();
        builder.Services.AddTransient<TempViewModel>();
        builder.Services.AddTransient<TempPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}