using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
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
    public class StoreCommands : BaseCommandModule
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

        [Command("clean"), Description("Removes the last X messages sent by StoreBot, requires both the bot and the user to have the manage messages permission."), RequireUserPermissions(Permissions.ManageMessages)]
        public async Task CleanAsync(CommandContext cct, [Description("Number of bot messages to delete")] int numbertoclean)
        {
            int messagesdeleted = 0;
            var messages = await cct.Channel.GetMessagesAsync(100);
            foreach (var message in messages)
            {
                if (messagesdeleted >= numbertoclean)
                {
                    var finishedmessage = await cct.RespondAsync("Finished cleaning.");
                    await Task.Delay(3000);
                    await finishedmessage.DeleteAsync();
                    return;
                }
                else if (message.Author == cct.Client.CurrentUser || message.MentionedUsers.Contains(cct.Client.CurrentUser))
                {
                    await message.DeleteAsync();
                    messagesdeleted++;
                }
            }

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
        public async Task PackagesAsync(CommandContext cct, [Description("Specify a product ID or storepage, Example: 9wzdncrfj3tj or https://www.microsoft.com/en-gb/p/netflix/9wzdncrfj3tj")] string ID)
        {
            DisplayCatalogHandler dcat = DisplayCatalogHandler.ProductionConfig();
            //Push the input id through a Regex filter in order to take the onestoreid from the storepage url
            if (new Regex(@"[a-zA-Z0-9]{12}").Matches(ID).Count == 0)
            {
                return;
            }
            if (Program.TokenDictionary.ContainsKey(cct.User.Id))
            {
                await dcat.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value, Program.TokenDictionary.GetValueOrDefault(cct.User.Id));
            }
            else
            {
                await dcat.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value);
            }
            if (dcat.IsFound)
            {
                if (dcat.ProductListing.Product != null) //One day ill fix the mess that is the StoreLib JSON, one day.
                {
                    dcat.ProductListing.Products = new();
                    dcat.ProductListing.Products.Add(dcat.ProductListing.Product);
                }
                if (dcat.ProductListing.Products[0].LocalizedProperties[0].Images[0].Uri.StartsWith("//"))
                { //Some apps have a broken url, starting with a //, this removes that slash and replaces it with proper https.
                    dcat.ProductListing.Products[0].LocalizedProperties[0].Images[0].Uri = dcat.ProductListing.Products[0].LocalizedProperties[0].Images[0].Uri.Replace("//", "https://");
                }
                //configure product embed 
                var productembedded = new DiscordEmbedBuilder()
                {
                    //configure title of embed
                    Title = "Packages:",
                    //configure colour of embed
                    Color = DiscordColor.Gold,
                    //add app info like name and logo to the footer of the embed
                    Footer = new Discord​Embed​Builder.EmbedFooter() { Text = $"{dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle} - {dcat.ProductListing.Product.LocalizedProperties[0].PublisherName}", IconUrl = dcat.ProductListing.Product.LocalizedProperties[0].Images[0].Uri }
                };
                //set maximum number of free characters per embed
                int freecharsmax = 5988 - dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle.Length;
                freecharsmax -= dcat.ProductListing.Product.LocalizedProperties[0].PublisherName.Length;
                //set the number of free characters in the current embed
                int freechars = freecharsmax;
                //create empty package list string
                string packagelist = "";
                //start typing indicator
                await cct.TriggerTypingAsync();
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
                                //build product embed
                                productembedded.Build();
                                //send product embed
                                await cct.RespondAsync("", false, productembedded);
                                //reset fields of product embed
                                productembedded.RemoveFieldRange(0, productembedded.Fields.Count);
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
                                            //build product embed
                                            productembedded.Build();
                                            //send product embed
                                            await cct.RespondAsync("", false, productembedded);
                                            //reset fields of product embed
                                            productembedded.RemoveFieldRange(0, productembedded.Fields.Count);
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
                        //build product embed
                        productembedded.Build();
                        //send product embed
                        await cct.RespondAsync("", false, productembedded);
                        //reset fields of product embed
                        productembedded.RemoveFieldRange(0, productembedded.Fields.Count);
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

        [Command("advancedpackages"), Description("Queries FE3 for packages for the specified ID, Environment and Locale.")]
        public async Task AdvancedPackagesAsync(CommandContext cct, [Description("Specify a product ID, Example: 9wzdncrfj3tj")] string ID, [Description("Specify an Environment Example: Production for Production, Int for Instance")] string environment, [Description("Specify a locale Example: US-EN for United States English")] string localestring)
        {
            if (String.IsNullOrEmpty(ID) || String.IsNullOrEmpty(environment) || String.IsNullOrEmpty(localestring))
            {
                await cct.RespondAsync("Please supply all required arguments. Example: advancedpackages 9wzdncrfj3tj Int US-EN");
                return;
            }
            bool marketresult = Enum.TryParse(localestring.Split('-')[0], true, out Market market);
            bool langresult = Enum.TryParse(localestring.Split('-')[1].ToLower(), true, out Lang lang);
            if (!marketresult || !langresult)
            {
                await cct.RespondAsync($"Invalid Market or Lang specified. Example: US-EN for United States English, you provided Market {localestring.Split('-')[0]} and Language {localestring.Split('-')[1]}");
                return;
            }
            bool environmentresult = Enum.TryParse(environment, true, out DCatEndpoint env);
            if (!environmentresult)
            {
                await cct.RespondAsync($"Invalid Environment specified. Example: Production for Production, Int for Instance. You provided {environment}");
                return;
            }
            DisplayCatalogHandler dcat = new DisplayCatalogHandler(env, new Locale(market, lang, true));
            //Push the input id through a Regex filter in order to take the onestoreid from the storepage url
            //await dcat.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value);
            if (Program.TokenDictionary.ContainsKey(cct.User.Id))
            {
                await dcat.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value, Program.TokenDictionary.GetValueOrDefault(cct.User.Id));

            }
            else
            {
                await dcat.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value);
            }
            if (dcat.IsFound)
            {
                if (dcat.ProductListing.Product != null) //One day ill fix the mess that is the StoreLib JSON, one day.
                {
                    dcat.ProductListing.Products = new();
                    dcat.ProductListing.Products.Add(dcat.ProductListing.Product);
                }
                //configure product embed 
                var productembedded = new DiscordEmbedBuilder()
                {
                    //configure title of embed
                    Title = "Packages:",
                    //configure colour of embed
                    Color = DiscordColor.Gold,
                    //add app info like name and logo to the footer of the embed
                    Footer = new Discord​Embed​Builder.EmbedFooter() { Text = $"{dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle} - {dcat.ProductListing.Product.LocalizedProperties[0].PublisherName}", IconUrl = dcat.ProductListing.Product.LocalizedProperties[0].Images[0].Uri.Replace("//", "https://") }
                };
                //set maximum number of free characters per embed
                int freecharsmax = 5988 - dcat.ProductListing.Product.LocalizedProperties[0].ProductTitle.Length;
                freecharsmax -= dcat.ProductListing.Product.LocalizedProperties[0].PublisherName.Length;
                //set the number of free characters in the current embed
                int freechars = freecharsmax;
                //create empty package list string
                string packagelist = "";
                //start typing indicator
                await cct.TriggerTypingAsync();
                //get all packages for the product.
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
                            //build product embed
                            productembedded.Build();
                            //send product embed
                            await cct.RespondAsync("", false, productembedded);
                            //reset fields of product embed
                            productembedded.RemoveFieldRange(0, productembedded.Fields.Count);
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
                                        //build product embed
                                        productembedded.Build();
                                        //send product embed
                                        await cct.RespondAsync("", false, productembedded);
                                        //reset fields of product embed
                                        productembedded.RemoveFieldRange(0, productembedded.Fields.Count);
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
        public async Task AdvancedQueryAsync(CommandContext cct, [Description("Specify a product ID, Example: 9wzdncrfj3tj")] string ID, [Description("Specify an Environment Example: Production for Production, Int for Instance")] string environment, [Description("Specify a locale Example: US-EN for United States English")] string localestring)
        {
            //start typing indicator
            await cct.TriggerTypingAsync();
            if (String.IsNullOrEmpty(ID) || String.IsNullOrEmpty(environment) || String.IsNullOrEmpty(localestring))
            {
                await cct.RespondAsync("Please supply all required arguments. Example: advancedquery 9wzdncrfj3tj Int US-EN");
                return;
            }
            bool marketresult = Enum.TryParse(localestring.Split('-')[0], true, out Market market);
            bool langresult = Enum.TryParse(localestring.Split('-')[1].ToLower(), true, out Lang lang);
            if (!marketresult || !langresult)
            {
                await cct.RespondAsync($"Invalid Market or Lang specified. Example: US-EN for United States English, you provided Market {localestring.Split('-')[0]} and Language {localestring.Split('-')[1]}");
                return;
            }
            bool environmentresult = Enum.TryParse(environment, true, out DCatEndpoint env);
            if (!environmentresult)
            {
                await cct.RespondAsync($"Invalid Environment specified. Example: Production for Production, Int for Instance. You provided {environment}");
                return;
            }
            DisplayCatalogHandler customizedhandler = new DisplayCatalogHandler(env, new Locale(market, lang, true));
            //await customizedhandler.QueryDCATAsync(ID);
            if (Program.TokenDictionary.ContainsKey(cct.User.Id))
            {
                await customizedhandler.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value, Program.TokenDictionary.GetValueOrDefault(cct.User.Id));

            }
            else
            {
                await customizedhandler.QueryDCATAsync(new Regex(@"[a-zA-Z0-9]{12}").Matches(ID)[0].Value);
            }
            if (customizedhandler.IsFound)
            {
                if (customizedhandler.ProductListing.Product != null) //One day ill fix the mess that is the StoreLib JSON, one day.
                {
                    customizedhandler.ProductListing.Products = new();
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
                productembedded.Build();
                await cct.RespondAsync("", false, productembedded);
            }
            else
            {
                await cct.RespondAsync("Product not found.");
            }

        }

        [Command("search"), Description("Enumerate content via search query")]
        public async Task SearchAsync(CommandContext cct, [Description("Query string")] string query, [Description("Device family")] string deviceFamily)
        {
            if (String.IsNullOrEmpty(query) || String.IsNullOrEmpty(deviceFamily))
            {
                await cct.RespondAsync("Please supply all required arguments. Example: search \"Halo\" Xbox");
                return;
            }

            if (!Enum.TryParse(deviceFamily, true, out DeviceFamily deviceFamilyEnum))
            {
                await cct.RespondAsync($"Invalid DeviceFamily. Valid choices: [{string.Join(",", Enum.GetNames(typeof(DeviceFamily)))}] ... you provided DeviceFamily: {deviceFamily}.");
                return;
            }

            try
            {
                DisplayCatalogHandler dcat = DisplayCatalogHandler.ProductionConfig();
                DCatSearch results = await dcat.SearchDCATAsync(query, deviceFamilyEnum);
                var searchresultsembedded = new DiscordEmbedBuilder()
                {
                    Title = "Search results:",
                    Footer = new Discord​Embed​Builder.EmbedFooter() { Text = $"Result count: {results.TotalResultCount}, Device family: {deviceFamilyEnum}" },
                    Color = DiscordColor.Gold
                };

                foreach (Result res in results.Results)
                {
                    foreach (Product prod in res.Products)
                    {
                        searchresultsembedded.AddField($"{prod.Title} {prod.Type}", prod.ProductId);
                    }
                }

                searchresultsembedded.Build();
                await cct.RespondAsync("", false, searchresultsembedded);
            }
            catch (Exception ex)
            {
                await cct.RespondAsync($"Exception while executing SearchAsync: {ex.Message}");
            }
        }

        [Command("convert"), Description("Convert the provided id to other formats")]
        public async Task convertid(CommandContext cct, [Description("package ID")] string ID, [Description("Optionally set the identifer type, The options are:\nProductID, XboxTitleID, PackageFamilyName, ContentID, LegacyWindowsPhoneProductID, LegacyWindowsStoreProductID and LegacyXboxProductID")] string identifertype = "")
        {
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
                    dcat.ProductListing.Products = new();
                    dcat.ProductListing.Products.Add(dcat.ProductListing.Product);
                }
                //start typing indicator
                await cct.TriggerTypingAsync();
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
