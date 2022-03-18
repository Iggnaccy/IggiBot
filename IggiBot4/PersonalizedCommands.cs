using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

namespace IggiBot4
{
    //Custom-made commands for specific persons
    class PersonalizedCommands
    {
        ///Command dictionaries
        public Dictionary<string, Command> customCommands;
        public Dictionary<string, Command> chatCommands;
        public Dictionary<string, Command> whisperCommands;
        ///Bot reference
        TwitchBot bot;
        ///Command variables
        //Giveaway variables
        Queue<GiveawayMember> giveawayQueue;
        int tier1subs;
        //Kraken variables
        bool kraken;
        int totalKrakens;
        int nextKraken;
        //Coin variables
        float missedCoins;
        float extraCoins;
        float bits;
        int subs;
        int resubs;
        ///Files
        TxtFile krakenText;
        TxtFile extraCoinsText;
        TxtFile donationsText;
        TxtFile dndRewardsText;


        public PersonalizedCommands(TwitchBot Bot, string krakenPath, string extraCoinsPath, string donationPath, string dndPath)
        {
            bot = Bot;
            bot.client.OnGiftedSubscription += GiftSub;
            bot.client.OnNewSubscriber += NewSub;
            bot.client.OnReSubscriber += Resub;
            //bot.client.OnAnonGiftedSubscription += AnonGiftSub;
            bot.client.OnChatCommandReceived += OnChatCommandReceived;
            bot.client.OnWhisperCommandReceived += OnWhisperCommandReceived;
            bot.client.OnMessageReceived += CountBits;
            customCommands = new Dictionary<string, Command>();
            krakenText = new TxtFile(krakenPath);
            extraCoinsText = new TxtFile(extraCoinsPath);
            donationsText = new TxtFile(donationPath);
            dndRewardsText = new TxtFile(dndPath);
            VariableInitialization();
            FillPersonalizedChatCommands();
            FillPersonalizedWhisperCommands();
        }

        internal void RefreshEvents(TwitchBot Bot)
        {
            bot = Bot;
            bot.client.OnGiftedSubscription += GiftSub;
            bot.client.OnNewSubscriber += NewSub;
            bot.client.OnReSubscriber += Resub;
            //bot.client.OnAnonGiftedSubscription += AnonGiftSub;
            bot.client.OnChatCommandReceived += OnChatCommandReceived;
            bot.client.OnWhisperCommandReceived += OnWhisperCommandReceived;
            bot.client.OnMessageReceived += CountBits;
        }

