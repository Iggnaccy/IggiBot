using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Api.V5.Models.Subscriptions;

namespace IggiBot4
{
    class SubscriberCache
    {
        Dictionary<string, Subscription> subs;
        TwitchBot bot;

        public SubscriberCache(TwitchBot bot)
        {
            subs = new Dictionary<string, Subscription>();
            this.bot = bot;
            LoadAllSubs();
        }

        void LoadAllSubs()
        {
            var list = bot.GetSubscriptions().GetAwaiter().GetResult();
            foreach(var s in list)
            {
                if (subs.ContainsKey(s.User.Name.ToLower())) continue;
                subs.Add(s.User.Name.ToLower(), s);
            }
        }

        public async Task<int> GetOrCreate(string username)
        {
            if(subs.TryGetValue(username.ToLower(), out Subscription sub))
            {
                if(CheckValidity(sub))
                {
                    return GetTier(sub);
                }
                else
                {
                    try
                    {
                        var newSub = await TwitchBot.api.V5.Channels.CheckChannelSubscriptionByUserAsync(await bot.GetUserID(Credentials.targetStream), await bot.GetUserID(username), authToken: Credentials.authToken);
                        subs[username.ToLower()] = newSub;
                        return GetTier(newSub);
                    }
                    catch
                    {
                        subs.Remove(username.ToLower());
                        return 0;
                    }
                }
            }
            else
            {
                try
                {
                    var newSub = await bot.GetUserSubscription(username);
                    subs.Add(username.ToLower(), newSub);
                    return GetTier(newSub);
                }
                catch
                {
                    return 0;
                }
            }
        }

        int GetTier(Subscription sub)
        {
            if (sub.SubPlan == "1000" || sub.SubPlan == "Prime")
            {
                return 1;
            }
            if (sub.SubPlan == "2000")
            {
                return 2;
            }
            if (sub.SubPlan == "3000")
            {
                return 3;
            }
            return 0;
        }

        bool CheckValidity(Subscription sub)
        {
            if (sub == null) return false;
            return (sub.CreatedAt.AddDays(30).Subtract(DateTime.Now) > TimeSpan.Zero);
        }
    }
}
