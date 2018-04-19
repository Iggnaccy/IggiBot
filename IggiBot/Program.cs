using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TwitchLib;
using TwitchLib.Api;
using TwitchLib.Api.Models.Undocumented.Chatters;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.PubSub;

namespace IggiBot
{
    class Program
    {
        static void Main (string[] args)
        {
            TwitchBot bot = new TwitchBot ();
            bot.Initialization ();
            bot.Connect ();
            Console.ReadLine ();
            bot.Close ();
            Console.ReadLine ();
        }
    }

    class TwitchBot
    {
        #region Variables
        #region TwitchVariables
        private static TwitchAPI api;
        private TwitchClient client;
        private TwitchPubSub pubsub;
        #endregion
        #region ExtraCoinsVariables
        private List<string> coinCheck;
        float bits;
        float donations;
        float lastDonations;
        float subs;
        float reSubs;
        int leftoverMinutes;
        bool update;
        bool krakenNight;
        bool patreonStream;
        bool readBits;
        DateTime startTime;
        int missedCoins;
        int missedMinutes;
        List<ChatterFormatted> chatters;
        #endregion
        #region KrakenVariables
        float totalKrakens;
        float nextKraken;
        #endregion
        #region CacheFiles
        TxtFile stats;
        TxtFile bonusMinutesCache;
        TxtFile customCommandsCache;
        TxtFile giveawaysCache;
        TxtFile subCache;
        #endregion
        #region InputFiles
        TxtFile donation;
        TxtFile bitsFile;
        #endregion
        #region OutputFiles
        TxtFile krakenFile;
        TxtFile extraCoins;
        TxtFile endTime;
        #endregion
        #region Timers
        Timer auctionTimer;
        Timer coinCheckTimer;
        Timer loyaltyTimer;
        Timer timeTimer;
        int saveCounter;
        bool online;
        #endregion
        #region Dictionaries
        private Dictionary<string, UserStats> userStats;
        private Dictionary<string, Command> commands;
        private Dictionary<string, WhisperCommand> whisperCommands;
        private Dictionary<string, CustomCommand> customCommands;
        #endregion
        #region AuctionVariables
        Queue<string> giveawayDesc;
        Queue<int> giveawayMult;
        Queue<string> giveawayHouse;
        bool auctionOpen;
        string auctionInfo;
        string auctionWinner;
        int auctionTime;
        int auctionLeft;
        int auctionMinimalIncrement;
        int auctionBid;
        #endregion
        #endregion
        #region Start+End
        void FileInitialization ()
        {
            // Initialization and saving files:
            Credentials.Initialize ($@"{Environment.CurrentDirectory}/Credentials.txt");
            stats = new TxtFile ($@"{Environment.CurrentDirectory}/UserStats.ini");
            customCommandsCache = new TxtFile ($@"{Environment.CurrentDirectory}/commands.txt");
            bonusMinutesCache = new TxtFile ($@"{Environment.CurrentDirectory}/leftoverMinutes.txt");
            giveawaysCache = new TxtFile ($@"{Environment.CurrentDirectory}/giveaways.txt");
            subCache = new TxtFile ($@"{Environment.CurrentDirectory}/sub_resub_cache.txt");
            // Streamlabs integration files:
            donation = new TxtFile ($@"{Environment.CurrentDirectory}/session_donation_amount.txt");
            bitsFile = new TxtFile ($@"{Environment.CurrentDirectory}/session_cheer_amount.txt");
            // Bot output files:
            krakenFile = new TxtFile ($@"{Environment.CurrentDirectory}/KrakenCounter.txt");
            extraCoins = new TxtFile ($@"{Environment.CurrentDirectory}/ExtraCoins.txt");
            endTime = new TxtFile ($@"{Environment.CurrentDirectory}/EndTime.txt");
        }
        void VariableInitialization ()
        {
            userStats = new Dictionary<string, UserStats> ();
            commands = new Dictionary<string, Command> ();
            whisperCommands = new Dictionary<string, WhisperCommand> ();
            customCommands = new Dictionary<string, CustomCommand> ();
            coinCheck = new List<string> ();
            giveawayDesc = new Queue<string> ();
            giveawayHouse = new Queue<string> ();
            giveawayMult = new Queue<int> ();
            auctionTimer = new Timer ();
            coinCheckTimer = new Timer ();
            loyaltyTimer = new Timer ();
            timeTimer = new Timer ();
            chatters = new List<ChatterFormatted> ();
            auctionTimer.Interval = 1000;
            coinCheckTimer.Interval = 4000;
            loyaltyTimer.Interval = 5 * 60 * 1000;
            timeTimer.Interval = 60000;
            auctionTimer.Elapsed += AuctionTimer_Elapsed;
            coinCheckTimer.Elapsed += CoinCheckTimer_Elapsed;
            loyaltyTimer.Elapsed += LoyaltyTimer_Elapsed;
            timeTimer.Elapsed += TimeTimer_Elapsed;
            auctionTimer.Start ();
            coinCheckTimer.Start ();
            loyaltyTimer.Start ();
            timeTimer.Start ();
            bits = donations = lastDonations = subs = reSubs = saveCounter = missedCoins = missedMinutes = 0;
            totalKrakens = 0.00f;
            nextKraken = 4;
            update = readBits = false;
        }
        public void Initialization ()
        {
            FileInitialization ();
            VariableInitialization ();

            LoadFiles ();
            DefineBaseCommands ();

            if (GetUptime ().HasValue)
            {
                online = true;
                NewSession ();
            }
            else
            {
                online = false;
            }
        }
        public void Close ()
        {
            Disconnect ();
            loyaltyTimer.Stop ();
            timeTimer.Stop ();
            coinCheckTimer.Stop ();
            auctionTimer.Stop ();
            SaveCache ();
        }
        #endregion

