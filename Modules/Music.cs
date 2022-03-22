using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;

namespace MusicBot.Modules
{
    public class Music : ModuleBase<SocketCommandContext>
    {
        private readonly AudioService _service;

        public Music(AudioService service)
        {
            _service = service;
        }

        // You *MUST* mark these commands with 'RunMode.Async'
        // otherwise the bot will not respond until the Task times out.
        [Command("join", RunMode = RunMode.Async)]
        [Alias("summon")]
        [Summary("Request the bot to join the voice channel that the user is in.")]
        public async Task JoinCmd()
        {
            Console.WriteLine($"[ {DateTime.Now,0:t} ] Joining Voice Channel: {(Context.User as IVoiceState).VoiceChannel.Name}");
            await _service.JoinAudioChannelAsync(Context.Guild, (Context.User as IVoiceState).VoiceChannel);
        }

        [Command("autoplay", RunMode = RunMode.Async)]
        [Alias("auto")]
        [Summary("Start the music autoplay system.")]
        public async Task AutoPlay()
        {
            Console.WriteLine($"[ {DateTime.Now,0:t} ] Autoplay started");
            await _service.StartPlaying(Context.Guild as SocketGuild, Context.Client, (Context.User as IVoiceState).VoiceChannel);
        }

        [Command("leave", RunMode = RunMode.Async)]
        [Alias("bye")]
        [Summary("Kick the bot from voice.")]
        public async Task LeaveCmd()
        {
            Console.WriteLine($"[ {DateTime.Now,0:t} ] Leaving voice channel.");
            await Context.Client.SetGameAsync("");
            await _service.LeaveAudioChannelAsync(Context.Guild);
        }

        [Command("play", RunMode = RunMode.Async)]
        [Alias("p", "request")]
        [Summary("Request a song to be played from the bot. Must be a valid YouTube link.")]
        public async Task PlayCmd([Remainder] string url)
        {
            Console.WriteLine($"[ {DateTime.Now,0:t} ] Adding to requests list");
            bool added = await _service.AddToPlaylistAsync(url);
            if (added)
                await ReplyAsync("Added song to requests playlist.");
            else
                await ReplyAsync($"Error, could not find video at url: {url}");
            if (!_service.IsPlaying)
            {
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Audio service not currently playing music, starting music.");
                await _service.StartPlaying(Context.Guild as SocketGuild, Context.Client, (Context.User as IVoiceState).VoiceChannel);
            }
        }

        [Command("stop", RunMode = RunMode.Async)]
        [Summary("Stop the music that is being played.")]
        public async Task StopCmd()
        {
            if (_service.IsPlaying)
            {
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Stopping music");
                await Context.Client.SetGameAsync("");
                _service.StopPlaying();
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Music stopped");
            }
            else
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Stop called but no music being played.");
        }


        [Command("skip", RunMode = RunMode.Async)]
        [Summary("Skip the current song that is being played.")]
        public async Task SkipCmd()
        {
            if (_service.IsPlaying)
            {
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Skipping music");
                _service.SkipSong();
            }
            else
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Skip called but no music being played.");
        }

        [Command("nowplaying")]
        [Alias("np")]
        [Summary("Get information about the song that is currently playing.")]
        public async Task NowPlayingCmd()
        {
            if (_service.IsPlaying)
            {
                await ReplyAsync("**Now playing:** " + _service.CurrentlyPlaying);
            }
            else
            {
                await ReplyAsync("Bot is not playing anything.");
            }
        }
    }
}
