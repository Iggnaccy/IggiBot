using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IggiBot4
{
    //Base commands that IggiBot comes with
    class Commands
    {
        //Dictionaries with standard commands
        public Dictionary<string, Command> chatCommands;
        public Dictionary<string, Command> whisperCommands;
        //Variables used in standard commands
        Dictionary<int, string> quotes;
        //Auction variables
        public string auctionWinner;
        private int auctionLeft;
        public string auctionInfo;
        public bool auctionOpen;
        public string auctionLast;
        private int auctionTime;
        private int auctionBid;
        private int auctionIncrement;
        private int auctionLastAnnounement;
        //Timers
        Timer auctionTimer;
        Timer coinCheckTimer;

        TwitchBot bot;

        //Misc.
        Queue<string> coinCheckList;
        TxtFile commandText;
        TxtFile quoteText;

        public Commands(TwitchBot Bot, string quotePath, string commandPath = @"/home/iggnaccy/IggiBot4/IggiBot4/bin/Debug/commands.txt")
        {
            chatCommands = new Dictionary<string, Command>();
            whisperCommands = new Dictionary<string, Command>();
            coinCheckList = new Queue<string>();
            commandText = new TxtFile(commandPath);
            quoteText = new TxtFile(quotePath);
            quotes = new Dictionary<int, string>();
            bot = Bot;
            LoadCommands();
            LoadQuotes();
            auctionTimer = new Timer((sender) => 
            {
                if (!auctionOpen) return;
                auctionLeft--;
                string winner = auctionWinner;
                int winAmount = auctionBid;

                if(auctionLeft == auctionLastAnnounement)
                {
                    bot.SendMessageRaw($"The auction will end soon! Current winner is {winner} with a bid of {winAmount} {Credentials.currencyName} ! We are auctioning for {auctionInfo}");
                }
                else if(auctionLeft * 2 == auctionTime)
                {
                    bot.SendMessageRaw($"The auction is near it's half-way spot! Current winner is {winner} with a bid of {winAmount} {Credentials.currencyName} ! We are auctioning for {auctionInfo}");
                }
                else if(auctionLeft == 0)
                {
                    bot.SendMessageRaw($"@{Credentials.targetStream} The auction has closed! The winner is @{winner}, with a bid of {winAmount} {Credentials.currencyName}");
                    bot.coinSystem.AddCoins(winner, -winAmount);
                    auctionBid = auctionTime = auctionLeft = 0;
                    auctionLast = winner;
                    auctionWinner = auctionInfo = "";
                    auctionOpen = false;
                }
            }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            coinCheckTimer = new Timer((sender) =>
            {
                if (coinCheckList.Count == 0) return;
                string msg = "";
                while(coinCheckList.Count > 0 && msg.Length <= 500)
                {
                    string username = coinCheckList.Dequeue();
                    msg += $"@{username} has {bot.coinSystem.GetCoins(username)} {Credentials.currencyName}, ";
                }
                msg = msg.Substring(0, msg.Length - 2);
                bot.SendMessageRaw(msg);
            }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2.5));
            bot.client.OnChatCommandReceived += ChatCommandHandler;
            bot.client.OnWhisperCommandReceived += WhisperCommandHandler;
            FillChatCommands();
            FillWhisperCommands();
        }

        internal void RefreshEvents(TwitchBot Bot)
        {
            bot = Bot;
            bot.client.OnChatCommandReceived += ChatCommandHandler;
            bot.client.OnWhisperCommandReceived += WhisperCommandHandler;
        }

        private void WhisperCommandHandler(object sender, TwitchLib.Client.Events.OnWhisperCommandReceivedArgs e)
        {
            if(whisperCommands.TryGetValue(e.Command.CommandText.ToLower(), out Command c))
            {
                c.Invoke(e).GetAwaiter().GetResult();
            }
        }

        internal void Close()
        {
            SaveCommands();
            SaveQuotes();
        }

        void FillChatCommands()
        {
            chatCommands.Add("auction", new Command((args) =>
            {
                if (args.Arguments.Count == 0)
                {
                    if (auctionOpen && auctionInfo.Length > 0)
                    {
                        bot.SendMessageRaw($"@{args.Caller} We are auctioning for {auctionInfo}");
                        return Task.Run(() => { return 2; });
                    }
                    else
                    {
                        return Task.Run(() => { return 0; });
                    }
                }
                else if (args.Arguments[0] == "last" && auctionLast != "")
                {
                    bot.SendMessageRaw($"@{args.Caller} last auction was won by {auctionLast}");
                    return Task.Run(() => { return 2; });
                }
                else if(args.isBroadcaster || args.isOwner)
                {
                    if(args.Arguments[0].ToLower() == "close")
                    {
                        if(auctionOpen)
                        {
                            auctionOpen = false;
                            bot.SendMessageRaw("The auction was cancelled");
                            auctionWinner = auctionInfo = "";
                            auctionLeft = auctionTime = auctionBid = 0;
                        }
                        return Task.Run(() => { return 0; });
                    }
                    if(auctionOpen)
                    {
                        bot.SendMessageRaw("There's an auction running already");
                        return Task.Run(() => { return 0; });
                    }
                    int id = 0;
                    if(args.Arguments[id].ToLower() == "open")
                    {
                        id++;
                    }
                    if (int.TryParse(args.Arguments[id], out int time))
                    {
                        if (int.TryParse(args.Arguments[id + 1], out int increment))
                        {
                            auctionIncrement = increment;
                            id++;
                        }
                        else
                        {
                            auctionIncrement = 1;
                        }
                        string desc = "";
                        for (id++; id < args.Arguments.Count; id++)
                        {
                            desc += $"{args.Arguments[id]} ";
                        }
                        desc = desc.TrimEnd();
                        auctionTime = auctionLeft = time * 60;
                        auctionWinner = "";
                        auctionInfo = desc;
                        auctionBid = 0;
                        auctionOpen = true;
                        auctionLastAnnounement = 30;
                        if (auctionInfo.Length > 0)
                        {
                            bot.SendMessageRaw($"The auction is now open and will last {time} minutes! We are auctioning for {auctionInfo} The minimal increment is {auctionIncrement} {Credentials.currencyName}");

                        }
                        else
                        {
                            bot.SendMessageRaw($"The auction is now open and will last {time} minutes! The minimal increment is {auctionIncrement} {Credentials.currencyName}");
                        }
                    }
                    else if (args.Arguments[id].Split(new[] { 'r' }).Length >= 2)
                    {
                        var split = args.Arguments[id].Split(new[] { 'r' });
                        if (int.TryParse(split[0], out time)) 
                        {
                            //Console.WriteLine($"splits: {split[0]}, {split[1]}");
                            if(int.TryParse(args.Arguments[id+1], out int increment))
                            {
                                auctionIncrement = increment;
                                id++;
                            }
                            else
                            {
                                auctionIncrement = 1;
                            }
                            string desc = "";
                            for(id++; id < args.Arguments.Count; id++)
                            {
                                desc += $"{args.Arguments[id]} ";
                            }
                            desc = desc.TrimEnd();
                            if (int.TryParse(split[1], out int seconds))
                            {
                                Random rng = new Random();
                                var random = rng.Next(-seconds, seconds);
                                auctionLastAnnounement = rng.Next(15, 45);
                                auctionTime = auctionLeft = time * 60 + random;
                                //Console.WriteLine($"Auction time: {auctionTime}, base: {time * 60}, randomed: {random}");
                            }
                            else
                            {
                                auctionTime = auctionLeft = time * 60;
                                auctionLastAnnounement = 30;
                            }
                            auctionWinner = "";
                            auctionInfo = desc;
                            auctionBid = 0;
                            auctionOpen = true;
                            if (auctionInfo.Length > 0)
                            {
                                bot.SendMessageRaw($"The auction is now open and will last around {time} minutes! We are auctioning for {auctionInfo} The minimal increment is {auctionIncrement} {Credentials.currencyName}");

                            }
                            else
                            {
                                bot.SendMessageRaw($"The auction is now open and will last around {time} minutes! The minimal increment is {auctionIncrement} {Credentials.currencyName}");
                            }
                        }
                        else
                        {
                            bot.SendMessageRaw($"@{args.Caller} usage: !auction [time in minutes][r[random time in seconds]] [minimal increments] [description]");
                        }
                    }
                    else
                    {
                        bot.SendMessageRaw($"@{args.Caller} usage: !auction [time in minutes][r[random time in seconds]] [minimal increments] [description]");
                    }
                    return Task.Run(() => { return 0; });
                }
                else
                {
                    if (auctionOpen && auctionInfo.Length > 0)
                    {
                        bot.SendMessageRaw($"@{args.Caller} We are auctioning for {auctionInfo}");
                        return Task.Run(() => { return 2; });
                    }
                    else
                    {
                        return Task.Run(() => { return 0; });
                    }
                }
            }, "Displays info about the auction. Controls the auction (Broadcaster only). Syntax: !auction, !auction [time in minutes] [description] or !auction [time in minutes] [minimal increment] [description]\n"));
            chatCommands.Add("bid", new Command((args) =>
            {
                if(auctionOpen == false)
                {
                    bot.SendMessageRaw($"@{args.Caller} there's no auction open");
                    return Task.Run(() => { return 0; });
                }
                if(args.Arguments.Count == 0)
                {
                    bot.SendMessageRaw($"@{args.Caller} usage: !bid [amount]");
                    return Task.Run(() => { return 0; });
                }
                int callerCoins = bot.coinSystem.GetCoins(args.Caller);
                if (int.TryParse(args.Arguments[0], out int flatResult))
                {
                    if (flatResult > callerCoins)
                    {
                        bot.SendMessageRaw($"@{args.Caller} you don't have that many coins");
                        return Task.Run(() => { return 0; });
                    }
                    if (auctionBid + auctionIncrement > flatResult)
                    {
                        bot.SendMessageRaw($"@{args.Caller} the top bid is {auctionBid}. Minimal increment is {auctionIncrement}");
                        return Task.Run(() => { return 0; });
                    }
                    if (auctionWinner == args.Caller.ToLower())
                    {
                        bot.SendMessageRaw($"@{args.Caller} you can't outbid yourself BrokeBack");
                        return Task.Run(() => { return 0; });
                    }
                    auctionWinner = args.Caller.ToLower();
                    auctionBid = flatResult;
                    return Task.Run(() => { return 0; });
                }
                else if (int.TryParse(args.Arguments[0].Substring(0, args.Arguments[0].Length - 1), out int percentageResult) && args.Arguments[0].EndsWith("%"))
                {
                    int result = (int)(callerCoins * (percentageResult / 100f));
                    if (auctionBid + auctionIncrement > result)
                    {
                        bot.SendMessageRaw($"@{args.Caller} the top bid is {auctionBid}. Minimal increment is {auctionIncrement}");
                        return Task.Run(() => { return 0; });
                    }
                    if (auctionWinner == args.Caller.ToLower())
                    {
                        bot.SendMessageRaw($"@{args.Caller} you can't outbid yourself BrokeBack");
                        return Task.Run(() => { return 0; });
                    }
                    auctionWinner = args.Caller.ToLower();
                    auctionBid = result;
                    return Task.Run(() => { return 0; });
                }
                else if (float.TryParse(args.Arguments[0].Substring(0, args.Arguments[0].Length - 1), out float percentageResultF) && args.Arguments[0].EndsWith("%"))
                {
                    int result = (int)(callerCoins * (percentageResultF / 100f));
                    if (auctionBid + auctionIncrement > result)
                    {
                        bot.SendMessageRaw($"@{args.Caller} the top bid is {auctionBid}. Minimal increment is {auctionIncrement}");
                        return Task.Run(() => { return 0; });
                    }
                    if (auctionWinner == args.Caller.ToLower())
                    {
                        bot.SendMessageRaw($"@{args.Caller} you can't outbid yourself BrokeBack");
                        return Task.Run(() => { return 0; });
                    }
                    auctionWinner = args.Caller.ToLower();
                    auctionBid = result;
                    return Task.Run(() => { return 0; });
                }
                return Task.Run(() => { return 0; });
            }, "Bid in an auction. Syntax: !bid [amount] or !bid [amount]%\n"));
            chatCommands.Add("chattime", new Command((args) => 
            {
                if(args.Arguments.Count == 0 || !bot.coinSystem.CheckKey(args.Arguments[0]))
                {
                    var time = TimeSpan.FromMinutes(bot.coinSystem.GetTime(args.Caller));
                    string msg = $"@{args.Caller} has been in chat for ";
                    if(time.Days > 0)
                    {
                        msg += $"{time.Days} days, ";
                    }
                    if(time.Hours > 0)
                    {
                        msg += $"{time.Hours} hours, ";
                    }
                    msg += $"{time.Minutes} minutes.";
                    bot.SendMessageRaw(msg);
                    return Task.Run(() => { return 5; });
                }
                else
                {
                    var time = TimeSpan.FromMinutes(bot.coinSystem.GetTime(args.Arguments[0]));
                    string msg = $"@{args.Arguments[0]} has been in chat for ";
                    if (time.Days > 0)
                    {
                        msg += $"{time.Days} days, ";
                    }
                    if (time.Hours > 0)
                    {
                        msg += $"{time.Hours} hours, ";
                    }
                    msg += $"{time.Minutes} minutes.";
                    bot.SendMessageRaw(msg);
                    return Task.Run(() => { return 5; });
                }
            }, "Checks how much time has a user spent in chat. Syntax: !chattime or !chattime [username]\n"));
            chatCommands.Add("coins", new Command(async (args) =>
            {
                if(args.Arguments.Count == 0)
                {
                    coinCheckList.Enqueue(args.Caller);
                    return 0;
                }
                if(args.Arguments[0].StartsWith("@"))
                {
                    args.Arguments[0] = args.Arguments[0].Substring(1);
                }
                if(bot.coinSystem.CheckKey(args.Arguments[0]))
                {
                    coinCheckList.Enqueue(args.Arguments[0]);
                    return 0;
                }
                if(args.isBroadcaster)
                {
                    if(args.Arguments.Count >= 3)
                    {
                        if(args.Arguments[0] == "add")
                        {
                            if(int.TryParse(args.Arguments[2], out int value))
                            {
                                if(args.Arguments[1] == "all")
                                {
                                    await bot.coinSystem.CoinGiveaway(value);
                                    bot.SendMessageRaw($"Successfully gave {value} {Credentials.currencyName} to all people in chat");
                                }
                                else if(bot.coinSystem.CheckKey(args.Arguments[1]))
                                {
                                    bot.coinSystem.AddCoins(args.Arguments[1], value);
                                    bot.SendMessageRaw($"Successfully gave {value} {Credentials.currencyName} to @{args.Arguments[1]} New balance is: {bot.coinSystem.GetCoins(args.Arguments[1])}");
                                }
                                else
                                {
                                    bot.SendMessageRaw($"@{args.Caller} usage: !coins [add] [username] [value]");
                                }
                            }
                        }
                    }
                }
                return 0;
            }, "Check coins of a user. Syntax: !coins or !coins [username].\n\tAdd coins to a user. Broadcaster only. Syntax: !coins add [username] [value] or !coins add all [value]\n"));
            chatCommands.Add("coin", new Command(async (args) =>
            {
                if (args.Arguments.Count == 0)
                {
                    coinCheckList.Enqueue(args.Caller);
                    return 0;
                }
                if (args.Arguments[0].StartsWith("@"))
                {
                    args.Arguments[0] = args.Arguments[0].Substring(1);
                }
                if (bot.coinSystem.CheckKey(args.Arguments[0]))
                {
                    coinCheckList.Enqueue(args.Arguments[0]);
                    return 0;
                }
                if (args.isBroadcaster)
                {
                    if (args.Arguments.Count >= 3)
                    {
                        if (args.Arguments[0] == "add")
                        {
                            if (int.TryParse(args.Arguments[2], out int value))
                            {
                                if (args.Arguments[1] == "all")
                                {
                                    await bot.coinSystem.CoinGiveaway(value);
                                    bot.SendMessageRaw($"Successfully gave {value} {Credentials.currencyName} to all people in chat");
                                }
                                else if (bot.coinSystem.CheckKey(args.Arguments[1]))
                                {
                                    bot.coinSystem.AddCoins(args.Arguments[1], value);
                                    bot.SendMessageRaw($"Successfully gave {value} {Credentials.currencyName} to @{args.Arguments[1]} New balance is: {bot.coinSystem.GetCoins(args.Arguments[1])}");
                                }
                                else
                                {
                                    bot.SendMessageRaw($"@{args.Caller} usage: !coins [add] [username] [value]");
                                }
                            }
                        }
                    }
                }
                return 0;
            }, "Alias for !coins"));
            chatCommands.Add("command", new Command(async (args) => 
            {
                if(args.Arguments.Count == 0 || (!args.isBroadcaster && !args.isModerator))
                {
                    string paste = "";
                    foreach(var c in chatCommands)
                    {
                        paste += $"!{c.Key}: {c.Value.info}\n";
                    }
                    foreach(var c in bot.personalizedCommands.chatCommands)
                    {
                        paste += $"!{c.Key}: {c.Value.info}\n\n";
                    }
                    paste += "Custom commands:\n\n";
                    foreach (var c in bot.personalizedCommands.customCommands)
                    {
                        paste += $"!{c.Key}: {c.Value.info}\n\n";
                    }
                    string link = await bot.PasteOnPasteBin(paste, "IggiBot Command list");
                    bot.SendMessageRaw($"@{args.Caller} command list: {link}");
                    return 5;
                }
                else if((args.isModerator || args.isBroadcaster) && args.Arguments.Count >= 2)
                {
                    if(args.Arguments[0] == "add" || args.Arguments[0] == "edit")
                    {
                        string name = args.Arguments[1];
                        while(name.StartsWith("!"))
                        {
                            name = name.Substring(1);
                        }
                        string code = "";
                        for(int i = 2; i < args.Arguments.Count; i++)
                        {
                            code += $"{args.Arguments[i]} ";
                        }
                        code = code.TrimEnd();
                        if (bot.personalizedCommands.customCommands.TryGetValue(name.ToLower(), out Command cc))
                        {
                            bot.personalizedCommands.customCommands[name.ToLower()] = bot.personalizedCommands.CreateCustomCommand(code);
                            SaveCommands();
                            bot.SendMessageRaw($"@{args.Caller} successfully edited !{args.Arguments[1]} custom command");
                        }
                        else
                        {
                            bot.personalizedCommands.customCommands.Add(name.ToLower(), bot.personalizedCommands.CreateCustomCommand(code));
                            SaveCommands();
                            bot.SendMessageRaw($"@{args.Caller} successfully added !{args.Arguments[1]} custom command");
                        }
                    }
                    else if(args.Arguments[0] == "remove" || args.Arguments[0] == "delete")
                    {
                        if (bot.personalizedCommands.customCommands.ContainsKey(args.Arguments[1].ToLower()))
                        {
                            bot.personalizedCommands.customCommands.Remove(args.Arguments[1].ToLower());
                            bot.SendMessage($"@{args.Caller} successfully removed !{args.Arguments[1]} custom command");
                        }
                        else if (bot.personalizedCommands.customCommands.ContainsKey(args.Arguments[1].ToLower().Substring(1)))
                        {

                            bot.personalizedCommands.customCommands.Remove(args.Arguments[1].ToLower().Substring(1));
                            bot.SendMessage($"@{args.Caller} successfully removed {args.Arguments[1]} custom command");
                        }
                    }
                    return 0;
                }
                else
                {
                    string paste = "";
                    foreach (var c in chatCommands)
                    {
                        paste += $"!{c.Key}: {c.Value.info}\n\n";
                    }
                    foreach (var c in bot.personalizedCommands.chatCommands)
                    {
                        paste += $"!{c.Key}: {c.Value.info}\n\n";
                    }
                    paste += "Custom commands:\n\n";
                    foreach (var c in bot.personalizedCommands.customCommands)
                    {
                        paste += $"!{c.Key}: {c.Value.info}\n\n";
                    }
                    string link = await bot.PasteOnPasteBin(paste, "IggiBot Command list");
                    bot.SendMessageRaw($"@{args.Caller} command list: {link}");
                    return 5;
                }
            }, "Posts this list on pastebin. Syntax: !command\n\tAdds, edits or removes custom commands. Moderator only. Syntax: !command [add/edit] [command name] [formatted command string] for adding/editing. !command remove [command name] for removing\n"));
            chatCommands.Add("uptime", new Command(async (args) =>
            {
                var ts = await bot.GetUptime();
                if(ts.HasValue)
                {
                    string msg = $"@{args.Caller}, {Credentials.targetStream} was live for ";
                    if(ts.Value.Hours > 0)
                    {
                        msg += $"{ts.Value.Hours} hours, ";
                    }
                    if(ts.Value.Minutes > 0)
                    {
                        msg += $"{ts.Value.Minutes} minutes, ";
                    }
                    msg += $"{ts.Value.Seconds} seconds.";
                    bot.SendMessageRaw(msg);
                }
                else
                {
                    bot.SendMessageRaw($"@{args.Caller}, {Credentials.targetStream} is not live");
                }
                return 120;
            }, "Checks how long has the stream been live. Syntax: !uptime\n"));
            chatCommands.Add("subcheck", new Command(async (args) =>
            {
                if(args.Arguments.Count == 0)
                {
                    try
                    {
                        var sub = await TwitchBot.api.V5.Channels.CheckChannelSubscriptionByUserAsync(await bot.GetUserID(Credentials.targetStream), await bot.GetUserID(args.Caller), Credentials.authToken);
                        if(sub.SubPlan == "1000" || sub.SubPlan == "Prime")
                        {
                            bot.SendMessageRaw($"@{args.Caller} is subscribed to {Credentials.targetStream} on a Tier 1 sub plan");
                        }
                        if (sub.SubPlan == "2000")
                        {
                            bot.SendMessageRaw($"@{args.Caller} is subscribed to {Credentials.targetStream} on a Tier 2 sub plan");
                        }
                        if (sub.SubPlan == "3000")
                        {
                            bot.SendMessageRaw($"@{args.Caller} is subscribed to {Credentials.targetStream} on a Tier 3 sub plan");
                        }
                    }
                    catch
                    {
                        bot.SendMessageRaw($"@{args.Caller} is not subscribed to {Credentials.targetStream}");
                    }
                }
                else
                {
                    if (bot.coinSystem.CheckKey(args.Arguments[0]))
                    {
                        try
                        {
                            var sub = await TwitchBot.api.V5.Channels.CheckChannelSubscriptionByUserAsync(await bot.GetUserID(Credentials.targetStream), await bot.GetUserID(args.Arguments[0]), Credentials.authToken);
                            if (sub.SubPlan == "1000" || sub.SubPlan == "Prime")
                            {
                                bot.SendMessageRaw($"@{args.Arguments[0]} is subscribed to {Credentials.targetStream} on a Tier 1 sub plan");
                            }
                            if (sub.SubPlan == "2000")
                            {
                                bot.SendMessageRaw($"@{args.Arguments[0]} is subscribed to {Credentials.targetStream} on a Tier 2 sub plan");
                            }
                            if (sub.SubPlan == "3000")
                            {
                                bot.SendMessageRaw($"@{args.Arguments[0]} is subscribed to {Credentials.targetStream} on a Tier 3 sub plan");
                            }
                        }
                        catch
                        {
                            bot.SendMessageRaw($"@{args.Arguments[0]} is not subscribed to {Credentials.targetStream}");
                        }
                    }
                }
                return 5;
            }, "Checks whether a user is subscribed to the broadcaster. Syntax: !subcheck or !subcheck [username]\n"));
            chatCommands.Add("subcount", new Command(async (args) =>
            {
                if (args.isBroadcaster || args.isModerator || args.isOwner)
                {
                    var subList = await TwitchBot.api.V5.Channels.GetAllSubscribersAsync(await bot.GetUserID(Credentials.targetStream), Credentials.authToken);
                    bot.SendMessage($"@{args.Caller}, @{Credentials.targetStream} currently has {subList.Count} subs.");
                }
                return 0;
            }, ""));
            chatCommands.Add("quote", new Command((args) => 
            {
                if(args.Arguments.Count == 0)
                {
                    if(quotes.Count > 0)
                    {
                        Random random = new Random();
                        var tempList = quotes.Keys.ToList();
                        int key = tempList[random.Next(tempList.Count)];
                        bot.SendMessageRaw($"{key}. {quotes[key]}");
                    }
                    return Task.Run(() => { return 4; });
                }
                if(int.TryParse(args.Arguments[0], out int id) && quotes.ContainsKey(id))
                {
                    bot.SendMessageRaw($"{id}. {quotes[id]}");
                    return Task.Run(() => { return 4; });
                }
                else if(args.Arguments[0].ToLower() == "list")
                {
                    string paste = "";
                    foreach(var q in quotes)
                    {
                        paste += $"{q.Key}. {q.Value}\n";
                    }
                    string pasteLink = bot.PasteOnPasteBin(paste, "IggiBot Quote list").GetAwaiter().GetResult();
                    bot.SendMessageRaw($"@{args.Caller} quote list: {pasteLink}");
                    return Task.Run(() => { return 1; });
                }
                else if((args.isModerator || args.isBroadcaster) && args.Arguments.Count >= 2)
                {
                    if(args.Arguments[0].ToLower() == "add")
                    {
                        int num = NewQuoteNumber();
                        quotes.Add(num, args.ArgumentsAsString.Substring(4));
                        SaveQuotes();
                        bot.SendMessageRaw($"@{args.Caller} added quote #{num}: {quotes[num]}");
                    }
                    else if(args.Arguments[0].ToLower() == "edit")
                    {
                        if(int.TryParse(args.Arguments[1], out int num) && quotes.ContainsKey(num))
                        {
                            string newQuote = "";
                            for(int i = 2; i < args.Arguments.Count; i++)
                            {
                                newQuote += args.Arguments[i] + " ";
                            }
                            newQuote = newQuote.TrimEnd();
                            if (newQuote.Length > 0)
                            {
                                quotes[num] = newQuote;
                                bot.SendMessageRaw($"@{args.Caller} edited quote #{num}: {quotes[num]}");
                                SaveQuotes();
                            }
                            else
                            {
                                bot.SendMessageRaw($"@{args.Caller} did you mean !quote delete {num}?");
                            }
                        }
                        else
                        {
                            bot.SendMessageRaw($"@{args.Caller} unrecognized quote number");
                        }
                    }
                    else if(args.Arguments[0].ToLower() == "remove" || args.Arguments[0].ToLower() == "delete" || args.Arguments[0].ToLower() == "del")
                    {
                        if(int.TryParse(args.Arguments[1], out int removeID) && quotes.ContainsKey(removeID))
                        {
                            quotes.Remove(removeID);
                            SaveQuotes();
                        }
                        else
                        {
                            bot.SendMessageRaw($"@{args.Caller} unrecognized quote number");
                        }
                    }
                    return Task.Run(() => { return 0; });
                }
                else
                {
                    int search = SearchForQuote(args.ArgumentsAsString);
                    if(search > 0)
                    {
                        bot.SendMessageRaw($"{search}. {quotes[search]}");
                        return Task.Run(() => { return 4; });
                    }
                    else
                    {
                        return Task.Run(() => { return 0; });
                    }
                }
            }, "Shows a random quote. Syntax: !quote [quote number]. Posts quote list on pastebin. Syntax: !quote [list]. Adds, edits or removes quotes. Moderator only. Syntax: !quote [add] [text], !quote [edit] [quote number] [new text], !quote [remove/delete] [quote number]\n"));
        }

        void FillWhisperCommands()
        {
            whisperCommands.Add("average", new Command((args) =>
            {
                if(args.isBroadcaster || args.isOwner)
                {
                    int count = 0;
                    long value = 0;
                    long time = 0;
                    int minimumTime = 240;
                    foreach(var u in bot.coinSystem.userStats)
                    {
                        if(u.Value.time >= minimumTime)
                        {
                            count++;
                            value += u.Value.coins;
                            time += u.Value.time;
                        }
                    }
                    value /= count;
                    time /= count;
                    TimeSpan timeSpan = TimeSpan.FromMinutes(time);
                    bot.SendWhisper(args.Caller, $"The average user has {value} coins and spent {timeSpan.Hours}h {timeSpan.Minutes}m in chat. Stats based on {count} users");
                }
                return Task.Run(() => { return 0; });
            }));
            whisperCommands.Add("top", new Command(async (args) =>
            {
                int count = 10;
                if(args.Arguments.Count > 0)
                {
                    if(!int.TryParse(args.Arguments[0], out count))
                    {
                        count = 10;
                    }
                }
                var topX = bot.coinSystem.userStats.ToList();
                topX.Sort((x, y) => { return y.Value.coins - x.Value.coins; });
                string msg = "";
                for(int i = 0; i < count; i++)
                {
                    msg += $"{topX[i].Key}: {topX[i].Value.coins}\n";
                }
                var paste = await bot.PasteOnPasteBin(msg, $"Top {count}");
                bot.SendWhisperRaw(args.Caller, paste);
                return 3;
            }));
            whisperCommands.Add("transfer", new Command((args) =>
            {
                if (args.isOwner || args.isBroadcaster)
                {
                    if (args.Arguments.Count >= 2)
                    {
                        if(bot.coinSystem.CheckKey(args.Arguments[0]) && bot.coinSystem.CheckKey(args.Arguments[1]))
                        {
                            bot.coinSystem.userStats[args.Arguments[1].ToLower()] += bot.coinSystem.userStats[args.Arguments[0].ToLower()];
                            bot.coinSystem.userStats.Remove(args.Arguments[0].ToLower());
                            bot.SendWhisper(args.Caller, $"Successfully moved {args.Arguments[0]}'s stats over to {args.Arguments[1]} and removed the former from the base");
                        }
                        else
                        {
                            bot.SendWhisper(args.Caller, $"One or more arguments weren't present in the database");
                        }
                        return Task.Run(() => { return 0; });
                    }
                    else
                    {
                        return Task.Run(() => { return 0; });
                    }
                }
                else
                {
                    return Task.Run(() => { return 0; });
                }
            }));
            whisperCommands.Add("morethan", new Command((args) =>
            {
                if (args.Arguments.Count < 1) return Task.Run(() => { return 0; });
                if (int.TryParse(args.Arguments[0], out int x))
                {
                    int result = 0;
                    foreach (var l in bot.coinSystem.userStats)
                    {
                        if (l.Value.coins >= x)
                            result++;
                    }
                    bot.SendWhisper(args.Caller, result.ToString());
                    return Task.Run(() => { return 5; });
                }
                else
                {
                    return Task.Run(() => { return 0; });
                }
            }));
            whisperCommands.Add("sendmsg", new Command((args) =>
            {
                if(args.isBroadcaster || args.isModerator || args.isOwner)
                {
                    bot.SendMessageRaw(args.ArgumentsAsString);
                }
                return Task.Run(() => { return 0; });
            }));
            whisperCommands.Add("tryrefresh", new Command(async (args) =>
            {
                if(args.isOwner)
                {
                    bot.SendWhisper(args.Caller, "Starting refresh");
                    try
                    {
                        var botTokens = await TwitchBot.api.V5.Auth.RefreshAuthTokenAsync(Credentials.botRefreshToken, "0osemzvzybmaunf9tvz78iag3sfhlw", TwitchBot.api.Settings.ClientId);
                        var authTokens = await TwitchBot.api.V5.Auth.RefreshAuthTokenAsync(Credentials.authRefreshToken, "0osemzvzybmaunf9tvz78iag3sfhlw", TwitchBot.api.Settings.ClientId);
                        Credentials.UpdateTokens(bot.credentialsPath, botTokens.AccessToken, authTokens.AccessToken, botTokens.RefreshToken, authTokens.RefreshToken);
                        bot.SendWhisper(args.Caller, "Successfully refreshed");
                    }
                    catch (Exception ex)
                    {
                        bot.SendWhisper(args.Caller, $"Error during refresh: {ex.Message}");
                        bot.LogError("TryRefresh", $"Error message: {ex.Message}");
                    }
                }
                return 0;
            }));
        }

        private void ChatCommandHandler(object sender, TwitchLib.Client.Events.OnChatCommandReceivedArgs e)
        {
            try
            {
                if (chatCommands.TryGetValue(e.Command.CommandText, out Command c))
                {
                    c.Invoke(e).GetAwaiter().GetResult();
                    if (e.Command.ChatMessage.IsModerator || e.Command.ChatMessage.IsBroadcaster || e.Command.ChatMessage.Username.ToLower() == "iggnaccy")
                    {
                        bot.Log($"{e.Command.ChatMessage.Username} used {e.Command.CommandText} command with arguments {e.Command.ArgumentsAsString}");
                    }
                }
            }
            catch(Exception ex)
            {
                bot.LogError("Command - ChatCommandHandler",$"Error occured during {e.Command.CommandText} command: {ex.Message}");
            }
        }

        int NewQuoteNumber()
        {
            var list = quotes.Keys.ToList();
            return list[list.Count - 1] + 1;
        }

        int SearchForQuote(string quote)
        {
            var list = quotes.Keys.ToList();
            for(int i = 0; i < list.Count; i++)
            {
                if(quotes[list[i]].ToLower().Contains(quote.ToLower()))
                {
                    return list[i];
                }
            }
            return -1;
        }

        void LoadQuotes()
        {
            var lines = quoteText.ReadAllLines();
            foreach(var l in lines)
            {
                var split = l.Split(new[] { '&' });
                quotes.Add(int.Parse(split[0]), split[1]);
            }
        }

        void SaveQuotes()
        {
            List<string> write = new List<string>();
            foreach(var q in quotes)
            {
                write.Add($"{q.Key}&{q.Value}");
            }
            quoteText.WriteAllLines(write);
        }

        private void LoadCommands()
        {
            var list = commandText.ReadAllLines();
            foreach(var l in list)
            {
                var split = l.Split(new[] { '=' });
                bot.personalizedCommands.customCommands.Add(split[0], bot.personalizedCommands.CreateCustomCommand(split[1]));
            }
        }

        private void SaveCommands()
        {
            List<string> write = new List<string>();
            foreach(var c in bot.personalizedCommands.customCommands)
            {
                write.Add($"{c.Key}={c.Value.info}");
            }
            commandText.WriteAllLines(write);
        }
    }
}