        #region Connection
        public void Connect ()
        {
            ConnectionCredentials cc = new ConnectionCredentials (Credentials.BotUsername, Credentials.BotToken);
            if (api == null)
            {
                api = new TwitchAPI ();
                var x = api.InitializeAsync (Credentials.ClientID, Credentials.AuthToken);
                x.Wait ();
            }

            client = new TwitchClient ();
            client.Initialize (cc, channel : Credentials.Destination);
            client.AddChatCommandIdentifier ('!');
            client.AddWhisperCommandIdentifier ('!');
            client.AutoReListenOnException = true;
            client.DisableAutoPong = false;

            client.ChatThrottler = new TwitchLib.Client.Services.MessageThrottler (client, 100, TimeSpan.FromSeconds (30), maximumMessageLengthAllowed : 500, applyThrottlingToRawMessages : true);
            client.WhisperThrottler = new TwitchLib.Client.Services.MessageThrottler (client, 3, TimeSpan.FromSeconds (1), maximumMessageLengthAllowed : 500);

            client.OnSendReceiveData += Client_OnSendReceiveData;

            client.OnGiftedSubscription += Client_OnGiftedSubscription;
            client.OnNewSubscriber += Client_OnNewSubscriber;
            client.OnReSubscriber += Client_OnReSubscriber;

            client.OnChatCommandReceived += Client_OnChatCommandReceived;
            client.OnWhisperCommandReceived += Client_OnWhisperCommandReceived;

            client.OnConnected += Client_OnConnected;
            client.OnDisconnected += Client_OnDisconnected;

            client.OnConnectionError += Client_OnConnectionError;
            client.OnIncorrectLogin += Client_OnIncorrectLogin;

            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnJoinedChannel += Client_OnJoinedChannel;

            client.Connect ();

            pubsub = new TwitchPubSub ();
            pubsub.ListenToVideoPlayback (Credentials.Destination.ToLower ());
            pubsub.OnPubSubServiceConnected += Pubsub_OnPubSubServiceConnected;
            pubsub.OnPubSubServiceError += Pubsub_OnPubSubServiceError;
            pubsub.OnStreamUp += Pubsub_OnStreamUp;
            pubsub.OnStreamDown += Pubsub_OnStreamDown;
            pubsub.OnListenResponse += Pubsub_OnListenResponse;
            pubsub.Connect ();
        }

        void Disconnect ()
        {
            client.Disconnect ();
            pubsub.Disconnect ();
        }
        #endregion

        #region PubSubEvents
        private void Pubsub_OnPubSubServiceError (object sender, TwitchLib.PubSub.Events.OnPubSubServiceErrorArgs e) => Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: PubSubServiceError: {e.Exception.Message}");
        private void Pubsub_OnStreamDown (object sender, TwitchLib.PubSub.Events.OnStreamDownArgs e)
        {
            Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: Automatically ending session");
            online = false;
            EndSession ();
        }
        private void Pubsub_OnStreamUp (object sender, TwitchLib.PubSub.Events.OnStreamUpArgs e)
        {
            online = true;
            NewSession ();
            string kraken = krakenNight ? "Kraken night" : (patreonStream ? "Patreon stream" : "Normal stream");
            Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: Automatically starting a new session ({kraken})");
        }
        private void Pubsub_OnPubSubServiceConnected (object sender, EventArgs e) => pubsub.SendTopics ();
        private void Pubsub_OnListenResponse (object sender, TwitchLib.PubSub.Events.OnListenResponseArgs e)
        {
            string successful = e.Successful ? "Successful" : "Unsuccessful";
            Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: PubSub: {e.Topic}: {successful}");
        }
        #endregion
        #region ClientEvents
        private void Client_OnSendReceiveData (object sender, OnSendReceiveDataArgs e)
        {
            
        }

        private void Client_OnJoinedChannel (object sender, OnJoinedChannelArgs e) => Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: Joined Channel");

        private void Client_OnDisconnected (object sender, OnDisconnectedArgs e) => Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: Disconnected");

        private void Client_OnConnected (object sender, OnConnectedArgs e) => Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: Connected");

        private void Client_OnBeingHosted (object sender, OnBeingHostedArgs e) => SendMessage ($"Let's welcome {e.Channel}'s viewers! rhyRaid");

        private void Client_OnIncorrectLogin (object sender, OnIncorrectLoginArgs e) => Console.WriteLine ($"{DateTime.Now.ToLocalTime()} (ERROR): {e.Exception.Message}");

        private void Client_OnConnectionError (object sender, OnConnectionErrorArgs e) => Console.WriteLine ($"{DateTime.Now.ToLocalTime()} (ERROR): {e.Error.Message}");

        private void Client_OnWhisperCommandReceived (object sender, OnWhisperCommandReceivedArgs e)
        {
            if (whisperCommands.TryGetValue (e.Command, out WhisperCommand wc))
            {
                wc.FireCommand (e);
            }
        }

        private void Client_OnChatCommandReceived (object sender, OnChatCommandReceivedArgs e)
        {
            if (commands.TryGetValue (e.Command.CommandText.ToLower (), out Command c))
            {
                c.FireCommand (e);
                //Console.WriteLine($"Fired {e.Command.CommandText} command");
            }
            else
            {
                if (customCommands.TryGetValue (e.Command.CommandText.ToLower (), out CustomCommand cc))
                {
                    cc.FireCommand (e);
                    //Console.WriteLine($"Fired {e.Command.CommandText} command");
                }
            }
        }

        private void Client_OnReSubscriber (object sender, OnReSubscriberArgs e)
        {
            string desc = $"{e.ReSubscriber.DisplayName}: {e.ReSubscriber.Months} months, ";
            giveawayHouse.Enqueue(e.ReSubscriber.DisplayName.ToLower());
            if (e.ReSubscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Prime || e.ReSubscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier1)
            {
                desc += "tier 1";
                giveawayDesc.Enqueue (desc);
                giveawayMult.Enqueue (e.ReSubscriber.Months);
                reSubs += 5;
            }
            else if (e.ReSubscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier2)
            {
                desc += "tier 2";
                giveawayDesc.Enqueue (desc);
                giveawayMult.Enqueue (e.ReSubscriber.Months * 2);
                reSubs += 10;
            }
            else if (e.ReSubscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier3)
            {
                desc += "tier 3";
                giveawayDesc.Enqueue (desc);
                giveawayMult.Enqueue (e.ReSubscriber.Months * 6);
                reSubs += 25;
            }
            Console.WriteLine($"{DateTime.Now}: Resub: {desc}");
            update = true;
            UpdateOutputFiles ();
        }

        private void Client_OnNewSubscriber (object sender, OnNewSubscriberArgs e)
        {
            string desc = $"{e.Subscriber.DisplayName}: new, ";
            giveawayHouse.Enqueue(e.Subscriber.DisplayName.ToLower());
            if (e.Subscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Prime || e.Subscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier1)
            {
                desc += "tier 1";
                giveawayDesc.Enqueue (desc);
                giveawayMult.Enqueue (1);
                subs += 5;
            }
            else if (e.Subscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier2)
            {
                desc += "tier 2";
                giveawayDesc.Enqueue (desc);
                giveawayMult.Enqueue (2);
                subs += 10;
            }
            else if (e.Subscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier3)
            {
                desc += "tier 3";
                giveawayDesc.Enqueue (desc);
                giveawayMult.Enqueue (6);
                subs += 25;
            }
            Console.WriteLine($"{DateTime.Now}: New sub: {desc}");
            update = true;
            UpdateOutputFiles ();
        }

