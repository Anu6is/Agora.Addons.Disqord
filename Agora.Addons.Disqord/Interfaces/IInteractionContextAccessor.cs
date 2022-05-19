using Disqord.Bot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agora.Addons.Disqord
{
    public interface IInteractionContextAccessor
    {
        public DiscordInteractionContext Context { get; set; }
    }
}
