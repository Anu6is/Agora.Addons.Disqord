using System.Globalization;

namespace Agora.Addons.Disqord;

public interface ILocalizationService
{
    void SetCulture(CultureInfo culture);
    string Translate(string key, string resourceName);
}
