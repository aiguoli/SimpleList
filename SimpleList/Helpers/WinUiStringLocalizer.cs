using SimpleList.Core.Contracts;

namespace SimpleList.Helpers;

public class WinUiStringLocalizer : IStringLocalizer
{
    public string this[string key] => ResourceHelper.GetLocalized(key);
    public string Format(string key, params object[] args) => string.Format(ResourceHelper.GetLocalized(key), args);
}
