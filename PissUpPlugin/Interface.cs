using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PissUpPlugin
{
    using MessageAction = Func<String, CancellationToken, Task>;
    public interface IGame
    {
        string GetFriendlyName();
        void DrawConfig(uint GameCount);
        Task Run(Plugin GamePlugin, CancellationToken TaskCancellationToken, MessageAction SendMessage);
    }

    public class Services
    {
    }
}
