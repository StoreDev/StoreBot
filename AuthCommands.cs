using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreBot
{
    public class AuthCommands : BaseCommandModule
    {
        [Command("submittoken"), Description("Provide a MSA token to allow for flighted queries, DM ONLY")]
        public async Task IngestAuthToken(CommandContext cct, string token)
        {
            if(cct.Guild != null)
            {
                var lastmessage = await cct.Channel.GetMessagesAsync(1);
                await cct.Channel.DeleteMessageAsync(lastmessage[0]);
                await cct.RespondAsync($"{cct.Message.Author.Mention} you must submit your token via a DM to avoid account takeover. Your token may have been exposed, an account relog is recommended.");
                return;
            }
            ulong DiscordAuthorID = cct.User.Id;
            if (Program.TokenDictionary.ContainsKey(DiscordAuthorID))
            {
                Program.TokenDictionary.Remove(DiscordAuthorID);
                Program.TokenDictionary.Add(DiscordAuthorID, token);
                await cct.RespondAsync("Your token has been updated.");
                return;

            }
            Program.TokenDictionary.Add(DiscordAuthorID, token);
            await cct.RespondAsync($"Your token has been ingested. It will be used for all future commands coming from you. (Discord ID: {DiscordAuthorID})"); ;
        }


    }
}
