namespace Agora.Addons.Disqord.Common
{
    public class PluginParameters : Dictionary<string, object>
    {
        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (TryGetValue(key, out object value))
            {
                if (value is T typedValue) return typedValue;
                
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }
    }
}
