using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace StoreBot
{
    public class Program
    {

        public struct ConfigJson
        {
            [JsonProperty("discordtoken")]
            public string Token { get; private set; }
        }

        public static Task Client_Ready(ReadyEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "StoreBot", "Client is ready to process events.", DateTime.Now);
            return Task.CompletedTask;
        }

        private static Task Client_GuildAvailable(GuildCreateEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "StoreBot", $"Server available: {e.Guild.Name}", DateTime.Now);
            return Task.CompletedTask;
        }

        private static Task Client_ClientError(ClientErrorEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "StoreBot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);
            return Task.CompletedTask;
        }

        private static Task Commands_CommandExecuted(CommandExecutionEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info, "StoreBot", $"{e.Context.User.Username}:{e.Context.User.Id.ToString()} ran command '{e.Command.QualifiedName}'", DateTime.Now);
            return Task.CompletedTask;
        }

        private static Task Commands_CommandErrored(CommandErrorEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Error, "StoreBot", $"{e.Context.User.Username} tried executing '{e.Command?.QualifiedName ?? "<unknown command>"}' but it errored: {e.Exception.GetType()}: {e.Exception.Message ?? "<no message>"}", DateTime.Now);
            return Task.CompletedTask;
        }

        public static async Task<DiscordConfiguration> LoadConfig()
        {
            string token = Environment.GetEnvironmentVariable("storebottoken", EnvironmentVariableTarget.User);
            if (!String.IsNullOrEmpty(token))
            {
                DiscordConfiguration configenv = new()
                {
                    Token = token,
                    TokenType = TokenType.Bot,
                    AutoReconnect = true,
                    LogLevel = LogLevel.Info,
                    UseInternalLogHandler = true
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
            DiscordConfiguration config = new()
            {
                Token = cfgjson.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                LogLevel = LogLevel.Info,
                UseInternalLogHandler = true
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
            client.UseInteractivity(new InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(2)
            });

            var ccfg = new CommandsNextConfiguration
            {

                // enable responding in direct messages
                EnableDms = true,

                // enable mentioning the bot as a command prefix
                EnableMentionPrefix = true
            };

            var Commands = client.UseCommandsNext(ccfg);
            Commands.CommandErrored += Commands_CommandErrored;
            Commands.CommandExecuted += Commands_CommandExecuted;
            Commands.RegisterCommands<StoreCommands>();
            await client.ConnectAsync(new DiscordActivity("DisplayCatalog", ActivityType.Watching), UserStatus.Online);
            await Task.Delay(-1);
        }


    }
}
