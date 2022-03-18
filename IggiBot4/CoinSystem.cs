using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IggiBot4
{
    class CoinSystem
    {
        internal Dictionary<string, UserStats> userStats;
        SubscriberCache cache;
        Timer passiveCoins;
        //Timer chatTime;

        TwitchBot bot;
        TxtFile userStatsTxt;
        string backupDirectoryPath;
        int maxCoins = 200000;

        int saveCounter = 1;

        public CoinSystem(TwitchBot Bot, string userStatsPath, string backupDirectoryPath)
        {
            userStats = new Dictionary<string, UserStats>();
            userStatsTxt = new TxtFile(userStatsPath);
            LoadStats();
            this.backupDirectoryPath = backupDirectoryPath;
            bot = Bot;
            cache = new SubscriberCache(bot);
            passiveCoins = new Timer(async (sender) =>
            {
                if (bot.online == false) return;
                var list = await bot.GetChatList();
                if(list != null && list.Count > 0)
                {
                    foreach(var u in list)
                    {
                        int subTier = await cache.GetOrCreate(u.Username);
                        switch(subTier)
                        {
                            case 0:
                                AddCoins(u.Username, 1);
                                break;
                            case 1:
                                AddCoins(u.Username, 2);
                                break;
                            case 2:
                                AddCoins(u.Username, 3);
                                break;
                            case 3:
                                AddCoins(u.Username, 6);
                                break;
                        }
                    }
                    if (saveCounter >= 5)
                    {
                        SaveStats();
                        saveCounter = 0;
                    }
                    saveCounter++;
                }
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            /*chatTime = new Timer(async (sender) =>
            {
                if (bot.online == false) return;
                var list = await bot.GetChatList();
                if (list != null)
                {
                    foreach (var u in list)
                    {
                        AddTime(u.Username, 1);
                    }
                }
            },null,TimeSpan.FromMinutes(1),TimeSpan.FromMinutes(1));
            */
            
        }

        internal void RefreshEvents(TwitchBot Bot)
        {
            bot = Bot;
        }

        public void AddCoins(string user, int value)
        {
            if(userStats.TryGetValue(user.ToLower(), out UserStats us))
            {
                if (us.coins >= maxCoins) return;
                us.coins = Math.Min(us.coins + value, maxCoins);
            }
            else
            {
                userStats.Add(user.ToLower(), new UserStats(value, 0, 0));
            }
        }

        public int GetCoins(string user)
        {
            if (userStats.TryGetValue(user.ToLower(), out UserStats us))
            {
                int uCoins = us.coins;
                /*
                if (uCoins > maxCoins)
                {
                    AddCoins(user, maxCoins - uCoins);
                }*/
                return us.coins;
            }
            else
            {
                userStats.Add(user.ToLower(), new UserStats());
                return 0;
            }
        }

        public void AddTime(string user, int value)
        {
            if (userStats.TryGetValue(user.ToLower(), out UserStats us))
            {
                us.time += value;
            }
            else
            {
                userStats.Add(user.ToLower(), new UserStats(0, value, 0));
            }
        }

        public int GetTime(string user)
        {
            if (userStats.TryGetValue(user.ToLower(), out UserStats us))
            {
                return us.time;
            }
            else
            {
                userStats.Add(user.ToLower(), new UserStats());
                return 0;
            }
        }

        public bool CheckKey(string username)
        {
            return userStats.ContainsKey(username.ToLower());
        }

        public async Task CoinGiveaway(int value)
        {
            var list = await bot.GetChatList();
            foreach(var u in list)
            {
                AddCoins(u.Username, value);
            }
        }

        public void Close()
        {
            SaveStats();
        }

        void LoadStats()
        {
            var lines = userStatsTxt.ReadAllLines();
            foreach(var l in lines)
            {
                var split1 = l.Split(new[] { '=' });
                string username = split1[0];
                var split2 = split1[1].Split(new[] { '.' });
                int coins, time, shards;
                coins = int.Parse(split2[0]);
                time = int.Parse(split2[1]);
                shards = int.Parse(split2[2]);
                if(coins > 0 || time > 0)
                {
                    userStats.Add(username.ToLower(), new UserStats(coins, time, shards));
                }
            }
        }

        internal void SaveStats()
        {
                List<string> write = new List<string>();
                foreach (var u in userStats)
                {
                    write.Add($"{u.Key}={u.Value.coins}.{u.Value.time}.{u.Value.shards}");
                }
                userStatsTxt.WriteAllLines(write);
                BackupStats(write);
        }

        void BackupStats(List<string> write)
        {
            DateTime now = DateTime.Now;
            TxtFile backup = new TxtFile($@"{backupDirectoryPath}\{now.Year}-{now.Month}-{now.Day}-UserStats.ini");
            backup.WriteAllLines(write);
        }
    }

    class UserStats
    {
        internal int coins;
        internal int time;
        internal int shards;

        public UserStats(int coins, int time, int shards)
        {
            this.coins = coins;
            this.time = time;
            this.shards = shards;
        }
        public UserStats()
        {
            coins = time = shards = 0;
        }

        public static UserStats operator+ (UserStats a, UserStats b)
        {
            return new UserStats(a.coins + b.coins, a.time + b.time, a.shards + b.shards);
        }
    }
}
