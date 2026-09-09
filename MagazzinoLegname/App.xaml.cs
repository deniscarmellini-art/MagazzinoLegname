using System.Configuration;
using System.Data;
using System.Globalization;
using System.Windows;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Services;
using System.Diagnostics;
using System.Windows.Markup;

namespace MagazzinoLegname
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
public partial class App : Application
{
        public App()
        {
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("it-IT")));

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("it-IT");
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            SqlPersistenceRoot.InitializeDatabase();
            SqlDomainConfigurationState.Initialize();
        }
        catch (Exception exception)
        {
#if DEBUG
            Debug.WriteLine($"Startup exception type: {exception.GetType().FullName}");
            Debug.WriteLine($"Startup message: {exception.Message}");
            Debug.WriteLine($"Startup inner exception: {exception.InnerException}");
            Debug.WriteLine($"Startup stack trace: {exception.StackTrace}");
#endif
            MessageBox.Show(DatabaseErrorTranslator.Translate(exception).OperatorMessage,
                "Avvio non riuscito", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }
        base.OnStartup(e);
    }
}

}
