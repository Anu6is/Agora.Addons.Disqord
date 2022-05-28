namespace Agora.Addons.Disqord.Menus
{
    public class GuildSettingsOption
    {
        private readonly Func<GuildSettingsContext, List<GuildSettingsOption>, BaseSettingsView> _func;
        
        public string Name { get; init; }
        public string Description { get; init; }
        public bool IsDefault { get; set; }

        public GuildSettingsOption(string name, string description, Func<GuildSettingsContext, List<GuildSettingsOption>, BaseSettingsView> func)
        {
            Name = name;
            Description = description;
            
            _func = func;
        }

        public BaseSettingsView GetView(GuildSettingsContext conext, List<GuildSettingsOption> options) => _func(conext, options);
    }
}
