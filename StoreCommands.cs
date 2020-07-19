using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using StoreLib.Models;
using StoreLib.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StoreBot
{
    public class StoreCommands : BaseCommandModule
    {
        [Command("info"), Description("Returns info about the current StoreBot Instance")]
        public async Task InfoAsync(CommandContext cct)
        {

            var embeddialog = new DiscordEmbedBuilder
            {
                Title = "StoreBot Info",
                Color = DiscordColor.Cyan,
                Description = $"StoreBot Version {Assembly.GetExecutingAssembly().GetName().Version.ToString()}"



            };
            await cct.RespondAsync("", false, embeddialog);

        }

        /*
        [Command("help"), Description("Returns all registered commands and their descriptions.")]
        public async Task Help(CommandContext cct)
        {
            foreach(var command in cct.Client.GetCommandsNext().RegisteredCommands)
            {
                StringBuilder message = new StringBuilder();
                message.AppendLine($"{command.Value.Name} - {command.Value.Description}");
            }
        }
        */

        [Command("packages"), Description("Queries FE3 for packages for the specified ID.")]
        public async Task PackagesAsync(CommandContext cct, string ID)
        {
            DisplayCatalogHandler dcat = DisplayCatalogHandler.ProductionConfig();
            //Push the input id through a Regex filter in order to take the onestoreid from the storepage url
            await dcat.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value);
            if (dcat.IsFound)
            {
                var productembedded = new DiscordEmbedBuilder()
                {
                    Title = dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle,
                    ImageUrl = dcat.ProductListing.Product.LocalizedProperties[0].Images[0].Uri.Replace("//", "https://"),
                    Color = DiscordColor.Gold,
                    Description = dcat.ProductListing.Product.LocalizedProperties[0].ProductDescription
                };
                var packages = await dcat.GetPackagesForProductAsync();
                foreach(PackageInstance package in packages)
                {
                    productembedded.AddField(package.PackageMoniker, package.PackageUri.ToString());
                }
                productembedded.Build();
                Debug.WriteLine(dcat.ProductListing.Product.LocalizedProperties[0].Images[0].Uri.Replace("//", "https://"));
                await cct.RespondAsync("", false, productembedded);
            }
            else
            {
                await cct.RespondAsync("Product not found.");
            }

        }

        [Command("advancedquery"), Description("A customizable query that allows the caller to specify the environment and locale.")]
        public async Task AdvancedQueryAsync(CommandContext cct, string ID, string environment, string localestring)
        {
            if(String.IsNullOrEmpty(ID) || String.IsNullOrEmpty(environment) || String.IsNullOrEmpty(localestring))
            {
                await cct.RespondAsync("Please supply all required arguments. Example: advancedquery 9wzdncrfj3tj INT US-EN");
                return;
            }
            bool marketresult = Enum.TryParse(localestring.Split('-')[0], out Market market);
            bool langresult = Enum.TryParse(localestring.Split('-')[1].ToLower(), out Lang lang);
            if(!marketresult || !langresult)
            {
                await cct.RespondAsync($"Invalid Market or Lang specified. Example: US-EN for United States English, you provided Market {localestring.Split('-')[0]} and Language {localestring.Split('-')[1]}");
                return;
            }
            bool environmentresult = Enum.TryParse(environment, out DCatEndpoint env);
            if (!environmentresult)
            {
                await cct.RespondAsync($"Invalid Environment specified. Example: Production for Production, Int for Instance. You provided {environment}");
                return;
            }
            DisplayCatalogHandler customizedhandler = new DisplayCatalogHandler(env, new Locale(market, lang, true));
            await customizedhandler.QueryDCATAsync(ID);
            if (customizedhandler.IsFound)
            {
                var productembedded = new DiscordEmbedBuilder()
                {
                    Title = customizedhandler.ProductListing.Product.LocalizedProperties[0].ProductTitle,
                    Color = DiscordColor.Gold
                };
                productembedded.AddField("Description:", customizedhandler.ProductListing.Product.LocalizedProperties[0].ProductDescription.Substring(0, 1023));
                productembedded.Build();
                await cct.RespondAsync("", false, productembedded);
            }
            else
            {
                await cct.RespondAsync("Product not found.");
            }

        }


    }
}
