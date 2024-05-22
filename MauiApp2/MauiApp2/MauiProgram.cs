using Microsoft.Extensions.Logging;

namespace MauiApp2
{
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
                    fonts.AddFont("MaterialIconsOutlined-Regular.otf", "MaterialIconsOutlined-Regular");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons-Regular");
                    fonts.AddFont("Strande2.ttf", "Strande2");
                    fonts.AddFont("icomoon.ttf", "icomoon");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // Register TestViewModel and TestPage
            builder.Services.AddSingleton<viewDataModel>();
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<Profile>();


            return builder.Build();
        }

        
    }
}
