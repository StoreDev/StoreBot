using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using StoreLib.Models;
using StoreLib.Services;
using StoreLib.Utilities;
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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;

namespace StoreBot
{
    public class StoreCommands : ApplicationCommandModule
    {

        public String BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }
        private static readonly MSHttpClient _httpClient = new MSHttpClient();

        [SlashCommand("Info", "Returns info about the current StoreBot Instance")]
        public async Task InfoAsync(InteractionContext ctx)
        {

            var embeddialog = new DiscordEmbedBuilder
            {
                Title = "StoreBot Info",
                Color = DiscordColor.Cyan,
                Description = $"StoreBot Version {Assembly.GetExecutingAssembly().GetName().Version.ToString()}"
            };
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(embeddialog.Build()));

        }

        [SlashCommand("Clean", "Removes the last X messages sent by StoreBot, the user must have the manage messages permission.")]
        public async Task CleanAsync(InteractionContext ctx, [Option("NumberToClean", "Number of messages to delete")] long numbertoclean)
        {
            if (!ctx.Member.Permissions.HasPermission(Permissions.ManageMessages))
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Missing manage messages perm"));
                return;
            }
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            int messagesdeleted = 0;
            var messages = await ctx.Channel.GetMessagesAsync(100);
            foreach (var message in messages)
            {
                if (messagesdeleted >= numbertoclean)
                {
                    var finishedmessage = await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Finished Cleaning!"));
                    return;
                }
                else if (message.Author == ctx.Client.CurrentUser || message.MentionedUsers.Contains(ctx.Client.CurrentUser))
                {
                    await message.DeleteAsync();
                    messagesdeleted++;
                }
            }

        }



        [SlashCommand("Packages", "Queries FE3 for packages for the specified ID in the specified locale and environment.")]
        public async Task PackagesAsync(
            InteractionContext ctx, 
            [Option("PackageID", "The package ID for a given app (you may provide this as a link to a store page)")] 
            string ID,
            [Option("Locale", "Specify a locale Example: EN-US for United States English")]
            string localestring = "EN-US",
            [Option("Environment", "Environment to search (production by default)")]
            Enums.DCatEndpoint env = Enums.DCatEndpoint.Production)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            bool marketresult = Enum.TryParse(localestring.Split('-')[1], true, out Market market);
            bool langresult = Enum.TryParse(localestring.Split('-')[0].ToLower(), true, out Lang lang);
            if (!marketresult || !langresult)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Invalid Market or Lang specified. Example: US-EN for United States English, you provided Market {localestring.Split('-')[0]} and Language {localestring.Split('-')[1]}"));
                return;
            }
            DisplayCatalogHandler dcat = new DisplayCatalogHandler((DCatEndpoint)env, new Locale(market, lang, true));
            //Push the input id through a Regex filter in order to take the onestoreid from the storepage url
            if (new Regex(@"[a-zA-Z0-9]{12}").Matches(ID).Count == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Invalid Product ID"));
                return;
            }
            if (Program.TokenDictionary.ContainsKey(ctx.User.Id))
            {
                await dcat.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value, Program.TokenDictionary.GetValueOrDefault(ctx.User.Id));
            }
            else
            {
                await dcat.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value);
            }
            if (dcat.IsFound)
            {
                List<DiscordEmbedBuilder> discordEmbedBuilders = new List<DiscordEmbedBuilder>();
                if (dcat.ProductListing.Product != null) //One day ill fix the mess that is the StoreLib JSON, one day.
                {
                    dcat.ProductListing.Products = new List<Product>();
                    dcat.ProductListing.Products.Add(dcat.ProductListing.Product);
                }
                if (dcat.ProductListing.Products[0].LocalizedProperties[0].Images[0].Uri.StartsWith("//"))
                { //Some apps have a broken url, starting with a //, this removes that slash and replaces it with proper https.
                    dcat.ProductListing.Products[0].LocalizedProperties[0].Images[0].Uri = dcat.ProductListing.Products[0].LocalizedProperties[0].Images[0].Uri.Replace("//", "https://");
                }
                //configure product embed 
                var productembedded = new DiscordEmbedBuilder()
                {
                    //configure colour of embed
                    Color = DiscordColor.Gold,
                    //add app info like name and logo to the footer of the embed
                    Footer = new Discord​Embed​Builder.EmbedFooter() { Text = $"{dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle} - {dcat.ProductListing.Product.LocalizedProperties[0].PublisherName}", IconUrl = dcat.ProductListing.Product.LocalizedProperties[0].Images[0].Uri }
                };
                //set maximum number of free characters per embed (minus 10 for pages)
                int freecharsmax = 5978 - dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle.Length;
                freecharsmax -= dcat.ProductListing.Product.LocalizedProperties[0].PublisherName.Length;
                //set the number of free characters in the current embed
                int freechars = freecharsmax;
                //create empty package list string
                string packagelist = "";
                //get all packages for the product.
                if (dcat.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.FulfillmentData != null)
                {
                    var packages = await dcat.GetPackagesForProductAsync();
                    //iterate through all packages
                    foreach (PackageInstance package in packages)
                    {
                        HttpRequestMessage httpRequest = new HttpRequestMessage();
                        httpRequest.RequestUri = package.PackageUri;
                        //httpRequest.Method = HttpMethod.Get;
                        httpRequest.Method = HttpMethod.Head;
                        httpRequest.Headers.Add("Connection", "Keep-Alive");
                        httpRequest.Headers.Add("Accept", "*/*");
                        //httpRequest.Headers.Add("Range", "bytes=0-1");
                        httpRequest.Headers.Add("User-Agent", "Microsoft-Delivery-Optimization/10.0");
                        HttpResponseMessage httpResponse = await _httpClient.SendAsync(httpRequest, new System.Threading.CancellationToken());
                        HttpHeaders headers = httpResponse.Content.Headers;
                        IEnumerable<string> values;
                        string packagelink;
                        if (headers.TryGetValues("Content-Disposition", out values))
                        {
                            ContentDisposition contentDisposition = new ContentDisposition(values.First());
                            string filename = contentDisposition.FileName;
                            packagelink = $"[{filename}]({package.PackageUri})";
                        }
                        else
                        {
                            //temporarily hold the value of the new package in a seperate var in order to check if the field will be too long
                            packagelink = $"[{package.PackageMoniker}]({package.PackageUri})";
                        }
                        if (headers.TryGetValues("Content-Length", out values))
                        {
                            string filesize = BytesToString(long.Parse(values.FirstOrDefault()));
                            packagelink += $": {filesize}";
                        }
                        //check if the combined lengths of the package list and new package link will not exceed the maximum field length of 1024 characters
                        if ((packagelink.Length + packagelist.Length) >= 1024)
                        {
                            //if the combined lengths of the package list and new package link exceed 1024 characters
                            //subtract the number of free characters being taken up by the new field
                            freechars -= packagelist.Length + 1;
                            // if the embed will exceed the max number of characters allowed (6000)
                            if (freechars <= 0)
                            {
                                //add embed to list
                                discordEmbedBuilders.Add(productembedded);
                                //reset and configure product embed 
                                productembedded = new DiscordEmbedBuilder()
                                {
                                    //configure colour of embed
                                    Color = DiscordColor.Gold,
                                    //add app info like name and logo to the footer of the embed
                                    Footer = new Discord​Embed​Builder.EmbedFooter() { Text = $"{dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle} - {dcat.ProductListing.Product.LocalizedProperties[0].PublisherName}", IconUrl = dcat.ProductListing.Product.LocalizedProperties[0].Images[0].Uri }
                                };
                                //reset number of characters free in embed
                                freechars = freecharsmax - packagelink.Length - 1;
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
                    if (dcat.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.Packages.Count > 0)
                    {
                        if (dcat.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.Packages.Count != 0)
                        {
                            //For some weird reason, some listings report having packages when really they don't have one hosted. This checks the child to see if the package is really null or not.
                            if (!object.ReferenceEquals(dcat.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.Packages[0].PackageDownloadUris, null))
                            {
                                foreach (var Package in dcat.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.Packages[0].PackageDownloadUris)
                                {
                                    //create new uri from package uri
                                    Uri PackageURL = new Uri(Package.Uri);
                                    //temporarily hold the value of the new package in a seperate var in order to check if the field will be to long
                                    string packagelink = $"[{PackageURL.Segments[PackageURL.Segments.Length - 1]}]({Package.Uri})";
                                    HttpRequestMessage httpRequest = new HttpRequestMessage();
                                    httpRequest.RequestUri = PackageURL;
                                    //httpRequest.Method = HttpMethod.Get;
                                    httpRequest.Method = HttpMethod.Head;
                                    httpRequest.Headers.Add("Connection", "Keep-Alive");
                                    httpRequest.Headers.Add("Accept", "*/*");
                                    //httpRequest.Headers.Add("Range", "bytes=0-1");
                                    httpRequest.Headers.Add("User-Agent", "Microsoft-Delivery-Optimization/10.0");
                                    HttpResponseMessage httpResponse = await _httpClient.SendAsync(httpRequest, new System.Threading.CancellationToken());
                                    HttpHeaders headers = httpResponse.Content.Headers;
                                    IEnumerable<string> values;
                                    if (headers.TryGetValues("Content-Length", out values))
                                    {
                                        string filesize = BytesToString(long.Parse(values.FirstOrDefault()));
                                        packagelink += $": {filesize}";
                                    }
                                    //check if the combined lengths of the package list and new package link will not exceed the maximum field length of 1024 characters
                                    if ((packagelink.Length + packagelist.Length) >= 1024)
                                    {
                                        //if the combined lengths of the package list and new package link exceed 1024 characters
                                        //subtract the number of free characters being taken up by the new field
                                        freechars -= packagelist.Length + 1;
                                        // if the embed will exceed the max number of characters allowed (6000)
                                        if (freechars <= 0)
                                        {
                                            //add embed to list
                                            discordEmbedBuilders.Add(productembedded);
                                            //reset and configure product embed 
                                            productembedded = new DiscordEmbedBuilder()
                                            {
                                                //configure colour of embed
                                                Color = DiscordColor.Gold,
                                                //add app info like name and logo to the footer of the embed
                                                Footer = new Discord​Embed​Builder.EmbedFooter() { Text = $"{dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle} - {dcat.ProductListing.Product.LocalizedProperties[0].PublisherName}", IconUrl = dcat.ProductListing.Product.LocalizedProperties[0].Images[0].Uri }
                                            };
                                            //reset number of characters free in embed
                                            freechars = freecharsmax - packagelink.Length - 1;
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
                            }
                        }


                    }
                    //check if the last field will not exceed the maximum number of characters per embed
                    if (freechars <= 0)
                    {
                        //add embed to list
                        discordEmbedBuilders.Add(productembedded);
                        //reset and configure product embed 
                        productembedded = new DiscordEmbedBuilder()
                        {
                            //configure colour of embed
                            Color = DiscordColor.Gold,
                            //add app info like name and logo to the footer of the embed
                            Footer = new Discord​Embed​Builder.EmbedFooter() { Text = $"{dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle} - {dcat.ProductListing.Product.LocalizedProperties[0].PublisherName}", IconUrl = dcat.ProductListing.Product.LocalizedProperties[0].Images[0].Uri }
                        };
                    }
                    //push the last field
                    if (!string.IsNullOrWhiteSpace(packagelist))
                    {
                        productembedded.AddField("‍", packagelist);
                    }
                    if (productembedded.Fields.Count == 0)
                    {
                        productembedded.Description = "No packages were found";
                    }
                }
                else
                {
                    productembedded.Description = "No packages were found";
                }
                //add embed to list
                discordEmbedBuilders.Add(productembedded);
                for (int i = 0; i < discordEmbedBuilders.Count; i++)
                {
                    discordEmbedBuilders[i].Title = $"Product ({i+1}/{discordEmbedBuilders.Count}):";
                }
                if (discordEmbedBuilders.Count > 0)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(discordEmbedBuilders[0].Build()));
                    discordEmbedBuilders.RemoveAt(0);
                }
                foreach (var discordEmbedBuilder in discordEmbedBuilders)
                {
                    await ctx.Channel.SendMessageAsync(discordEmbedBuilder.Build());
                }
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Product not found."));
            }

        }

        [SlashCommand("Query", "Query app info for a given product ID in a given locale and environment.")]
        public async Task QueryAsync(
           InteractionContext ctx,
            [Option("PackageID", "The package ID for a given app (you may provide this as a link to a store page)")]
            string ID,
            [Option("Locale", "Specify a locale Example: EN-US for United States English")]
            string localestring = "EN-US",
            [Option("Environment", "Environment to search (production by default)")]
            Enums.DCatEndpoint env = Enums.DCatEndpoint.Production)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            bool marketresult = Enum.TryParse(localestring.Split('-')[1], true, out Market market);
            bool langresult = Enum.TryParse(localestring.Split('-')[0].ToLower(), true, out Lang lang);
            if (!marketresult || !langresult)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Invalid Market or Lang specified. Example: US-EN for United States English, you provided Market {localestring.Split('-')[0]} and Language {localestring.Split('-')[1]}"));
                return;
            }
            //Push the input id through a Regex filter in order to take the onestoreid from the storepage url
            if (new Regex(@"[a-zA-Z0-9]{12}").Matches(ID).Count == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Invalid Product ID"));
                return;
            }
            DisplayCatalogHandler customizedhandler = new DisplayCatalogHandler((DCatEndpoint)env, new Locale(market, lang, true));
            //await customizedhandler.QueryDCATAsync(ID);
            if (Program.TokenDictionary.ContainsKey(ctx.User.Id))
            {
                await customizedhandler.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value, Program.TokenDictionary.GetValueOrDefault(ctx.User.Id));

            }
            else
            {
                await customizedhandler.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value);
            }
            if (customizedhandler.IsFound)
            {
                if (customizedhandler.ProductListing.Product != null) //One day ill fix the mess that is the StoreLib JSON, one day.
                {
                    customizedhandler.ProductListing.Products = new List<Product>();
                    customizedhandler.ProductListing.Products.Add(customizedhandler.ProductListing.Product);
                }
                var productembedded = new DiscordEmbedBuilder()
                {
                    Title = "App Info:",
                    Footer = new Discord​Embed​Builder.EmbedFooter() { Text = $"{customizedhandler.ProductListing.Product.LocalizedProperties[0].ProductTitle} - {customizedhandler.ProductListing.Product.LocalizedProperties[0].PublisherName}", IconUrl = customizedhandler.ProductListing.Product.LocalizedProperties[0].Images[0].Uri.Replace("//", "https://") },
                    Color = DiscordColor.Gold
                };
                if (customizedhandler.ProductListing.Product.LocalizedProperties[0].ProductDescription.Length < 1023)
                {
                    productembedded.AddField("Description:", customizedhandler.ProductListing.Product.LocalizedProperties[0].ProductDescription);

                }
                else
                {
                    productembedded.AddField("Description:", customizedhandler.ProductListing.Product.LocalizedProperties[0].ProductDescription.Substring(0, 1023));
                }
                productembedded.AddField("Rating:", $"{customizedhandler.ProductListing.Product.MarketProperties[0].UsageData[0].AverageRating} Stars");
                productembedded.AddField("Last Modified:", customizedhandler.ProductListing.Product.MarketProperties[0].OriginalReleaseDate.ToString());
                productembedded.AddField("Product Type:", customizedhandler.ProductListing.Product.ProductType);
                productembedded.AddField("Is a Microsoft Listing:", customizedhandler.ProductListing.Product.IsMicrosoftProduct.ToString());
                if (customizedhandler.ProductListing.Product.ValidationData != null)
                {
                    productembedded.AddField("Validation Info:", $"`{customizedhandler.ProductListing.Product.ValidationData.RevisionId}`");
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
                    if (customizedhandler.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.Packages[0].KeyId != null)
                    {
                        productembedded.AddField("EAppx Key ID:", customizedhandler.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.Packages[0].KeyId);
                    }
                    productembedded.AddField("WuCategoryID:", customizedhandler.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.FulfillmentData.WuCategoryId);
                }
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(productembedded.Build()));
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Product not found."));
            }

        }

        [SlashCommand("Search", "Enumerate content via search query")]
        public async Task SearchAsync(InteractionContext ctx, [Option("Query", "Query string")] string query, [Option("DeviceFamily", "Query string")] Enums.DeviceFamily deviceFamily = Enums.DeviceFamily.Xbox)
        {
            try
            {
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
                DisplayCatalogHandler dcat = DisplayCatalogHandler.ProductionConfig();
                DCatSearch results = await dcat.SearchDCATAsync(query, (StoreLib.Models.DeviceFamily)deviceFamily);
                var searchresultsembedded = new DiscordEmbedBuilder()
                {
                    Title = "Search results:",
                    Footer = new Discord​Embed​Builder.EmbedFooter() { Text = $"Result count: {results.TotalResultCount}, Device family: {deviceFamily}" },
                    Color = DiscordColor.Gold
                };

                foreach (Result res in results.Results)
                {
                    foreach (Product prod in res.Products)
                    {
                        searchresultsembedded.AddField($"{prod.Title} {prod.Type}", prod.ProductId);
                    }
                }

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(searchresultsembedded.Build()));
            }
            catch (Exception ex)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Exception while executing SearchAsync: {ex.Message}"));
            }
        }

        [SlashCommand("Convert", "Convert the provided id to other formats")]
        public async Task convertid(
            InteractionContext ctx, 
            [Option("ID", "Some form of ID for a given app (you may provide this as a link to a store page)")] 
            string ID, 
            [Choice("Content ID", "ContentID")]
            [Choice("Legacy Windows Phone Product ID", "LegacyWindowsPhoneProductID")]
            [Choice("Legacy Windows Store Product ID", "LegacyWindowsStoreProductID")]
            [Choice("Legacy Xbox Product ID", "LegacyXboxProductID")]
            [Choice("Package Family Name", "PackageFamilyName")]
            [Choice("Product ID", "ProductID")]
            [Choice("Xbox Title ID", "XboxTitleID")]
            [Option("IDtype", "The ID type for a given package (optional)")]
            string identifertype = "")
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            DisplayCatalogHandler dcat = DisplayCatalogHandler.ProductionConfig();
            IdentiferType IDType = IdentiferType.XboxTitleID;
            switch (identifertype)
            {
                case "":
                    if (new Regex(@"[a-zA-Z0-9]{12}").IsMatch(ID))
                    {
                        IDType = IdentiferType.ProductID;
                    }
                    else if (new Regex("[a-zA-z0-9]+[.]+[a-zA-z0-9]+[_]+[a-zA-z0-9]").IsMatch(ID))
                    {
                        IDType = IdentiferType.PackageFamilyName;
                    }
                    else if (new Regex(@"[0-9]{9}").IsMatch(ID))
                    {
                        IDType = IdentiferType.XboxTitleID;
                    }
                    break;
                case "ProductID":
                    IDType = IdentiferType.ProductID;
                    break;
                case "XboxTitleID":
                    IDType = IdentiferType.XboxTitleID;
                    break;
                case "PackageFamilyName":
                    IDType = IdentiferType.PackageFamilyName;
                    break;
                case "ContentID":
                    IDType = IdentiferType.ContentID;
                    break;
                case "LegacyWindowsPhoneProductID":
                    IDType = IdentiferType.LegacyWindowsPhoneProductID;
                    break;
                case "LegacyWindowsStoreProductID":
                    IDType = IdentiferType.LegacyWindowsStoreProductID;
                    break;
                case "LegacyXboxProductID":
                    IDType = IdentiferType.LegacyXboxProductID;
                    break;

            }
            await dcat.QueryDCATAsync(ID, IDType);
            if (dcat.IsFound)
            {
                if (dcat.ProductListing.Product != null) //One day ill fix the mess that is the StoreLib JSON, one day. Yeah mate just like how one day i'll learn how to fly
                {
                    dcat.ProductListing.Products = new List<Product>();
                    dcat.ProductListing.Products.Add(dcat.ProductListing.Product);
                }
                if (dcat.ProductListing.Products[0].LocalizedProperties[0].Images[0].Uri.StartsWith("//"))
                { //Some apps have a broken url, starting with a //, this removes that slash and replaces it with proper https.
                    dcat.ProductListing.Products[0].LocalizedProperties[0].Images[0].Uri = dcat.ProductListing.Products[0].LocalizedProperties[0].Images[0].Uri.Replace("//", "https://");
                }
                var productembedded = new DiscordEmbedBuilder()
                {
                    Title = "App Info:",
                    Footer = new Discord​Embed​Builder.EmbedFooter() { Text = $"{dcat.ProductListing.Products[0].LocalizedProperties[0].ProductTitle} - {dcat.ProductListing.Products[0].LocalizedProperties[0].PublisherName}", IconUrl = dcat.ProductListing.Products[0].LocalizedProperties[0].Images[0].Uri },
                    Color = DiscordColor.Gold
                };
                foreach (AlternateId PID in dcat.ProductListing.Products[0].AlternateIds) //Dynamicly add any other ID(s) that might be present rather than doing a ton of null checks.
                {
                    productembedded.AddField($"{PID.IdType}:", PID.Value);
                }
                productembedded.AddField($"ProductID:", dcat.ProductListing.Products[0].ProductId); //Add the product ID
                try
                {
                    productembedded.AddField($"PackageFamilyName:", dcat.ProductListing.Products[0].Properties.PackageFamilyName); //Add the package family name

                }
                catch (Exception ex) { Console.WriteLine(ex); };
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(productembedded.Build()));
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Product not found."));
            }
        }

    }
}
