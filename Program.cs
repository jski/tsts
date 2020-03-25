using System;
using System.Linq;
using Tweetinvi;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;

namespace TweetStreamToSlack
{

    public class Program
    {

        private static HttpClient httpClient = new HttpClient();
        private static string initString = "TweetStreamToSlack initializing at " + DateTime.UtcNow.ToString("HH:mm:ss") + " UTC";
        private static async void SayHello(Credentials creds)
        {

            dynamic jsonPayload = new JObject();
            jsonPayload.text = initString;
            jsonPayload.channel = creds.Channel;
            jsonPayload.username = "System-Startup";
            jsonPayload.icon_emoji = ":ghost:";

            var stringContent = new StringContent(JsonConvert.SerializeObject(jsonPayload), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await httpClient.PostAsync(creds.WebhookURL, stringContent);
            HttpContent responseContent = response.Content;
            using (var reader = new StreamReader(await responseContent.ReadAsStreamAsync()))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(await reader.ReadToEndAsync() + " - " + initString);
            }

        }
        private static void Main()
        {
            // Let's try literally try/catching the whole thing
            try
            {

                // Project requirements:
                // 1. TweetInvi library installed from NuGet - Install-Package TweetInvi
                // 2. Setup Twitter application to get a workable CONSUMER_KEY, CONSUMER_SECRET, ACCESS_TOKEN, ACCESS_TOKEN_SECRET (https://apps.twitter.com)
                // 3. Setup incoming Slack webhook to a channel in the group you want, to display the information you pass
                // 4. Collect credentials and pass them as environment variables; either using -e and docker run, or into the docker-compose file included.
                // That's it!

                Credentials creds = new Credentials();
                creds.ConsumerKey = Environment.GetEnvironmentVariable("ConsumerKey");
                creds.ConsumerSecret = Environment.GetEnvironmentVariable("ConsumerSecret");
                creds.AccessToken = Environment.GetEnvironmentVariable("AccessToken");
                creds.AccessTokenSecret = Environment.GetEnvironmentVariable("AccessTokenSecret");
                creds.Channel = Environment.GetEnvironmentVariable("Channel");
                creds.Username = Environment.GetEnvironmentVariable("Username");
                creds.EmojiIcon = Environment.GetEnvironmentVariable("EmojiIcon");
                creds.WebhookURL = Environment.GetEnvironmentVariable("WebhookURL");

                // Load credentials into Tweetinvi auth.
                Auth.SetUserCredentials(creds.ConsumerKey, creds.ConsumerSecret, creds.AccessToken, creds.AccessTokenSecret);
                var authenticatedUser = Tweetinvi.User.GetAuthenticatedUser();

                // Enable Automatic RateLimit handling; not too sure what this does to be honest, but is recommended by the Tweetinvi dev.
                RateLimit.RateLimitTrackerMode = RateLimitTrackerMode.TrackAndAwait;

                // Get list membership info
                var listOwner = Environment.GetEnvironmentVariable("ListOwner");
                var listToTrack = Environment.GetEnvironmentVariable("ListToTrack");
                var chosenList = TwitterList.GetExistingList(listToTrack, listOwner);
                var allUsersFromList = chosenList.GetMembers();

                // Attempting to make a stream of the users. Works, but gives tweets @ too.
                var tweetStream = Tweetinvi.Stream.CreateFilteredStream();
                foreach (var user in allUsersFromList)
                {
                    tweetStream.AddFollow(user);
                }

                // Initialize and say hello.
                SayHello(creds);

                // Write how many users you got to start with.
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(initString + "\n");
                Console.WriteLine("Using list: {0}", chosenList.FullName);
                Console.WriteLine("Have loaded {0} users into the stream...", tweetStream.FollowingUserIds.Count);

                // Start the stream; set a couple params.
                tweetStream.MatchOn = Tweetinvi.Streaming.MatchOn.Follower;
                tweetStream.StallWarnings = true;

                tweetStream.MatchingTweetReceived += async (succsender, succargs) =>
                {

                    // Something broke in the actual setting to match followers, so doublecheck here.
                    if (succargs.MatchOn == tweetStream.MatchOn)
                    {
                        string tweetText = (succargs.Tweet.Url + " - " + DateTime.UtcNow).ToString(); 

                        // Created and then serialize JSON payload, which Slack expects. Pass as StringContent to HttpReader.
                        dynamic jsonPayload = new JObject();
                        jsonPayload.text = tweetText;
                        jsonPayload.channel = creds.Channel;
                        jsonPayload.username = creds.Username;
                        jsonPayload.icon_emoji = creds.EmojiIcon;

                        var stringContent = new StringContent(JsonConvert.SerializeObject(jsonPayload), Encoding.UTF8, "application/json");

                        HttpResponseMessage response = await httpClient.PostAsync(creds.WebhookURL, stringContent);
                        HttpContent responseContent = response.Content;
                        using (var reader = new StreamReader(await responseContent.ReadAsStreamAsync()))
                        {
                            // Write the response output to console. Debug mode.
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(await reader.ReadToEndAsync() + " - " + DateTime.UtcNow + " - tweet from " + succargs.Tweet.CreatedBy.ScreenName + " matched and sent");
                        }

                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("no - " + DateTime.UtcNow + " - tweet from " + succargs.Tweet.CreatedBy.ScreenName + " rejected for not matching");
                    }

                };

                // Start stream if it's not running; attempt to get it to auto-restart
                do
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(DateTime.UtcNow + " - Starting stream...\n");
                    tweetStream.StartStreamMatchingAllConditions();
                    Console.WriteLine(DateTime.UtcNow + " - Stream stopped...\n");
                } while (tweetStream.StreamState != Tweetinvi.Models.StreamState.Running);

            }

            catch (Exception e)
            {
                Console.WriteLine("{0}", e);
                File.WriteAllText(@"log.txt", DateTime.UtcNow + e.ToString());
            }

            Console.WriteLine(DateTime.UtcNow + "Main function ending, even though this shouldn't happen!");

        }

    }

}