        private void Client_OnGiftedSubscription (object sender, OnGiftedSubscriptionArgs e)
        {
            string desc = e.GiftedSubscription.MsgParamMonths == "0" ? $"{e.GiftedSubscription.MsgParamRecipientDisplayName}: new, " : $"{e.GiftedSubscription.MsgParamRecipientDisplayName}: {e.GiftedSubscription.MsgParamMonths} months, ";
            if (e.GiftedSubscription.MsgParamSubPlan == "1000")
            {
                desc += "tier 1";
                giveawayHouse.Enqueue (e.GiftedSubscription.DisplayName.ToLower());
                giveawayMult.Enqueue (int.Parse (e.GiftedSubscription.MsgParamMonths) == 0 ? 1 : int.Parse(e.GiftedSubscription.MsgParamMonths));
                if (int.Parse (e.GiftedSubscription.MsgParamMonths) == 1 || int.Parse (e.GiftedSubscription.MsgParamMonths) == 0)
                {
                    subs += 5;
                }
                else
                {
                    reSubs += 5;
                }
            }
            else if (e.GiftedSubscription.MsgParamSubPlan == "2000")
            {
                desc += "tier 2";
                giveawayHouse.Enqueue (e.GiftedSubscription.DisplayName.ToLower ());
                giveawayMult.Enqueue (int.Parse (e.GiftedSubscription.MsgParamMonths) == 0 ? 1 : int.Parse(e.GiftedSubscription.MsgParamMonths) * 2);
                if (int.Parse (e.GiftedSubscription.MsgParamMonths) == 1 || int.Parse (e.GiftedSubscription.MsgParamMonths) == 0)
                {
                    subs += 10;
                }
                else
                {
                    reSubs += 10;
                }
            }
            else if (e.GiftedSubscription.MsgParamSubPlan == "3000")
            {
                desc += "tier 3";
                giveawayHouse.Enqueue (e.GiftedSubscription.DisplayName.ToLower ());
                giveawayMult.Enqueue (int.Parse (e.GiftedSubscription.MsgParamMonths) == 0 ? 1 : int.Parse(e.GiftedSubscription.MsgParamMonths) * 6);
                if (int.Parse (e.GiftedSubscription.MsgParamMonths) == 1 || int.Parse (e.GiftedSubscription.MsgParamMonths) == 0)
                {
                    subs += 25;
                }
                else
                {
                    reSubs += 25;
                }
            }
            desc += $", gifted by {e.GiftedSubscription.DisplayName}";
            Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: Gifted sub: {desc}");
            giveawayDesc.Enqueue (desc);
            update = true;
            UpdateOutputFiles ();
        }

