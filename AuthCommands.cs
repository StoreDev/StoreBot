using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XboxWebApi.Authentication;
using XboxWebApi.Authentication.Model;
using XboxWebApi.Common;
using System.IO;
using System.Net.Security;


namespace StoreBot
{
    public class AuthCommands : ApplicationCommandModule
    {
        [SlashCommand("SubmitToken", "Provide a MSA token to allow for flighted queries, DM ONLY")]
        public async Task IngestAuthToken(InteractionContext ctx, [Option("token","The MSA token / Xtoken to ingest")] string token)
        {
            if (ctx.Guild != null)
            {
                var lastmessage = await ctx.Channel.GetMessagesAsync(1);
                await ctx.Channel.DeleteMessageAsync(lastmessage[0]);
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"{ctx.User.Mention} you must submit your token via a DM to avoid account takeover. Your token may have been exposed, an account relog is recommended."));
                return;
            }
            ulong DiscordAuthorID = ctx.User.Id;
            if (Program.TokenDictionary.ContainsKey(DiscordAuthorID))
            {
                Program.TokenDictionary.Remove(DiscordAuthorID);
                Program.TokenDictionary.Add(DiscordAuthorID, token);
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Your token has been updated."));
                return;

            }
            Program.TokenDictionary.Add(DiscordAuthorID, token);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Your token has been ingested. It will be used for all future commands coming from you. (Discord ID: {DiscordAuthorID})"));
        }

        [SlashCommand("GenerateTokens", "Export tokens from authentication url, run the command with no arguments for more info")]
        public async Task GenerateTokens(InteractionContext ctx, [Option("TokenUrl","Authentication URL. Leave this blank to get usage instructions for this command.")] string tokenuri = null)
        {

            if (ctx.Guild != null)
            {
                var lastmessage = await ctx.Channel.GetMessagesAsync(1);
                await ctx.Channel.DeleteMessageAsync(lastmessage[0]);
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"{ctx.User.Mention} you must submit your token via a DM to avoid account takeover. Your token may have been exposed, an account relog is recommended."));
                return;
            }

            if (tokenuri == null)
            {
                var inforesponse = new DiscordEmbedBuilder()
                {
                    Title = "Advanced info",
                    Description = "The tokenuri parameter is the response from: \nhttps://login.live.com/oauth20_authorize.srf?display=touch&scope=service%3A%3Auser.auth.xboxlive.com%3A%3AMBI_SSL&redirect_uri=https%3A%2F%2Flogin.live.com%2Foauth20_desktop.srf&locale=en&response_type=token&client_id=0000000048093EE3, the full command should look something like:\n`generatetokens https://login.live.com/oauth20_desktop.srf?...access_token=...&refresh_token=...`"
                };
                inforesponse.Build();
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(inforesponse.Build()));
                return;
            }
            try
            {
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
                WindowsLiveResponse response = AuthenticationService.ParseWindowsLiveResponse(tokenuri.ToString());
                AuthenticationService authenticator = new AuthenticationService(response);
                //get user token
                authenticator.UserToken = await AuthenticationService.AuthenticateXASUAsync(authenticator.AccessToken);
                //get xtoken
                authenticator.XToken = await AuthenticationService.AuthenticateXSTSAsync(authenticator.UserToken);
                //set user information
                authenticator.UserInformation = authenticator.XToken.UserInformation;
                //export json
                Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(AuthenticationService.DumpToJson(authenticator)));
                //respond with tokens
                var msg = await new DiscordMessageBuilder().WithFiles(new Dictionary<string, Stream>() { { "tokens.json", stream} }).SendAsync(ctx.Channel);
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Generated tokens successfully:"));
            }
            catch
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("An unknown authentication error occured"));
            }
            return;
        }
    }
}
