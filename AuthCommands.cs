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
        [Command("submittoken"), RequireDirectMessage(), Description("Provide a MSA token to allow for flighted queries, DM ONLY")]
        public async Task IngestAuthToken(CommandContext cct, string token)
        {
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
