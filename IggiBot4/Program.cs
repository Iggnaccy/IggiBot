using System;
using System.Threading.Tasks;

namespace IggiBot4
{
    class Program
    {
        static async Task Main(string[] args)
        {
            TwitchBot bot = new TwitchBot("rhykkerWindows");
            await Task.Run(() => { Console.ReadLine(); });
            bot.Close();
            Console.ReadLine();
        }
    }
}
