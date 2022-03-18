using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib;
using TwitchLib.Api;
using TwitchLib.Api.Core.Models.Undocumented.Chatters;
using TwitchLib.Api.V5.Models.Subscriptions;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.PubSub;

namespace IggiBot4
{
    //Main bot file
    class TwitchBot
    {
        //TwitchLib
        internal TwitchClient client;
        internal static TwitchAPI api;
        //Bot features
        internal CoinSystem coinSystem;
        Commands commands;
        internal PersonalizedCommands personalizedCommands;
        //Uptime
        internal bool online;
        Timer onlineCheck;
        //Paths and files
        internal string credentialsPath;
        string userStatsPath;
        string backupPath;
        string quotePath;
        string logPath;
        string errorLogPath;
        TxtFile logFile;
        TxtFile errorLogFile;
        //Custom feature paths
        string krakenPath;
        string extraCoinsPath;
        string donationsPath;
        string dndPath;
        string commandPath;
        //Misc.
        bool closing;
        int restarts;
        DateTime lastUpdatedChatters;

        List<ChatterFormatted> chatters;

        public TwitchBot(string endUser)
        {
            closing = false;
            SetPaths(endUser);
            logFile = new TxtFile(logPath);
            errorLogFile = new TxtFile(errorLogPath);
            Credentials.ReadCredentials(credentialsPath);
            restarts = 0;
            lastUpdatedChatters = DateTime.Now;
            Connect();
            coinSystem = new CoinSystem(this, userStatsPath, backupPath);
            personalizedCommands = new PersonalizedCommands(this, krakenPath, extraCoinsPath, donationsPath, dndPath);
            commands = new Commands(this, quotePath, commandPath);
            onlineCheck = new Timer(async (x) =>
            {
                if((await GetUptime()).HasValue)
                {
                    if (!online)
                    {
                        personalizedCommands.NewSession();
                    }
                    else
                    {
                        await personalizedCommands.UpdateFiles();
                    }
                    online = true;
                }
                else
                {
                    if(online)
                    {
                        personalizedCommands.EndSession();
                        coinSystem.SaveStats();
                    }
                    online = false;
                }
            }, null, TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(5));
        }

        public void Close()
        {
            closing = true;
            Disconnect();
            coinSystem.Close();
            commands.Close();
        }

        private void Disconnect()
        {
            try
            {
                client.Disconnect();
            }
            catch(Exception e)
            {
                logFile.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}: {e.Message}", true);
            }
        }

        void SetPaths(string user)
        {
            switch(user)
            {
                case "rhykkerLinux":
                    {
                        credentialsPath = @"/home/iggnaccy/IggiBot4/IggiBot4/bin/Debug/Credentials.txt";
                        krakenPath = @"/home/dropbox/Dropbox/Bot/KrakenCounter.txt";
                        extraCoinsPath = @"/home/dropbox/Dropbox/Bot/ExtraCoins.txt";
                        donationsPath = @"/home/dropbox/Dropbox/Bot/session_donation_amount.txt";
                        dndPath = @"/home/iggnaccy/IggiBot4/IggiBot4/bin/Debug/dndRewards.csv";
                        logPath = @"/home/iggnaccy/IggiBot4/LOG.txt";
                        userStatsPath = @"/home/iggnaccy/IggiBot4/IggiBot4/bin/Debug/UserStats.ini";
                        backupPath = @"/home/iggnaccy/IggiBot4/Backup";
                        quotePath = @"/home/iggnaccy/IggiBot4/IggiBot4/bin/Debug/quotes.txt";
                        commandPath = @"/home/iggnaccy/IggiBot4/IggiBot4/bin/Debug/commands.txt";
                    }
                    break;
                case "rhykkerWindows":
                    {
                        credentialsPath = @"C:\Users\Iggnaccy\Documents\IggiBot4\IggiBot4\bin\Debug\Credentials.txt";
                        krakenPath = @"C:\Users\Iggnaccy\Dropbox\Bot\KrakenCounter.txt";
                        extraCoinsPath = @"C:\Users\Iggnaccy\Dropbox\Bot\ExtraCoins.txt";
                        donationsPath = @"C:\Users\Iggnaccy\Dropbox\Bot\session_donation_amount.txt";
                        dndPath = @"C:\Users\Iggnaccy\Documents\IggiBot4\IggiBot4\bin\Debug\dndRewards.csv";
                        logPath = @"C:\Users\Iggnaccy\Documents\IggiBot4\LOG.txt";
                        errorLogPath = @"C:\Users\Iggnaccy\Documents\IggiBot4\ErrorLog.txt";
                        userStatsPath = @"C:\Users\Iggnaccy\Documents\IggiBot4\IggiBot4\bin\Debug\UserStats.ini";
                        backupPath = @"C:\Users\Iggnaccy\Documents\IggiBot4\Backup";
                        quotePath = @"C:\Users\Iggnaccy\Documents\IggiBot4\IggiBot4\bin\Debug\quotes.txt";
                        commandPath = @"C:\Users\Iggnaccy\Documents\IggiBot4\IggiBot4\bin\Debug\commands.txt";
                    }
                    break;
                case "local":
                    {
                        credentialsPath = @"E:\Projekty bez Dropboxa\ugh\IggiBot4\IggiBot4\bin\Debug\Credentials.txt";
                        krakenPath = @"E:\Dropbox\Bot\KrakenCounter.txt";
                        extraCoinsPath = @"E:\Dropbox\Bot\ExtraCoins.txt";
                        donationsPath = @"E:\Dropbox\Bot\session_donation_amount.txt";
                        dndPath = @"E:\Projekty bez Dropboxa\ugh\IggiBot4\IggiBot4\bin\Debug\dndRewards.csv";
                        logPath = @"E:\Projekty bez Dropboxa\ugh\IggiBot4\LOG.txt";
                        errorLogPath = @"E:\Projekty bez Dropboxa\ugh\IggiBot4\ErrorLog.txt";
                        userStatsPath = @"E:\Projekty bez Dropboxa\ugh\IggiBot4\IggiBot4\bin\Debug\UserStats.ini";
                        backupPath = @"E:\Projekty bez Dropboxa\ugh\IggiBot4\Backup";
                        quotePath = @"E:\Projekty bez Dropboxa\ugh\IggiBot4\IggiBot4\bin\Debug\quotes.txt";
                        commandPath = @"E:\Projekty bez Dropboxa\ugh\IggiBot4\IggiBot4\bin\Debug\commands.txt";
                    }
                    break;
                default:
                    break;
            }
        }