        private void OnWhisperCommandReceived(object sender, TwitchLib.Client.Events.OnWhisperCommandReceivedArgs e)
        {
            try
            {
                if (whisperCommands.TryGetValue(e.Command.CommandText, out Command c))
                {
                    c.Invoke(e).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                bot.LogError("PCommand - WhisperCommand", $"Error occured during {e.Command.CommandText} whisper command: {ex.Message}");
            }
        }

        private void OnChatCommandReceived(object sender, TwitchLib.Client.Events.OnChatCommandReceivedArgs e)
        {
            try
            {
                if (chatCommands.TryGetValue(e.Command.CommandText, out Command c))
                {
                    c.Invoke(e).GetAwaiter().GetResult();
                }
                else if(customCommands.TryGetValue(e.Command.CommandText.ToLower(), out Command cc))
                {
                    cc.Invoke(e).GetAwaiter().GetResult();
                }
                /*if (e.Command.ChatMessage.IsModerator || e.Command.ChatMessage.IsBroadcaster || e.Command.ChatMessage.Username.ToLower() == "iggnaccy")
                {
                    bot.Log($"{e.Command.ChatMessage.Username} used {e.Command.CommandText} command with arguments {e.Command.ArgumentsAsString}");
                }*/
            }
            catch (Exception ex)
            {
                bot.LogError("PCommand - ChatCommand", $"Error occured during {e.Command.CommandText} command: {ex.Message}");
            }
        }

        void VariableInitialization()
        {
            customCommands = new Dictionary<string, Command>();
            chatCommands = new Dictionary<string, Command>();
            whisperCommands = new Dictionary<string, Command>();
            giveawayQueue = new Queue<GiveawayMember>();
            tier1subs = 0;
            totalKrakens = 0;
            nextKraken = 11;
        }

        void FillPersonalizedChatCommands()
        {
            chatCommands.Add("winner", new Command(async (args) =>
            {
                if(args.isBroadcaster)
                {
                    if(giveawayQueue.Count == 0 && tier1subs == 0)
                    {
                        bot.SendMessageRaw($"@{args.Caller} no more giveaways");
                        return 0;
                    }
                    if(args.Arguments.Count == 0)
                    {
                        bot.SendMessageRaw($"@{args.Caller} usage: !winner [value] or !winner [username] [value]");
                        return 0;
                    }
                    if(args.Arguments.Count < 2)
                    {
                        if(int.TryParse(args.Arguments[0], out int value))
                        {

                            GiveawayMember g;
                            if(giveawayQueue.Count > 0)
                            {
                                g = giveawayQueue.Dequeue();
                            }
                            else
                            {
                                g = new GiveawayMember($"{tier1subs} tier 1 subs", tier1subs, 1);
                            }
                            int tierMult;
                            if(g.tier < 3)
                            {
                                tierMult = g.tier;
                            }
                            else
                            {
                                tierMult = 6;
                            }
                            int winValue = value * g.months * tierMult;
                            Random rng = new Random();
                            var chat = await bot.GetChatList();
                            int id = rng.Next(chat.Count);
                            string winner = chat[id].Username;
                            bot.SendMessageRaw($"And the winner of {winValue} coins on behalf of {g.name} is... @{winner}!");
                            if(CheckHouse(winner, g.name))
                            {
                                bot.SendMessageRaw($"When the house wins, EVERYBODY wins! Coins for everyone!");
                                await bot.coinSystem.CoinGiveaway(winValue);
                                bot.SendMessageRaw($"Successfully gave {winValue} {Credentials.currencyName} to everyone in chat");
                            }
                            else
                            {
                                bot.coinSystem.AddCoins(winner, winValue);
                                bot.SendMessageRaw($"Successfully gave {winValue} {Credentials.currencyName} to {winner}, new balance is {bot.coinSystem.GetCoins(winner)} {Credentials.currencyName}");
                            }
                        }
                        else
                        {
                            bot.SendMessageRaw($"@{args.Caller} usage: !winner [value]");
                        }
                    }
                    else
                    {
                        if(bot.coinSystem.CheckKey(args.Arguments[0]))
                        {
                            if(int.TryParse(args.Arguments[1], out int value))
                            {
                                string house = args.Arguments[0];
                                Random rng = new Random();
                                var chat = await bot.GetChatList();
                                int id = rng.Next(chat.Count);
                                string winner = chat[id].Username;
                                bot.SendMessageRaw($"And the winner of {value} {Credentials.currencyName} is... @{winner}!");
                                if(CheckHouse(winner, house))
                                {
                                    bot.SendMessageRaw("When the house wins, EVERYBODY wins! Coins for everyone!");
                                    await bot.coinSystem.CoinGiveaway(value);
                                    bot.SendMessageRaw($"Successfully gave {value} {Credentials.currencyName} to everyone in chat");
                                }
                                else
                                {
                                    bot.coinSystem.AddCoins(winner, value);
                                    bot.SendMessageRaw($"Successfully gave {value} {Credentials.currencyName} to @{winner}, new balance is {bot.coinSystem.GetCoins(winner)}");
                                }
                            }
                            else
                            {
                                bot.SendMessageRaw($"@{args.Caller} usage: !winner [username] [value]");
                            }
                        }
                        else
                        {
                            bot.SendMessageRaw($"@{args.Caller} usage: !winner [username] [value]");
                        }
                    }
                }
                return 0;
            }, "Pulls a random winner of a coin giveaway from chat. Broadcaster only. Syntax: !winner [dice roll] or !winner [house] [value]\n"));
            chatCommands.Add("giveaways", new Command(async (args) =>
            {
                if(args.isBroadcaster)
                {
                    string msg = "";
                    foreach(var g in giveawayQueue)
                    {
                        msg += $"{g.name}: {g.months} months, tier {g.tier}\n";
                    }
                    if(tier1subs > 0)
                    {
                        msg += $"{tier1subs} tier 1 subs\n";
                    }
                    var paste = await bot.PasteOnPasteBin(msg, $"{DateTime.Now.Year}.{DateTime.Now.Month}.{DateTime.Now.Day} IggiBot giveaways");
                    bot.SendWhisper(args.Caller, paste);
                    return 5;
                }
                return 0;
            }, "Posts giveaway list on pastebin. Broadcaster only. Syntax: !giveaways\n"));
            chatCommands.Add("kraken", new Command(async (args) =>
            {
                if(args.isBroadcaster || args.isModerator || args.isOwner)
                {
                    bot.SendMessageRaw($"Release the Kraken!");
                    totalKrakens += nextKraken;
                    nextKraken += 11;
                    await UpdateFiles();
                }
                return 0;
            }, "Releases the Kraken! Moderator+ only. Syntax: !kraken\n"));
            chatCommands.Add("missedcoins", new Command(async (args) =>
            {
                if(args.isBroadcaster || args.isModerator || args.isOwner)
                {
                    if(args.Arguments.Count > 0)
                    {
                        if(int.TryParse(args.Arguments[0], out int value))
                        {
                            missedCoins += value;
                            await UpdateFiles();
                        }
                        else if(float.TryParse(args.Arguments[0], out float valueF))
                        {
                            missedCoins += valueF;
                            await UpdateFiles();
                        }
                    }
                }
                return 0;
            }, "Adds missed extra coins. Moderator+ only. Syntax: !missedcoins [float value]\n"));

            chatCommands.Remove("winner");
            chatCommands.Remove("giveaways");
        }

        void FillPersonalizedWhisperCommands()
        {
            whisperCommands.Add("forceupdate", new Command(async (args) =>
            {
                if(args.isBroadcaster || args.isModerator || args.isOwner)
                {
                    await UpdateFiles();
                }
                return 0;
            }));
            whisperCommands.Add("dndanswerrewards", new Command(async (args) =>
            {
                if (args.isOwner == false) return 0;
                var r = await dndRewardsText.ReadAllLinesAsync();
                List<List<string>> splits = new List<List<string>>();
                foreach(var l in r)
                {
                    splits.Add(l.Split(new[] { '"' }).ToList());
                }
                foreach(var l in splits)
                {
                    for(int i = 0; i < l.Count; i++)
                    {
                        l.RemoveAt(i);
                    }
                }
                List<string> names = new List<string>();
                for(int i = 1; i < splits.Count; i++)
                {
                    names.Add(splits[i][3]);
                }
                foreach(var name in names)
                {
                    if(name.Length > 0)
                    {
                        bot.coinSystem.AddCoins(name, 533);
                    }
                }
                return 0;
            }));
            whisperCommands.Add("giveaways", new Command(async (args) =>
            {
                if (args.isBroadcaster || args.isOwner)
                {
                    string msg = "";
                    foreach (var g in giveawayQueue)
                    {
                        msg += $"{g.name}: {g.months} months, tier {g.tier}\n";
                    }
                    if (tier1subs > 0)
                    {
                        msg += $"{tier1subs} tier 1 subs\n";
                    }
                    var paste = await bot.PasteOnPasteBin(msg, $"{DateTime.Now.Year}.{DateTime.Now.Month}.{DateTime.Now.Day} IggiBot giveaways");
                    bot.SendWhisper(args.Caller, paste);
                    return 5;
                }
                return 0;
            }));

            whisperCommands.Remove("giveaways");
        }

        internal async Task UpdateFiles()
        {
            float preTotal = float.Parse((await extraCoinsText.ReadAllLinesAsync())[0]);
            float donations = float.Parse((await donationsText.ReadAllLinesAsync())[0].Substring(1));
            extraCoins = donations + bits + subs + missedCoins;
            if(kraken)
            {
                extraCoins += resubs;
                if (preTotal == extraCoins) return;
                await krakenText.WriteAllLinesAsync(new List<string>
                {
                    $"Next kraken: {(extraCoins - totalKrakens).ToString("0.00")}/{nextKraken}",
                    $"Krakens released: {(nextKraken / 11) - 1}"
                });
            }
            if (preTotal == extraCoins) return;
            await extraCoinsText.WriteLineAsync(extraCoins.ToString("0.00"));
        }

        bool CheckHouse(string username, string house)
        {
            List<string> houseList = new List<string>
            {
                Credentials.targetStream.ToLower(),
                Credentials.botUsername.ToLower(),
                "bunkitia",
                "pollmapebot",
                "nightbot",
                "stayhydratedbot",
                house.ToLower()
            };
            return houseList.Contains(username.ToLower());
        }

        void NewSub(object source, TwitchLib.Client.Events.OnNewSubscriberArgs args)
        {
            try
            {
                int tier = 1;
                if (args.Subscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Prime || args.Subscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier1)
                {
                    tier = 1;
                    subs += 5;
                }
                else if (args.Subscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier2)
                {
                    tier = 2;
                    subs += 10;
                }
                else if (args.Subscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier3)
                {
                    tier = 3;
                    subs += 25;
                }
                /*
                if (tier == 1)
                {
                    tier1subs++;
                }
                else
                {
                    giveawayQueue.Enqueue(new GiveawayMember(args.Subscriber.DisplayName, 1, tier));
                }
                */
                DoGiveaway(args.Subscriber.DisplayName, tier, 1).GetAwaiter().GetResult();
                UpdateFiles().GetAwaiter().GetResult();
            }
            catch(Exception ex)
            {
                bot.LogError("New sub", ex.Message);
            }
        }

        void Resub(object source, TwitchLib.Client.Events.OnReSubscriberArgs args)
        {
            try
            {


                int tier = 1;
                if (args.ReSubscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Prime || args.ReSubscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier1)
                {
                    tier = 1;
                    resubs += 5;
                }
                else if (args.ReSubscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier2)
                {
                    tier = 2;
                    resubs += 10;
                }
                else if (args.ReSubscriber.SubscriptionPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier3)
                {
                    tier = 3;
                    resubs += 25;
                }
                /*
                if (tier == 1)
                {
                    if(int.Parse(args.ReSubscriber.MsgParamCumulativeMonths) >= 24)
                    {
                        giveawayQueue.Enqueue(new GiveawayMember(args.ReSubscriber.DisplayName, int.Parse(args.ReSubscriber.MsgParamCumulativeMonths), tier));
                    }
                    else tier1subs++;
                }
                else
                {
                    giveawayQueue.Enqueue(new GiveawayMember(args.ReSubscriber.DisplayName, int.Parse(args.ReSubscriber.MsgParamCumulativeMonths), tier));
                }*/
                //args.ReSubscriber.RawIrc;
                //Regex regex = new Regex(@"/(;msg-param-culmulative-months=\b[0-9]|[0-9][0-9]\b)/g");
                //int month = int.Parse(regex.Match(args.ReSubscriber.RawIrc).Groups[0].Captures[0].Value);

                if (int.TryParse(args.ReSubscriber.MsgParamCumulativeMonths, out int months))
                {
                    DoGiveaway(args.ReSubscriber.DisplayName, tier, Math.Max(months, 1)).GetAwaiter().GetResult();
                }
                else
                {
                    DoGiveaway(args.ReSubscriber.DisplayName, tier, 1).GetAwaiter().GetResult();
                }
                UpdateFiles().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                bot.LogError("Resub", ex.Message);
            }
        }

        void GiftSub(object source, TwitchLib.Client.Events.OnGiftedSubscriptionArgs args)
        {
            try
            {
                int tier = 1;
                if (args.GiftedSubscription.MsgParamSubPlan == TwitchLib.Client.Enums.SubscriptionPlan.Prime || args.GiftedSubscription.MsgParamSubPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier1)
                {
                    tier = 1;
                    if (int.TryParse(args.GiftedSubscription.MsgParamMonths, out int m))
                    {
                        resubs += 5;
                    }
                    else
                    {
                        subs += 5;
                    }
                }
                else if (args.GiftedSubscription.MsgParamSubPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier2)
                {
                    tier = 2;
                    if (int.TryParse(args.GiftedSubscription.MsgParamMonths, out int m))
                    {
                        resubs += 10;
                    }
                    else
                    {
                        subs += 10;
                    }
                }
                else if (args.GiftedSubscription.MsgParamSubPlan == TwitchLib.Client.Enums.SubscriptionPlan.Tier3)
                {
                    tier = 3;
                    if (int.TryParse(args.GiftedSubscription.MsgParamMonths, out int m))
                    {
                        resubs += 25;
                    }
                    else
                    {
                        subs += 25;
                    }
                }
                if (int.TryParse(args.GiftedSubscription.MsgParamMonths, out int months))
                {
                    DoGiveaway(args.GiftedSubscription.MsgParamRecipientUserName, tier, Math.Max(months,1)).GetAwaiter().GetResult();
                }
                else
                {
                    DoGiveaway(args.GiftedSubscription.MsgParamRecipientUserName, tier, 1).GetAwaiter().GetResult();
                }
                UpdateFiles().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                bot.LogError("GiftedSub", ex.Message + $" {args.GiftedSubscription.MsgParamMonths}");
            }
        }

        void CountBits(object source, TwitchLib.Client.Events.OnMessageReceivedArgs args)
        {
            if(args.ChatMessage.Bits > 0)
            {
                bits += args.ChatMessage.Bits / 100f;
                UpdateFiles().GetAwaiter().GetResult();
            }
        }

        public Command CreateCustomCommand(string code)
        {
            return new Command((args) => 
            {
                string text = code;
                bot.SendMessageRaw(text);
                return Task.Run(() => { return 4; });
            }, code);
        }

        async Task DoGiveaway(string house, int tier, int months)
        {
            if (bot.online == false) return;
            int tierMult = tier == 3 ? 6 : tier;
            Random rng = new Random();
            int roll = 0;
            for(int i = 0; i <= months / 6 && i < 3; i++)
            {
                roll = Math.Max(rng.Next(1, 21), roll);
            }
            int critReq = 20, critConf = Math.Max(20 - months, 2);
            if(months >= 24)
            {
                critReq = 19;
            }
            if(roll >= critReq)
            {
                if(rng.Next(1,21) >= critConf)
                {
                    roll = 40;
                }
            }
            bot.SendMessage($"@{house} rolled a {roll}!");
            int winCount = tierMult * months * roll;
            var chatList = await bot.GetChatList();
            string winner = chatList[rng.Next(0, chatList.Count)].Username;
            if(CheckHouse(winner, house))
            {
                bot.SendMessage($"And the winner of {winCount} {Credentials.currencyName} on behalf of @{house} is... @{winner}! When the house wins, EVERYBODY wins! {Credentials.currencyName} for everyone!");
                foreach(var c in chatList)
                {
                    bot.coinSystem.AddCoins(c.Username.ToLower(), winCount);
                }
            }
            else
            {
                bot.coinSystem.AddCoins(winner, winCount);
                bot.SendMessage($"And the winner of {winCount} {Credentials.currencyName} on behalf of @{house} is... @{winner}. New balance: {bot.coinSystem.GetCoins(winner)}");
            }
        }

        internal void NewSession()
        {
            extraCoins = missedCoins = 0;
            bits = subs = 0;
            extraCoinsText.WriteLine("0.00");
            if(DateTime.Now.DayOfWeek == DayOfWeek.Friday)
            {
                kraken = true;
                krakenText.WriteAllLines(new List<string>
                {
                    "Next kraken: 0.00/11",
                    "Krakens released: 0"
                });
                nextKraken = 11;
                totalKrakens = 0;
            }
            else
            {
                kraken = false;
            }
            bot.Log("Automatically starting new session");
        }

        internal void EndSession()
        {
            extraCoins = missedCoins = 0;
            bits = subs = 0;
            nextKraken = 11;
            totalKrakens = 0;
            kraken = false;
            bot.Log("Automatically ending session");
        }
    }

    class GiveawayMember
    {
        public string name;
        public int months;
        public int tier;

        public GiveawayMember(string name, int months, int tier)
        {
            this.name = name;
            this.months = months;
            this.tier = tier;
        }
    }
}
