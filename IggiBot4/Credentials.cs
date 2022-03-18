using System;
using System.Collections.Generic;
using System.Text;

namespace IggiBot4
{
    //All credentials supplied by the end user
    public static class Credentials
    {
        public static string authToken;
        public static string botToken;
        public static string botUsername;
        public static string clientId;
        public static string currencyName;
        public static string botRefreshToken;
        public static string authRefreshToken;
        public static string targetStream;

        //Read credentials from file
        public static void ReadCredentials(string absolutePath)
        {
            TxtFile file = new TxtFile(absolutePath);
            var lines = file.ReadAllLines();
            for(int i = 0; i < lines.Count; i++)
            {
                var split = lines[i].Split(new[] { ':' });
                switch(split[0].ToLower())
                {
                    case "botname":
                    case "botusername":
                        {
                            botUsername = split[1];
                        }
                        break;
                    case "bottoken":
                        {
                            botToken = split[1];
                        }
                        break;
                    case "destination":
                        {
                            targetStream = split[1];
                        }
                        break;
                    case "clientid":
                        {
                            clientId = split[1];
                        }
                        break;
                    case "authtoken":
                    case "authenticationtoken":
                        {
                            authToken = split[1];
                        }
                        break;
                    case "currencyname":
                        {
                            currencyName = split[1];
                        }
                        break;
                    case "botrefreshtoken":
                        {
                            botRefreshToken = split[1];
                        }
                        break;
                    case "authrefreshtoken":
                        {
                            authRefreshToken = split[1];
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        public static void UpdateTokens(string absolutePath, string newBotToken = null, string newAuthToken = null, string newBotRefreshToken = null, string newAuthRefreshToken = null)
        {
            if (newBotToken != null)
            {
                botToken = newBotToken;
                botRefreshToken = newBotRefreshToken;
            }
            if (newAuthToken != null)
            {
                authToken = newAuthToken;
                authRefreshToken = newAuthRefreshToken;
            }
            SaveCredentials(absolutePath);
        }

        public static void SaveCredentials(string absolutePath)
        {
            TxtFile file = new TxtFile(absolutePath);
            List<string> write = new List<string>
            {
                $"Destination:{targetStream}",
                $"BotUsername:{botUsername}",
                $"BotToken:{botToken}",
                $"BotRefreshToken:{botRefreshToken}",
                $"AuthToken:{authToken}",
                $"AuthRefreshToken:{authRefreshToken}",
                $"ClientId:{clientId}",
                $"CurrencyName:{currencyName}"
            };
            file.WriteAllLines(write);
        }
    }
}