        private void Client_OnMessageReceived (object sender, OnMessageReceivedArgs e)
        {
            if (e.ChatMessage.Bits > 0)
            {
                bits += e.ChatMessage.Bits / 100.0f;
                Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: {e.ChatMessage.Username} cheered {e.ChatMessage.Bits}");
                update = true;
                UpdateOutputFiles ();
            }
        }
        #endregion
        #region TimerTicks
        private void TimeTimer_Elapsed (object sender, ElapsedEventArgs e)
        {
            if (!online) 
            {
                return;
            }
            List<ChatterFormatted> users;
            try
            {
                var usersTask = api.Undocumented.GetChattersAsync (Credentials.Destination.ToLower ());
                usersTask.Wait ();
                users = usersTask.Result;
                chatters = users;
            }
            catch
            {
                users = chatters;
            }
            Stopwatch sw = new Stopwatch ();
            sw.Start ();
            foreach (var user in users)
            {
                AddTime (user.Username.ToLower (), 1);
            }
            sw.Stop ();
        }
        private void LoyaltyTimer_Elapsed (object sender, ElapsedEventArgs e)
        {
            if (!online) 
            {
                return;
            }
            List<ChatterFormatted> users;
            try
            {
                var usersTask = api.Undocumented.GetChattersAsync (Credentials.Destination.ToLower ());
                usersTask.Wait ();
                users = usersTask.Result;
                chatters = users;
            }
            catch
            {
                users = chatters;
            }
            var destID = GetUserID (Credentials.Destination);
            foreach (var user in users)
            {
                try
                {
                    var subTask = api.Channels.v5.CheckChannelSubscriptionByUserAsync (destID, GetUserID (user.Username), Credentials.AuthToken);
                    subTask.Wait ();
                    var sub = subTask.Result;
                    if (sub.SubPlan == "1000" || sub.SubPlan == "Prime")
                    {
                        AddCoins (user.Username.ToLower (), 2);
                    }
                    else if (sub.SubPlan == "2000")
                    {
                        AddCoins (user.Username.ToLower (), 3);
                    }
                    else if (sub.SubPlan == "3000")
                    {
                        AddCoins (user.Username.ToLower (), 6);
                    }
                }
                catch
                {
                    AddCoins (user.Username.ToLower (), 1);
                }
            }
            saveCounter++;
            if (saveCounter >= 5)
            {
                saveCounter = 0;
                SaveCache ();
            }
        }
        private void CoinCheckTimer_Elapsed (object sender, ElapsedEventArgs e)
        {
            float newDonations = float.Parse (donation.ReadLine ().Substring (1));
            if (newDonations == 0.0f)
            {
                update = true;
            }
            if (update)
            {
                if (newDonations > lastDonations || readBits)
                {
                    lastDonations = newDonations;
                    UpdateOutputFiles ();
                }
            }
            string msg = "";
            while (coinCheck.Count != 0)
            {
                string add = $"@{coinCheck[0]} has {GetCoins(coinCheck[0])} coin ; ";
                if ((msg + add).Length >= 500)
                {
                    SendMessage (msg);
                    return;
                }
                msg += add;
                coinCheck.RemoveAt (0);
            }
            if (msg != "")
            {
                SendMessage (msg);
            }
        }
        private void AuctionTimer_Elapsed (object sender, ElapsedEventArgs e)
        {
            if (auctionOpen == false) return;
            string tempWinner = auctionWinner;
            int tempBid = auctionBid;
            auctionLeft--;
            if (auctionTime / 2 == auctionLeft && auctionTime >= 120)
            {
                SendMessage ($"The auction is at it's half-way spot! Current winner is {tempWinner} with a bid of {tempBid} coin ! We are auctioning for {auctionInfo}");
                return;
            }
            if (auctionLeft == 30)
            {
                SendMessage ($"The auction will end in 30 seconds! Current winner is {tempWinner} with a bid of {tempBid} coin ! We are auctioning for {auctionInfo}");
                return;
            }
            if (auctionLeft == 0)
            {
                SendMessage ($"@{Credentials.Destination} The auction has closed! The winner of {auctionInfo} is {tempWinner} with a bid of {tempBid}.");
                AddCoins (tempWinner, -tempBid);
                auctionOpen = false;
                auctionInfo = auctionWinner = "";
                auctionBid = auctionLeft = auctionTime = auctionMinimalIncrement = 0;
            }
        }
        #endregion
        #region SupportingMethods
        void DefineBaseCommands ()
        {
            Command auction = new Command (args =>
            {
                if (args.Command.ChatMessage.IsBroadcaster == false || args.Command.ArgumentsAsList.Count == 0)
                {
                    if (auctionOpen && auctionInfo.Length > 0)
                    {
                        SendMessage ($"@{args.Command.ChatMessage.Username} We are auctioning for {auctionInfo}");
                    }
                }
                else
                {
                    var list = args.Command.ArgumentsAsList;
                    int id = 0;
                    if (list[id].ToLower () == "close")
                    {
                        id++;
                        if (auctionOpen)
                        {
                            auctionOpen = false;
                            SendMessage ("The auction was cancelled.");
                            auctionWinner = auctionInfo = "";
                            auctionLeft = auctionTime = auctionBid = 0;
                        }
                        return;
                    }
                    else if (list[id].ToLower () == "open")
                    {
                        id++;
                    }
                    if (auctionOpen)
                    {
                        SendMessage ($"@{args.Command.ChatMessage.Username} there is already an auction running");
                        return;
                    }
                    if (int.TryParse (list[id++], out int time))
                    {
                        auctionLeft = auctionTime = time * 60;
                        auctionWinner = "";
                        auctionBid = 0;
                        auctionInfo = "";
                        if (int.TryParse (list[id], out int incr))
                        {
                            auctionMinimalIncrement = incr;
                            id++;
                        }
                        else
                        {
                            auctionMinimalIncrement = 1;
                        }
                        while (id < list.Count)
                        {
                            auctionInfo += list[id++] + " ";
                        }
                        auctionOpen = true;
                        if (auctionInfo == "")
                        {
                            SendMessage ($"The auction is now open and will last {time} minutes! The minimal increments are {auctionMinimalIncrement} coin");
                        }
                        else
                        {
                            SendMessage ($"The auction is now open and will last {time} minutes! We are auctioning for {auctionInfo}. The minimal increments are {auctionMinimalIncrement} coin");
                        }
                    }
                    else
                    {
                        SendMessage ($"@{args.Command.ChatMessage.Username} usage: !auction [open/close] [time in minutes] [minimal increment] [description]");
                    }
                }
            });
            Command bid = new Command (args =>
            {
                if (auctionOpen == false)
                {
                    SendMessage ($"@{args.Command.ChatMessage.Username} there is no auction running");
                    return;
                }
                List<string> list = args.Command.ArgumentsAsList;
                if (int.TryParse (list[0], out int value))
                {
                    if (value > GetCoins (args.Command.ChatMessage.Username))
                    {
                        SendMessage ($"@{args.Command.ChatMessage.Username} you don't have that many coins");
                        return;
                    }
                    if (auctionWinner == args.Command.ChatMessage.Username)
                    {
                        SendMessage ($"@{args.Command.ChatMessage.Username} you can't outbid yourself BrokeBack");
                        return;
                    }
                    if (value < auctionBid + auctionMinimalIncrement)
                    {
                        SendMessage ($"@{args.Command.ChatMessage.Username} the top bid is {auctionBid}. Minimal increment is {auctionMinimalIncrement}");
                        return;
                    }
                    auctionWinner = args.Command.ChatMessage.Username;
                    auctionBid = value;
                    return;
                }
                else
                {
                    SendMessage ($"@{args.Command.ChatMessage.Username} usage: !bid amount");
                    return;
                }
            });
            Command coins = new Command (args =>
            {
                List<string> list = args.Command.ArgumentsAsList;
                if (list.Count == 0)
                {
                    coinCheck.Add (args.Command.ChatMessage.Username.ToLower ());
                    return;
                }
                if (args.Command.ChatMessage.IsBroadcaster == false || list[0] != "add")
                {
                    if (list[0][0] == '@')
                        coinCheck.Add (list[0].Substring (1).ToLower ());
                    else coinCheck.Add (list[0].ToLower ());
                    return;
                }
                if (list.Count < 3)
                {
                    SendMessage ($"@{args.Command.ChatMessage.Username} usage: !coins add [username/'all'] [value]");
                    return;
                }
                if (int.TryParse (list[2], out int value))
                {
                    if (list[1] == "all")
                    {
                        Stopwatch sw = new Stopwatch ();
                        var userListTask = api.Undocumented.GetChattersAsync (Credentials.Destination);
                        userListTask.Wait ();
                        var userList = userListTask.Result;
                        sw.Start ();
                        foreach (var u in userList)
                        {
                            AddCoins (u.Username.ToLower (), value);
                        }
                        sw.Stop ();
                        SendMessage ($"Added {value} coin to everyone in the channel!");
                        Console.WriteLine ($"{DateTime.Now.ToString()}: gave {value} coins to everyone. Operation took {sw.Elapsed.TotalSeconds} seconds");
                        return;
                    }
                    else
                    {
                        if (list[1][0] == '@')
                        {
                            AddCoins (list[1].Substring (1).ToLower (), value);
                            Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: Added {value} coins to {list[1].Substring(1)}");
                            SendMessage ($"Added {value} coin to @{list[1].Substring(1)}, new balance is {GetCoins(list[1].Substring(1).ToLower())}");
                        }
                        else
                        {
                            AddCoins (list[1].ToLower (), value);
                            Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: Added {value} coins to {list[1]}");
                            SendMessage ($"Added {value} coin to @{list[1]}, new balance is {GetCoins(list[1].ToLower())}");
                        }
                    }
                }
                else
                {
                    SendMessage ($"@{args.Command.ChatMessage.Username} unrecognized value");
                }
            });
            Command winner = new Command (args =>
            {
                if (args.Command.ChatMessage.IsBroadcaster == false)
                {
                    return;
                }
                if (args.Command.ArgumentsAsList.Count == 0)
                {
                    SendMessage ($"@{args.Command.ChatMessage.Username} usage: !winner [roll] or !winner [house] [amount]");
                    return;
                }
                if (giveawayHouse.Count == 0 && args.Command.ArgumentsAsList.Count == 1)
                {
                    SendMessage ($"@{args.Command.ChatMessage.Username} no giveaways left");
                    return;
                }
                var users = chatters;
                if (int.TryParse (args.Command.ArgumentsAsList[0], out int value))
                {
                    string house = giveawayHouse.Dequeue ();
                    int mult = giveawayMult.Dequeue ();
                    giveawayDesc.Dequeue ();
                    Random random = new Random ();
                    string winnerName;
                    if (random.Next (0, 49) < 1)
                    {
                        winnerName = Credentials.Destination.ToLower ();
                    }
                    else
                    {
                        winnerName = users[random.Next (0, users.Count)].Username;
                    }
                    SendMessage ($"And the winner of {value * mult} coin on behalf of {house} is... @{winnerName} ! Congratulations!");
                    if (winnerName.ToLower () == Credentials.BotUsername.ToLower () || winnerName.ToLower () == Credentials.Destination.ToLower () || winnerName.ToLower () == "nightbot")
                    {
                        SendMessage ("When the house wins, EVERYBODY wins! coin Coins for everyone!");
                        Stopwatch sw = new Stopwatch ();
                        sw.Start ();
                        foreach (var u in users)
                        {
                            AddCoins (u.Username, value * mult);
                        }
                        sw.Stop ();
                        SendMessage ($"Added {value * mult} coin to everyone in chat.");
                        Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: Added {value * mult} coins to {users.Count} users (House win)");
                    }
                    else
                    {
                        AddCoins (winnerName, value * mult);
                        SendMessage ($"Added {value * mult} coin to @{winnerName}, new balance is {GetCoins(winnerName)}");
                    }
                }
                else
                {
                    value = int.Parse (args.Command.ArgumentsAsList[1]);
                    string house = args.Command.ArgumentsAsList[0];
                    Random random = new Random ();
                    string winnerName;
                    if (random.Next (0, 49) < 1)
                    {
                        winnerName = Credentials.Destination.ToLower ();
                    }
                    else
                    {
                        winnerName = users[random.Next (0, users.Count)].Username;
                    }
                    SendMessage ($"And the winner of {value} coin on behalf of {house} is... @{winnerName} ! Congratulations!");
                    if (winnerName.ToLower () == Credentials.BotUsername.ToLower () || winnerName.ToLower () == Credentials.Destination.ToLower () || winnerName.ToLower () == "nightbot")
                    {
                        SendMessage ("When the house wins, EVERYBODY wins! coin Coins for everyone!");
                        Stopwatch sw = new Stopwatch ();
                        sw.Start ();
                        foreach (var u in users)
                        {
                            AddCoins (u.Username, value);
                        }
                        sw.Stop ();
                        SendMessage ($"Added {value} coin to everyone in chat.");
                        Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: Added {value} coins to {users.Count} users (House win)");
                    }
                    else
                    {
                        AddCoins (winnerName, value);
                        SendMessage ($"Added {value} coin to @{winnerName}, new balance is {GetCoins(winnerName)}");
                    }
                }
            });
            Command command = new Command (args =>
            {
                var list = args.Command.ArgumentsAsList;
                if (list.Count == 0)
                {
                    string outMsg = "Current commands:\n";
                    foreach (var c in commands)
                    {
                        outMsg += $"!{c.Key}, ";
                    }
                    outMsg.Remove (outMsg.Length - 3, 3);
                    outMsg += "\nCustom commands:\n";
                    foreach (var c in customCommands)
                    {
                        outMsg += $"!{c.Key} - {c.Value.text}\n";
                    }
                    var task = PasteOnPasteBin (outMsg, "IggiBot Command list");
                    task.Wait ();
                    string clMsg = task.Result;
                    SendMessage ("Command list: " + clMsg);
                    return;
                }
                if (args.Command.ChatMessage.IsModerator || args.Command.ChatMessage.IsBroadcaster)
                {
                    if (list[0] == "add" || list[0] == "edit")
                    {
                        string identifier = list[1];
                        string msg = "";
                        int id = 2;
                        while (id < list.Count)
                        {
                            msg += list[id++];
                        }
                        if (customCommands.TryGetValue (identifier, out CustomCommand cc))
                        {
                            customCommands.Remove (identifier);
                        }
                        customCommands.Add (identifier, new CustomCommand (x =>
                        {
                            SendMessage (msg);
                        }, msg));
                    }
                }
            });
            Command giveaways = new Command (args =>
            {
                if (args.Command.ChatMessage.IsBroadcaster == false) return;
                string paste = "";
                foreach (string g in giveawayDesc)
                {
                    paste += g + "\n";
                }
                if (paste == "")
                {
                    SendWhisper (args.Command.ChatMessage.Username, "No more giveaways");
                    return;
                }
                var task = PasteOnPasteBin (paste, $"{DateTime.Now.ToLocalTime().Date} giveaways");
                task.Wait ();
                string msg = task.Result;
                SendWhisper (args.Command.ChatMessage.Username, msg);
            });
            Command uptime = new Command (args =>
            {
                TimeSpan? ts = GetUptime ();
                if (ts.HasValue)
                {
                    int hours = ts.Value.Hours;
                    int minutes = ts.Value.Minutes;
                    int seconds = ts.Value.Seconds;
                    string msg = $"{Credentials.Destination} has been live for ";
                    if (hours > 0 && minutes > 0)
                    {
                        msg += $"{hours} hours, ";
                    }
                    else if (hours > 0)
                    {
                        msg += $"{hours} hours and ";
                    }
                    if (minutes > 0)
                    {
                        msg += $"{minutes} minutes and ";
                    }
                    msg += $"{seconds} seconds";
                    SendMessage (msg);
                }
                else
                {
                    SendMessage ("Channel is not live");
                }
            });
            Command kraken = new Command (args =>
            {
                if (args.Command.ChatMessage.IsBroadcaster || args.Command.ChatMessage.IsModerator || args.Command.ChatMessage.Username.ToLower () == "iggnaccy")
                {
                    Console.WriteLine($"{DateTime.Now}: Release the Kraken!");
                    totalKrakens += nextKraken;
                    if (nextKraken < 64)
                    {
                        nextKraken *= 2;
                    }
                    else
                    {
                        nextKraken++;
                    }
                    SendMessage ("rhyCheers Cheers! rhyKraken rhyPrince rhyCaptain");
                    UpdateOutputFiles();
                }
            });
            Command chattime = new Command (args =>
            {
                if (args.Command.ArgumentsAsList.Count == 0)
                {
                    string msg = $"@{args.Command.ChatMessage.Username} has been in chat for ";
                    int time = GetTime (args.Command.ChatMessage.Username);
                    int minutes = time % 60;
                    int hours = (time / 60) % 60;
                    int days = ((time / 24) / 60) % 60;
                    if (days > 0)
                    {
                        msg += $"{days} days, ";
                    }
                    if (hours > 0)
                    {
                        msg += $"{hours} hours, ";
                    }
                    msg += $"{minutes} minutes";
                    SendMessage (msg);
                }
                else
                {
                    string username = args.Command.ArgumentsAsList[0];
                    if (username.StartsWith ("@"))
                    {
                        username = username.Substring (1);
                    }
                    string msg = $"@{username} has been in chat for ";
                    int time = GetTime (username);
                    int minutes = time % 60;
                    int hours = (time / 60) % 60;
                    int days = ((time / 24) / 60) % 60;
                    if (days > 0)
                    {
                        msg += $"{days} days, ";
                    }
                    if (hours > 0)
                    {
                        msg += $"{hours} hours, ";
                    }
                    msg += $"{minutes} minutes";
                    SendMessage (msg);
                }
            });
            Command missedCoins = new Command (args =>
            {
                if (args.Command.ChatMessage.IsBroadcaster || args.Command.ChatMessage.Username.ToLower () == "iggnaccy")
                {
                    if (int.TryParse (args.Command.ArgumentsAsList[0], out int x))
                    {
                        this.missedCoins += x;
                    }
                    else
                    {
                        SendMessage ($"@{args.Command.ChatMessage.Username} usage !missedcoins [number]");
                    }
                }
            });
            Command missedMinutes = new Command (args =>
            {
                if (args.Command.ChatMessage.IsBroadcaster || args.Command.ChatMessage.Username.ToLower () == "iggnaccy")
                {
                    if (int.TryParse (args.Command.ArgumentsAsList[0], out int x))
                    {
                        this.missedMinutes += x;
                    }
                    else
                    {
                        SendMessage ($"@{args.Command.ChatMessage.Username} usage !missedminutes [number]");
                    }
                }
            });
            Command subcheck = new Command (args =>
            {
                if (args.Command.ArgumentsAsList.Count == 0)
                {
                    try
                    {
                        var subTask = api.Channels.v5.CheckChannelSubscriptionByUserAsync (GetUserID (Credentials.Destination), args.Command.ChatMessage.UserId, Credentials.AuthToken);
                        subTask.Wait ();
                        var sub = subTask.Result;
                        if (sub.SubPlan == "Prime")
                        {
                            SendMessage ($"@{args.Command.ChatMessage.Username} is subscribed to {Credentials.Destination} with Twitch Prime!");
                        }
                        else
                        {
                            SendMessage ($"@{args.Command.ChatMessage.Username} is subscribed to {Credentials.Destination} on tier {int.Parse(sub.SubPlan) / 1000} Subscription Plan");
                        }
                    }
                    catch
                    {
                        SendMessage ($"@{args.Command.ChatMessage.Username} is not subscribed to {Credentials.Destination}");
                    }
                }
                else
                {
                    string username = args.Command.ArgumentsAsList[0];
                    if (username[0] == '@')
                    {
                        username = username.Substring (1);
                    }
                    if (userStats.ContainsKey (username.ToLower ()))
                    {
                        try
                        {
                            var subTask = api.Channels.v5.CheckChannelSubscriptionByUserAsync (GetUserID (Credentials.Destination), GetUserID (username), Credentials.AuthToken);
                            subTask.Wait ();
                            var sub = subTask.Result;
                            if (sub.SubPlan == "Prime")
                            {
                                SendMessage ($"@{args.Command.ChatMessage.Username}, @{username} is subscribed to {Credentials.Destination} with Twitch Prime!");
                            }
                            else
                            {
                                SendMessage ($"@{args.Command.ChatMessage.Username}, @{username} is subscribed to {Credentials.Destination} on tier {int.Parse(sub.SubPlan) / 1000} Subscription Plan");
                            }
                        }
                        catch
                        {
                            SendMessage ($"@{args.Command.ChatMessage.Username}, @{username} is not subscribed to {Credentials.Destination}");
                        }
                    }
                }
            });
            Command subcount = new Command(args =>
            {
                try
                {
                    var task = api.Channels.v5.GetChannelSubscribersAsync(GetUserID(Credentials.Destination), authToken: Credentials.AuthToken);
                    task.Wait();
                    var result = task.Result;
                    SendMessage($"@{args.Command.ChatMessage.Username}, {Credentials.Destination} has {result.Subscriptions.Length} subscribers");
                }
                catch
                {
                    SendMessage($"@{args.Command.ChatMessage.Username} couldn't get subscriber list");
                }
            });
            commands.Add ("auction", auction);
            commands.Add ("bid", bid);
            commands.Add ("coins", coins);
            commands.Add ("coin", coins);
            commands.Add ("winner", winner);
            commands.Add ("command", command);
            commands.Add ("giveaways", giveaways);
            commands.Add ("uptime", uptime);
            commands.Add ("chattime", chattime);
            commands.Add ("subcheck", subcheck);
            commands.Add ("missedminutes", missedMinutes);
            commands.Add ("missedcoins", missedCoins);
            commands.Add ("kraken", kraken);
            //commands.Add ("subcount", subcount);
            WhisperCommand forceUpdate = new WhisperCommand (args =>
            {
                update = true;
                UpdateOutputFiles ();
            });
            WhisperCommand whisperGiveaways = new WhisperCommand (args =>
            {
                string paste = "";
                foreach (string g in giveawayDesc)
                {
                    paste += g + "\n";
                }
                if (paste == "")
                {
                    SendWhisper (args.WhisperMessage.Username, "No more giveaways");
                    return;
                }
                var task = PasteOnPasteBin (paste, $"{DateTime.Now.ToLocalTime().Date} giveaways");
                task.Wait ();
                string msg = task.Result;
                SendWhisper (args.WhisperMessage.Username, msg);
            });
            WhisperCommand switchBits = new WhisperCommand (args =>
            {
                if (args.WhisperMessage.Username.ToLower () == Credentials.Destination.ToLower () || args.WhisperMessage.Username.ToLower () == "iggnaccy")
                {
                    readBits = !readBits;
                    UpdateOutputFiles ();
                }
            });
            WhisperCommand checkBools = new WhisperCommand (args =>
            {
                string kstring = krakenNight ? "Kraken night" : (patreonStream ? "Patreon stream" : "Normal stream");
                string upstring = online ? "Online" : "Not online";
                SendWhisper (args.WhisperMessage.Username, $"{kstring}, {upstring}");
            });
            WhisperCommand forceOnline = new WhisperCommand (args =>
            {
                online = true;
                startTime = DateTime.Now;
            });
            WhisperCommand forcekraken = new WhisperCommand (args =>
            {
                krakenNight = true;
                patreonStream = false;
            });
            whisperCommands.Add ("forceupdate", forceUpdate);
            whisperCommands.Add ("giveaways", whisperGiveaways);
            whisperCommands.Add ("switchbits", switchBits);
            whisperCommands.Add ("checkbools", checkBools);
            whisperCommands.Add ("forcekraken", forcekraken);
            whisperCommands.Add ("forceonline", forceOnline);
        }
        void UpdateOutputFiles ()
        {
            // As requested by Rhykker twitch.tv/rhykker
            float newDonations = float.Parse (donation.ReadLine ().Substring (1));
            float total = subs + newDonations + missedCoins;
            if (readBits)
            {
                total += float.Parse (bitsFile.ReadLine ()) / 100.0f;
            }
            else total += bits;
            int totalM = (int) total + missedMinutes + leftoverMinutes;
            if (krakenNight)
            {
                total += reSubs;
            }
            extraCoins.WriteLine (total.ToString ("0.00"));
            krakenFile.WriteLine ($"Next kraken: {(total - totalKrakens).ToString("0.00")}/{nextKraken}");
            int hours = totalM / 60;
            int minutes = totalM % 60;
            if (hours >= 3)
            {
                endTime.WriteLine ("End time: 8:00 PM");
            }
            else if (minutes < 10)
            {
                endTime.WriteLine ($"End time: {5 + hours}:0{minutes} PM");
            }
            else
            {
                endTime.WriteLine ($"End time: {5 + hours}:{minutes} PM");
            }
        }
        void AddCoins (string username, int value)
        {
            if (userStats.TryGetValue (username.ToLower (), out UserStats us))
            {
                us.coins += value;
            }
            else
            {
                userStats.Add (username.ToLower (), new UserStats (value, 0));
            }
        }
        void AddTime (string username, int value)
        {
            if (userStats.TryGetValue (username.ToLower (), out UserStats us))
            {
                us.time += value;
            }
            else
            {
                userStats.Add (username.ToLower (), new UserStats (0, value));
            }
        }
        int GetCoins (string username)
        {
            if (userStats.TryGetValue (username.ToLower (), out UserStats us))
            {
                return us.coins;
            }
            else
            {
                userStats.Add (username.ToLower (), new UserStats (0, 0));
                return 0;
            }
        }
        int GetTime (string username)
        {
            if (userStats.TryGetValue (username.ToLower (), out UserStats us))
            {
                return us.time;
            }
            else
            {
                userStats.Add (username.ToLower (), new UserStats (0, 0));
                return 0;
            }
        }
        void LoadFiles ()
        {
            var userStatsList = stats.ReadAllLines ();
            foreach (string triple in userStatsList)
            {
                string username = triple.Split (new [] { '=' }) [0].ToLower ();
                var CT = triple.Split (new [] { '=' }) [1].Split (new [] { '.' });
                int coins = int.Parse (CT[0]);
                int time = int.Parse (CT[1]);
                userStats.Add (username, new UserStats (coins, time));
            }
            var commandList = customCommandsCache.ReadAllLines ();
            foreach (string command in commandList)
            {
                string commandName = command.Split (new [] { '=' }) [0].ToLower ();
                string commandText = command.Substring (commandName.Length + 1);
                commandText.Trim ();
                customCommands.Add (commandName, new CustomCommand (x =>
                {
                    SendMessage (commandText);
                }, commandText));
            }
            if (GetUptime ().HasValue)
            {
                var subbers = subCache.ReadAllLines ();
                foreach (string c in subbers)
                {
                    var x = c.Split (new [] { '=' });
                    if (x[0] == "subs")
                    {
                        subs = float.Parse (x[1]);
                    }
                    else if (x[0] == "resubs")
                    {
                        reSubs = float.Parse (x[1]);
                    }
                    else if (x[0] == "bits")
                    {
                        bits = float.Parse (x[1]);
                    }
                }
            }
            leftoverMinutes = int.Parse (bonusMinutesCache.ReadLine ());
            var givs = giveawaysCache.ReadAllLines ();

            foreach (string giveaway in givs)
            {
                var split1 = giveaway.Split (new [] { ':' });
                string username = split1[0];
                var split2 = split1[1].Split (new [] { ',' });
                for (int i = 0; i < split2.Length; i++)
                {
                    split2[i].TrimStart (new [] { ' ' });
                    split2[i].TrimEnd (new [] { ' ' });
                }
                int months;
                if (split2[0] == " new")
                {
                    months = 1;
                }
                else
                {
                    months = int.Parse (split2[0].Split (new [] { ' ' }) [1]);
                }
                int tier = int.Parse (split2[1].Split (new [] { ' ' }) [2]);
                if (split2.Length >= 3)
                {
                    username = split2[2].Split (new [] { ' ' }) [2];
                }
                giveawayDesc.Enqueue (giveaway);
                giveawayHouse.Enqueue (username);
                if (tier == 1 || tier == 2)
                {
                    giveawayMult.Enqueue (months * tier);
                }
                else
                {
                    giveawayMult.Enqueue (months * 6);
                }
            }
        }
        void SaveCache ()
        {
            List<string> statsCache = new List<string> ();
            foreach (var u in userStats)
            {
                statsCache.Add ($"{u.Key}={u.Value.coins}.{u.Value.time}");
            }
            Stopwatch sw = new Stopwatch ();
            sw.Start ();
            stats.WriteAllLines (statsCache);
            sw.Stop ();
            Console.WriteLine ($"{DateTime.Now.ToLocalTime()}: saved user stats in {sw.Elapsed.TotalSeconds} seconds");
            List<string> commandCache = new List<string> ();
            foreach (var u in customCommands)
            {
                commandCache.Add ($"{u.Key}={u.Value.text}");
            }
            customCommandsCache.WriteAllLines (commandCache);
            List<string> subCache = new List<string>
            {
                $"subs={subs}",
                $"resubs={reSubs}",
                $"bits={bits}"
            };
            this.subCache.WriteAllLines (subCache);
            giveawaysCache.WriteAllLines (giveawayDesc.ToList ());
        }
        void NewSession ()
        {
            // As requested by Rhykker twitch.tv/rhykker
            update = false;
            subs = reSubs = bits = missedCoins = missedMinutes = 0;
            totalKrakens = 0;
            nextKraken = 4;
            online = true;
            extraCoins.WriteLine ("0.00");
            krakenFile.WriteLine ("Next kraken: 0.00/4");
            int hours = leftoverMinutes / 60;
            int minutes = leftoverMinutes % 60;
            if (hours >= 3)
            {
                endTime.WriteLine ("End time: 8:00 PM");
            }
            else if (minutes < 10)
            {
                endTime.WriteLine ($"End time: {5 + hours}:0{minutes} PM");
            }
            else
            {
                endTime.WriteLine ($"End time: {5 + hours}:{minutes} PM");
            }
            startTime = DateTime.UtcNow;
            DateTime easterTime = DateTime.Now.ToLocalTime ();
            if (easterTime.DayOfWeek == DayOfWeek.Friday)
            {
                krakenNight = true;
                patreonStream = false;
            }
            else if (easterTime.DayOfWeek == DayOfWeek.Saturday)
            {
                krakenNight = false;
                patreonStream = true;
            }
            else
            {
                krakenNight = patreonStream = false;
            }
        }
        void EndSession ()
        {
            // As requested by Rhykker twitch.tv/rhykker
            online = false;
            if (krakenNight || patreonStream) return;
            var EndTime = DateTime.UtcNow;
            var minus = EndTime.Subtract (startTime);
            float totalCoinsFloat = subs + bits;
            float donationTotal = float.Parse (donation.ReadLine ().Substring (1));
            leftoverMinutes += (int) (totalCoinsFloat + donationTotal + missedMinutes);
            leftoverMinutes -= ((int) minus.TotalMinutes - 180);
            if (leftoverMinutes < 0) leftoverMinutes = 0;
        }
        void SendMessage (string message)
        {
            // client.SendMessage doesn't work for some reason
            client.SendRaw ($"PRIVMSG #{Credentials.Destination.ToLower()} :{message}");
        }
        void SendWhisper (string username, string message)
        {
            // client.SendWhisper doesn't work for some reason
            client.SendRaw ($"PRIVMSG #{Credentials.Destination.ToLower()} :/w {username} {message}");
        }
        string GetUserID (string username)
        {
            try
            {
                var userListTask = api.Users.v5.GetUserByNameAsync (username);
                userListTask.Wait ();
                var userList = userListTask.Result.Matches;
                if (userList == null || userList.Length == 0)
                    return null;
                return userList[0].Id;
            }
            catch
            {
                return null;
            }
        }
        TimeSpan? GetUptime ()
        {
            string userId;
            userId = GetUserID (Credentials.Destination);
            if (userId == null)
                return null;
            try
            {
                return api.Streams.v5.GetUptimeAsync (userId).Result;
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region PasteBin
        static readonly HttpClient pasteBinClient = new HttpClient ();
        async Task<string> PasteOnPasteBin (string paste, string title)
        {
            var query = new Dictionary<string, string>
                { { "api_option", "paste" },
                    { "api_user_key", "" }, 
                    { "api_paste_private", "1" },
                    { "api_paste_name", title }, 
                    { "api_paste_expire_date", "1H" },
                    { "api_paste_format", "text" },
                    { "api_dev_key", "censored" }, 
                };
            query.Add ("api_paste_code", paste);
            var content = new FormUrlEncodedContent (query);
            var task = await pasteBinClient.PostAsync ("https://pastebin.com/api/api_post.php", content);
            var response = await task.Content.ReadAsStringAsync ();
            return response;
        }
        #endregion
    }
    #region SupportClasses
    class Command
    {
        public delegate void CommandEffect (OnChatCommandReceivedArgs x);

        CommandEffect effect;
        public string info;

        public Command (CommandEffect effect)
        {
            this.effect = effect;
            info = "";
        }
        public Command (CommandEffect effect, string info)
        {
            this.effect = effect;
            this.info = info;
        }

        public void FireCommand (OnChatCommandReceivedArgs args)
        {
            effect.Invoke (args);
        }
    }
    class CustomCommand
    {
        public delegate void CommandEffect (OnChatCommandReceivedArgs x);

        CommandEffect effect;
        public string text;
        public string info;

        public CustomCommand (CommandEffect effect, string text)
        {
            this.effect = effect;
            this.text = text;
            info = "";
        }
        public CustomCommand (CommandEffect effect, string text, string info)
        {
            this.effect = effect;
            this.text = text;
            this.info = info;
        }

        public void FireCommand (OnChatCommandReceivedArgs args)
        {
            effect.Invoke (args);
        }
    }
    class WhisperCommand
    {
        public delegate void CommandEffect (OnWhisperCommandReceivedArgs x);

        CommandEffect effect;
        public string info;

        public WhisperCommand (CommandEffect effect)
        {
            this.effect = effect;
            info = "";
        }

        public WhisperCommand (CommandEffect effect, string info)
        {
            this.effect = effect;
            this.info = info;
        }

        public void FireCommand (OnWhisperCommandReceivedArgs args)
        {
            effect.Invoke (args);
        }
    }
    class UserStats
    {
        public int coins;
        public int time;

        public UserStats (int coins, int time)
        {
            this.coins = coins;
            this.time = time;
        }

        public UserStats ()
        {
            coins = time = 0;
        }
    }
    class TxtFile
    {
        StreamReader sr;
        StreamWriter sw;
        string location;

        public TxtFile (string location)
        {
            this.location = location;
        }

        public List<string> ReadAllLines ()
        {
            sr = new StreamReader (location);
            List<string> a = new List<string> ();
            while (sr.EndOfStream == false)
            {
                a.Add (sr.ReadLine ());
            }
            sr.Close ();
            return a;
        }
        public void WriteAllLines (List<string> x)
        {
            sw = new StreamWriter (location);
            foreach (string n in x)
            {
                sw.WriteLine (n);
            }
            sw.Close ();
        }
        public string ReadLine ()
        {
            sr = new StreamReader (location);
            string r = "";
            r = sr.ReadLine ();
            sr.Close ();
            return r;
        }
        public void WriteLine (string text)
        {
            sw = new StreamWriter (location);
            sw.Write (text);
            sw.Close ();
        }
    }
    static class Credentials
    {
        public static string BotUsername;
        public static string BotToken;
        public static string Destination;
        public static string AuthToken;
        public static string ClientID;

        public static void Initialize (string path)
        {
            TxtFile file = new TxtFile (path);
            var lines = file.ReadAllLines ();

            for (int i = 0; i < lines.Count; i++)
            {
                var temp = lines[i].Split (new [] { ':' });
                if (temp.Length > 1)
                {
                    lines[i] = temp[1];
                }
                else lines[i] = temp[0];
            }
            BotUsername = lines[0];
            Destination = lines[1];
            BotToken = lines[2];
            ClientID = lines[3];
            AuthToken = lines[4];
        }
    }
    #endregion
}
