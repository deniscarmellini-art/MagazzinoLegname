using System.Configuration;
using System.Data;
using System.Globalization;
using System.Windows;
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
    }

}
