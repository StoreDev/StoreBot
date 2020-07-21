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
using System.Security.Cryptography;
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
                //configure product embed 
                var productembedded = new DiscordEmbedBuilder()
                {
                    //configure title of embed
                    Title = "Packages:",
                    //configure colour of embed
                    Color = DiscordColor.Gold,
                    //add app info like name and logo to the footer of the embed
                    Footer = new Discord​Embed​Builder.EmbedFooter() { Text = dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle, IconUrl= dcat.ProductListing.Product.LocalizedProperties[0].Images[0].Uri.Replace("//", "https://"), }
                };
                //set maximum number of free characters per embed
                int freecharsmax = 5991 - dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle.Length;
                //set the number of free characters in the current embed
                int freechars = freecharsmax;
                //create empty package list string
                string packagelist = "";
                //start typing indicator
                await cct.TriggerTypingAsync();
                //get all packages for the product
                var packages = await dcat.GetPackagesForProductAsync();
                //iterate through all packages
                foreach(PackageInstance package in packages)
                {
                    //temporarily hold the value of the new package in a seperate var in order to check if the field will be to long
                    string packagelink= $"[{package.PackageMoniker}]({package.PackageUri})";
                    //check if the combined lengths of the package list and new package link will not exceed the maximum field length of 1024 characters
                    if ((packagelink.Length + packagelist.Length) >= 1024)
                    {
                        //if the combined lengths of the package list and new package link exceed 1024 characters
                        //subtract the number of free characters being taken up by the new field
                        freechars -= packagelist.Length + 1;
                        // if the embed will exceed the max number of characters allowed (6000)
                        if (freechars <= 0)
                        {
                            //build product embed
                            productembedded.Build();
                            //send product embed
                            await cct.RespondAsync("", false, productembedded);
                            //reset fields of product embed
                            productembedded.RemoveFieldRange(0,productembedded.Fields.Count);
                            //reset number of characters free in embed
                            freechars = freecharsmax - packagelink.Length-1;
                        }
                        //push the packages as a field
                        productembedded.AddField("‍", packagelist);
                        //reset packagelist
                        packagelist = packagelink;

                    }
                    else
                    {
                        //if the combined lengths of the package list and new package link DO NOT exceed 1024 characters
                        packagelist += "\n" + packagelink;
                    }
                }
                //check if the last field will not exceed the maximum number of characters per embed
                if (freechars <= 0)
                {
                    //build product embed
                    productembedded.Build();
                    //send product embed
                    await cct.RespondAsync("", false, productembedded);
                    //reset fields of product embed
                    productembedded.RemoveFieldRange(0, productembedded.Fields.Count);
                }
                //push the last field
                productembedded.AddField("‍", packagelist);
                //build product embed
                productembedded.Build();
                //send product embed
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
            //start typing indicator
            await cct.TriggerTypingAsync();
            if (String.IsNullOrEmpty(ID) || String.IsNullOrEmpty(environment) || String.IsNullOrEmpty(localestring))
            {
                await cct.RespondAsync("Please supply all required arguments. Example: advancedquery 9wzdncrfj3tj Int US-EN");
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
                    Title = "App Info:",
                    Footer = new Discord​Embed​Builder.EmbedFooter() { Text = $"{customizedhandler.ProductListing.Product.LocalizedProperties[0].ProductTitle} - {customizedhandler.ProductListing.Product.LocalizedProperties[0].PublisherName}", IconUrl= customizedhandler.ProductListing.Product.LocalizedProperties[0].Images[0].Uri.Replace("//", "https://") },
                    Color = DiscordColor.Gold
                };
                productembedded.AddField("Description:", customizedhandler.ProductListing.Product.LocalizedProperties[0].ProductDescription.Substring(0, 1023));
                productembedded.AddField("Rating:", $"{customizedhandler.ProductListing.Product.MarketProperties[0].UsageData[0].AverageRating} Stars");
                productembedded.AddField("Last Modified:", customizedhandler.ProductListing.Product.MarketProperties[0].OriginalReleaseDate.ToString());
                productembedded.AddField("Product Type:", customizedhandler.ProductListing.Product.ProductType);
                productembedded.AddField("Is a Microsoft Listing:", customizedhandler.ProductListing.Product.IsMicrosoftProduct.ToString());
                if (customizedhandler.ProductListing.Product.ValidationData != null)
                {
                    productembedded.AddField("Validation Info:", customizedhandler.ProductListing.Product.ValidationData.RevisionId);
                }
                if (customizedhandler.ProductListing.Product.SandboxID != null)
                {
                    productembedded.AddField("SandBoxID:", customizedhandler.ProductListing.Product.SandboxID);
                }
                foreach (AlternateId PID in customizedhandler.ProductListing.Product.AlternateIds) //Dynamicly add any other ID(s) that might be present rather than doing a ton of null checks.
                {
                    productembedded.AddField($"{PID.IdType}:", PID.Value);
                }
                if (customizedhandler.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.FulfillmentData != null)
                {
                    productembedded.AddField("WuCategoryID:", customizedhandler.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.FulfillmentData.WuCategoryId);
                    productembedded.AddField("EAppx Key ID:", customizedhandler.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.Packages[0].KeyId);
                }
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
