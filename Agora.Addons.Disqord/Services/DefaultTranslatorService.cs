using System.Globalization;

namespace Agora.Addons.Disqord;

internal class DefaultTranslatorService : ILocalizationService
{
    public void SetCulture(CultureInfo culture) { }

    public string Translate(string key, string resourceName) => key;
}
