using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IggiBot4
{
    //Command definition
    class Command
    {
        public delegate Task<int> CommandEffect(CommandArgs args);
        
        public CommandEffect effect;

        public DateTime lastUsed;
        public int cooldown;

        public string info;

        public async Task Invoke(TwitchLib.Client.Events.OnChatCommandReceivedArgs args)
        {
            CommandArgs cArgs = new CommandArgs(args);
            if (DateTime.Now < lastUsed.AddSeconds(cooldown))
            {
                if (!(cArgs.isBroadcaster || cArgs.isModerator || cArgs.isOwner))
                {
                    return;
                }
            }
            cooldown = await effect.Invoke(cArgs);
            lastUsed = DateTime.Now;
        }

        public async Task Invoke(CommandArgs args)
        {
            if (DateTime.Now < lastUsed.AddSeconds(cooldown))
            {
                if (!(args.isBroadcaster || args.isModerator || args.isOwner))
                {
                    return;
                }
            }
            cooldown = await effect.Invoke(args);
            lastUsed = DateTime.Now;
        }

        public async Task Invoke(TwitchLib.Client.Events.OnWhisperCommandReceivedArgs args)
        {
            CommandArgs cArgs = new CommandArgs(args);
            if (DateTime.Now < lastUsed.AddSeconds(cooldown))
            {
                if (!(cArgs.isBroadcaster || cArgs.isModerator || cArgs.isOwner))
                {
                    return;
                }
            }
            cooldown = await effect.Invoke(cArgs);
            lastUsed = DateTime.Now;
        }

        public Command(CommandEffect effect, string info = "")
        {
            this.effect = effect;
            this.info = info;
            lastUsed = DateTime.Now;
            cooldown = 0;
        }
    }

    //Command arguments class
    class CommandArgs
    {
        public List<string> Arguments { get; private set; }
        public string ArgumentsAsString { get; private set; }
        public string Caller { get; private set; }
        public string CallerID { get; private set; }
        public bool isModerator { get; private set; }
        public bool isBroadcaster { get; private set; }
        public bool isOwner { get; private set; }

        public CommandArgs(TwitchLib.Client.Events.OnChatCommandReceivedArgs args)
        {
            Arguments = args.Command.ArgumentsAsList;
            ArgumentsAsString = args.Command.ArgumentsAsString;
            Caller = args.Command.ChatMessage.Username;
            CallerID = args.Command.ChatMessage.UserId;
            isModerator = args.Command.ChatMessage.IsModerator;
            isBroadcaster = args.Command.ChatMessage.IsBroadcaster;
            isOwner = args.Command.ChatMessage.Username.ToLower() == "iggnaccy";
        }

        public CommandArgs(TwitchLib.Client.Events.OnWhisperCommandReceivedArgs args)
        {
            Arguments = args.Command.ArgumentsAsList;
            ArgumentsAsString = args.Command.ArgumentsAsString;
            Caller = args.Command.WhisperMessage.Username;
            CallerID = args.Command.WhisperMessage.UserId;
            isModerator = moderators.Contains(Caller.ToLower());
            isBroadcaster = Caller.ToLower() == Credentials.targetStream.ToLower();
            isOwner = Caller.ToLower() == "iggnaccy";
        }

        private static List<string> moderators = new List<string>
        {
            "baaltrb",
            "dudeknext",
            "bunkitia",
            "bumbletrees",
            "kiskaloo",
            "naughtybear_",
            "psyenzfyktshun",
            "theargonaught",
            "leftpiece",
            "alissdigits",
            "soultarith",
            "wumpix",
            "nightbot",
            "theonebritish",
            "jhow",
            "bumble_treez",
            "revlobot",
            "rykrbot",
            "bigdaddyden76",
            "neme5i5"
        };
    }
}
