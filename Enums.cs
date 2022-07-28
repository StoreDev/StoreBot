using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.SlashCommands;

namespace StoreBot
{
    public class Enums
    {
        public enum DeviceFamily
        {
            [ChoiceName("Desktop")]
            Desktop,
            [ChoiceName("Mobile")]
            Mobile,
            [ChoiceName("Xbox")]
            Xbox,
            [ChoiceName("Server Core")]
            ServerCore,
            [ChoiceName("Iot Core")]
            IotCore,
            [ChoiceName("HoloLens")]
            HoloLens,
            [ChoiceName("Andromeda")]
            Andromeda,
            [ChoiceName("Universal")]
            Universal,
            [ChoiceName("Windows Core OS")]
            WCOS
        }

        public enum DCatEndpoint
        {
            [ChoiceName("Production")]
            Production,
            [ChoiceName("Internal")]
            Int,
            [ChoiceName("Xbox")]
            Xbox,
            [ChoiceName("Xbox Internal")]
            XboxInt,
            [ChoiceName("Dev")]
            Dev,
            [ChoiceName("OneP")]
            OneP,
            [ChoiceName("OneP Internal")]
            OnePInt
        }
    }
}