        internal void SendWhisper(string username, string message)
        {
            client.SendWhisper(username, message);
        }

        internal void SendWhisperRaw(string username, string message)
        {
            client.SendRaw($"PRIVMSG #{Credentials.targetStream.ToLower()} :/w {username} {message}");
        }

        public void SendMessage(string message)
        {
            client.SendMessage(Credentials.targetStream, message);
        }

        public void SendMessageRaw(string message)
        {
            client.SendRaw($"PRIVMSG #{Credentials.targetStream.ToLower()} :{message}");
        }

        public async Task<List<ChatterFormatted>> GetChatList()
        {
            if (DateTime.Now.Subtract(lastUpdatedChatters) > TimeSpan.FromSeconds(30))
            {
                try
                {
                    chatters = await api.Undocumented.GetChattersAsync(Credentials.targetStream);
                    return chatters;
                }
                catch (Exception ex)
                {
                    LogError("GetChattersAsync", ex.Message);
                    return chatters;
                }
            }
            else
            {
                return chatters;
            }
        }

        public async Task<Subscription> GetUserSubscription(string username)
        {
            try
            {
                var x = await api.V5.Channels.CheckChannelSubscriptionByUserAsync(await GetUserID(Credentials.targetStream), await GetUserID(username), authToken: Credentials.authToken);
                if(x != null)
                {
                    return x;
                }
                return null;
            }
            catch
            {
                //LogError("CheckChannelSubscribtionByUserAsync", ex.Message);
                return null;
            }
        }

        public async Task<string> GetUserID(string username)
        {
            try
            {
                var userListTask = await api.V5.Users.GetUserByNameAsync(username);
                var userList = userListTask.Matches;
                if (userList == null || userList.Length == 0)
                    return null;
                //Console.WriteLine($"{DateTime.Now.ToLocalTime()}: Found {username} ID: {userList[0].Id}");
                return userList[0].Id;
            }
            catch (Exception ex)
            {
                LogError("GetUserByNameAsync", ex.Message);
                return null;
            }
        }

        internal async Task<TimeSpan?> GetUptime()
        {
            string userId;
            userId = await GetUserID(Credentials.targetStream);
            if (userId == null)
                return null;
            try
            {
                return await api.V5.Streams.GetUptimeAsync(userId);
            }
            catch (Exception ex)
            {
                LogError("GetUptimeAsync", ex.Message);
                return null;
            }
        }

