using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace StoreBot
{
    public class Program
    {
        public static Dictionary<ulong, string> TokenDictionary = new Dictionary<ulong, string>();

        public struct ConfigJson
        {
            [JsonProperty("discordtoken")]
            public string Token { get; private set; }
        }

        public static Task Client_Ready(DiscordClient _, ReadyEventArgs e)
        {
            _.Logger.LogInformation(new EventId(1337,"StoreBot") , "Client is ready to process events.");
            return Task.CompletedTask;
        }

        private static Task Client_GuildAvailable(DiscordClient _, GuildCreateEventArgs e)
        {
            _.Logger.LogInformation(new EventId(1337,"StoreBot") , $"Server available: {e.Guild.Name}");
            return Task.CompletedTask;
        }

        private static Task Client_ClientError(DiscordClient _, ClientErrorEventArgs e)
        {
            _.Logger.LogInformation(new EventId(1337,"StoreBot") , $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}");
            return Task.CompletedTask;
        }

        private static Task Slash_SlashCommandErrored(SlashCommandsExtension sender, DSharpPlus.SlashCommands.EventArgs.SlashCommandErrorEventArgs e)
        {
            sender.Client.Logger.LogInformation(new EventId(1337, "StoreBot"), $"{e.Context.User.Username} tried executing '{e.Context?.CommandName ?? "<unknown command>"}' but it errored: {e.Exception.GetType()}: {e.Exception.Message ?? "<no message>"}");
            throw new NotImplementedException();
        }

        private static Task Slash_SlashCommandExecuted(SlashCommandsExtension sender, DSharpPlus.SlashCommands.EventArgs.SlashCommandExecutedEventArgs e)
        {
            sender.Client.Logger.LogInformation(new EventId(1337, "StoreBot"), $"{e.Context.User.Username}:{e.Context.User.Id.ToString()} ran command '{e.Context.CommandName}'");
            return Task.CompletedTask;
        }

        public static async Task<DiscordConfiguration> LoadConfig()
        {
            string token = Environment.GetEnvironmentVariable("STOREBOTTOKEN");
            if (!String.IsNullOrEmpty(token))
            {
                DiscordConfiguration configenv = new DiscordConfiguration()
                {
                    Token = token,
                    TokenType = TokenType.Bot,
                    AutoReconnect = true,
                    MinimumLogLevel = LogLevel.Information
                };
                return configenv;
            }
            string json = "";
            using (FileStream fs = File.Open("config.json", FileMode.Open))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    json = await sr.ReadToEndAsync();
                }
            }
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            DiscordConfiguration config = new DiscordConfiguration()
            {
                Token = cfgjson.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel= LogLevel.Information
            };
            return config;
        }

        public static async Task Main(string[] args)
        {
            Console.WriteLine($"StoreBot - {Assembly.GetExecutingAssembly().GetName().Version.ToString()}");
            DiscordClient client = new DiscordClient(await LoadConfig());
            client.Ready += Client_Ready;
            client.GuildAvailable += Client_GuildAvailable;
            client.ClientErrored += Client_ClientError;

            var slash = client.UseSlashCommands();
            slash.RegisterCommands<StoreCommands>();
            slash.RegisterCommands<AuthCommands>();
            slash.SlashCommandErrored += Slash_SlashCommandErrored;
            slash.SlashCommandExecuted += Slash_SlashCommandExecuted;

            await client.ConnectAsync(new DiscordActivity
                ("DisplayCatalog", ActivityType.Watching), UserStatus.Online);
            await Task.Delay(-1);
        }
    }
}
