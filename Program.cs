using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MusicBot
{
    public class Program
    {
        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        private DiscordSocketClient _client;
        private CommandService _commands;
        private CommandHandler _handler;

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _commands = new CommandService();
            _handler = new CommandHandler(_client, _commands);

            // Install commands
            await _handler.InstallCommandsAsync();

            // Hook client log to to Log event handler from below
            _client.Log += Log;
            // Bot token stored in config.json
            string token = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("config.json")).BotToken;

            // Start up client
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }
        private Task Log(LogMessage msg)
        {
            Console.WriteLine($"[ {DateTime.Now,0:t} ] [{msg.Severity,8}] {msg.Message}");
            return Task.CompletedTask;
        }

    }
}