        internal void Connect(bool refreshing = false)
        {
            ConnectionCredentials cc = new ConnectionCredentials(Credentials.botUsername, Credentials.botToken);
            if(api == null)
            {
                api = new TwitchAPI();
                api.Settings.ClientId = Credentials.clientId;
                api.Settings.AccessToken = Credentials.authToken;
            }
            if(refreshing)
            {
                api.Settings.AccessToken = Credentials.authToken;
            }
            client = new TwitchClient();
            client.Initialize(cc, channel: Credentials.targetStream);

            client.OnLog += OnLog;
            client.OnIncorrectLogin += Client_OnIncorrectLogin;

            client.OnDisconnected += Client_OnDisconnected;

            if(refreshing)
            {
                RefreshEvents();
            }

            client.Connect();
        }

        public async Task<List<Subscription>> GetSubscriptions()
        {
            var list = await api.V5.Channels.GetAllSubscribersAsync(await GetUserID(Credentials.targetStream), Credentials.authToken);
            return list;
        }

        void RefreshEvents()
        {
            coinSystem.RefreshEvents(this);
            personalizedCommands.RefreshEvents(this);
            commands.RefreshEvents(this);
        }

        private void Client_OnIncorrectLogin(object sender, TwitchLib.Client.Events.OnIncorrectLoginArgs e)
        {
            var botTokens = api.V5.Auth.RefreshAuthTokenAsync(Credentials.botRefreshToken, "0osemzvzybmaunf9tvz78iag3sfhlw", api.Settings.ClientId).GetAwaiter().GetResult();
            var authTokens = api.V5.Auth.RefreshAuthTokenAsync(Credentials.authRefreshToken, "0osemzvzybmaunf9tvz78iag3sfhlw", api.Settings.ClientId).GetAwaiter().GetResult();
            Credentials.UpdateTokens(credentialsPath, botTokens.AccessToken, authTokens.AccessToken, botTokens.RefreshToken, authTokens.RefreshToken);
            /*closing = true;
            client.Disconnect();
            Connect(true);
            closing = false;
            */
            Restart(true);
        }

        private void Client_OnDisconnected(object sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
        {
            if(!closing)
            {
                Restart();
            }
        }

        void Restart(bool refreshing = false)
        {
            closing = true;
            Disconnect();
            restarts++;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            while(sw.Elapsed.TotalSeconds < 2) { }
            sw.Stop();
            Log($"Restarting for the {AddOrdinal(restarts)} time.");
            Connect(refreshing);
            closing = false;
        }

        void OnLog(object sender, TwitchLib.Client.Events.OnLogArgs args)
        {
            if (args.Data.Contains("PING") || args.Data.Contains("PONG") || args.Data.Contains("JOIN") || args.Data.Contains("PART") || args.Data.Contains("USERSTATE")) return;
            if (args.Data.Contains("Writing") || !args.Data.Contains("PRIVMSG"))
            {
                Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}: {args.Data}");
            }
            if (args.Data.Contains("Disconnect") && !closing)
            {
                Restart();
            }
        }

        internal void Log(string message)
        {
            logFile.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}: {message}", append: true);
        }

        internal void LogError(string source, string message)
        {
            errorLogFile.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}: {source} -> {message}", true);
        }

        #region PasteBin
        static readonly HttpClient pasteBinClient = new HttpClient();
        public async Task<string> PasteOnPasteBin(string paste, string title)
        {
            var query = new Dictionary<string, string>
                { { "api_option", "paste" }, // paste to pastebin :D
                    { "api_user_key", "" }, // puste = guest, w innym razie użytkownika
                    { "api_paste_private", "1" }, // 0=public 1=unlisted 2=private
                    { "api_paste_name", title }, // name or title of your paste
                    { "api_paste_expire_date", "1H" }, // https://pastebin.com/api#6
                    { "api_paste_format", "text" }, // https://pastebin.com/api#5
                    { "api_dev_key", "dfafd4e4fd7777bafc8c72d3f9ba232d" }, // twój api_developer_key
                };
            query.Add("api_paste_code", paste);
            var content = new FormUrlEncodedContent(query);
            var task = await pasteBinClient.PostAsync("https://pastebin.com/api/api_post.php", content);
            var response = await task.Content.ReadAsStringAsync();
            return response;
        }
        #endregion

        public static string AddOrdinal(int num)
        {
            if (num <= 0) return num.ToString();

            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }

            switch (num % 10)
            {
                case 1:
                    return num + "st";
                case 2:
                    return num + "nd";
                case 3:
                    return num + "rd";
                default:
                    return num + "th";
            }

        }
    }
}
