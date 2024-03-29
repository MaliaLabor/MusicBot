﻿using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace MusicBot
{
    public class AudioService
    {
        private readonly ConcurrentDictionary<ulong, IAudioClient> _connectedChannels = new ConcurrentDictionary<ulong, IAudioClient>();
        private ConcurrentQueue<string> _userRequests;
        private ConcurrentQueue<string> _autoPlaylist;
        private string _playlistId;
        private CancellationTokenSource _tokenSource;
        private CancellationToken _cancellationToken;
        private YoutubeClient _ytClient;
        private AudioOutStream _audioStream;

        public bool IsPlaying { get; set; }
        public string CurrentlyPlaying { get; set; }

        public AudioService()
        {
            _ytClient = new YoutubeClient();
            _userRequests = new ConcurrentQueue<string>();
            _autoPlaylist = new ConcurrentQueue<string>();
            _playlistId = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("config.json")).PlaylistId;
            IsPlaying = false;
        }

        public async Task<bool> AddToPlaylistAsync(string url)
        {
            if (await VideoExistsAsync(url))
            {
                _userRequests.Enqueue(url);
                Console.WriteLine($"[ {DateTime.Now,0:t} ] {url} added, {_userRequests.Count} songs in the user requests list.");
                return true;
            }
            Console.WriteLine($"[ {DateTime.Now,0:t} ] Error, could not find song at url: {url}");
            return false;
        }

        public async Task CreateAutoPlaylistAsync()
        {
            try
            {
                if (_playlistId != null)
                {
                    // get list of videos in playlist and shuffle them
                    var videos = await _ytClient.Playlists.GetVideosAsync(_playlistId);
                    var rng = new Random();
                    foreach (var video in videos.OrderBy(x => rng.Next()))
                    {
                        _autoPlaylist.Enqueue($"https://youtu.be/{video.Id}");
                    }
                }
                else
                    Console.WriteLine($"[ {DateTime.Now,0:t} ] Error getting playlist ID from config file.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Exception while creating playlist from Youtube: {e}");
            }
        }

        public async Task StartPlaying(SocketGuild guild, DiscordSocketClient discordClient, IVoiceChannel channel)
        {
            IAudioClient client;
            if (!_connectedChannels.TryGetValue(guild.Id, out client))
                await JoinAudioChannelAsync(guild, channel);
            if (_connectedChannels.TryGetValue(guild.Id, out client))
            {
                // set or reset cancellation token
                _tokenSource = new CancellationTokenSource();
                _cancellationToken = _tokenSource.Token;
                IsPlaying = true;
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Creating Audio Stream");
                if (_audioStream != null)
                {
                    await _audioStream.ClearAsync(new CancellationToken());
                    await _audioStream.DisposeAsync();
                    await _audioStream.FlushAsync();
                    _audioStream = null;
                }
                _audioStream = client.CreatePCMStream(AudioApplication.Music, 96 * 1024); // default bitrate is 96 * 1024 if not specified
                bool autoPlaylistHasSongs = true;
                bool requestsPlaylistHasSongs = true;
                string autoplayUrl;
                string requestUrl;
                // check if auto playlist has songs in it, if not create playlist from config
                autoPlaylistHasSongs = _autoPlaylist.TryDequeue(out autoplayUrl);
                if (!autoPlaylistHasSongs)
                {
                    Console.WriteLine($"[ {DateTime.Now,0:t} ] Retrieving auto playlist from Youtube.");
                    await CreateAutoPlaylistAsync();
                    Console.WriteLine($"[ {DateTime.Now,0:t} ] Playlist retrieval complete. Number of songs in list: {_autoPlaylist.Count}");
                    autoPlaylistHasSongs = _autoPlaylist.TryDequeue(out autoplayUrl);
                }
                requestsPlaylistHasSongs = _userRequests.TryDequeue(out requestUrl);
                while (IsPlaying && (requestsPlaylistHasSongs || autoPlaylistHasSongs))
                {
                    if (requestsPlaylistHasSongs)
                    {
                        while (IsPlaying && requestsPlaylistHasSongs)
                        {
                            await CheckVideoTitleAsync(requestUrl);
                            await discordClient.SetGameAsync(CurrentlyPlaying);
                            Console.WriteLine($"[ {DateTime.Now,0:t} ] Retrieving: {requestUrl}");
                            await SendAudioAsync(guild, requestUrl);
                            requestsPlaylistHasSongs = _userRequests.TryDequeue(out requestUrl);
                        }
                    }
                    else if (autoPlaylistHasSongs)
                    {
                        //auto playlist has music and user requests are empty
                        while (IsPlaying && autoPlaylistHasSongs && !requestsPlaylistHasSongs)
                        {
                            await CheckVideoTitleAsync(autoplayUrl);
                            await discordClient.SetGameAsync(CurrentlyPlaying);
                            Console.WriteLine($"[ {DateTime.Now,0:t} ] Retrieving {CurrentlyPlaying}");
                            Console.WriteLine($"[ {DateTime.Now,0:t} ] Number of songs left in auto playlist: {_autoPlaylist.Count}");
                            await SendAudioAsync(guild, autoplayUrl);
                            autoPlaylistHasSongs = _autoPlaylist.TryDequeue(out autoplayUrl);
                            //check user requests playlist for new requests
                            requestsPlaylistHasSongs = _userRequests.TryDequeue(out requestUrl);
                        }
                    }
                    else if (!autoPlaylistHasSongs)
                    {
                        Console.WriteLine($"[ {DateTime.Now,0:t} ] Retrieving auto playlist from Youtube.");
                        await CreateAutoPlaylistAsync();
                        Console.WriteLine($"[ {DateTime.Now,0:t} ] Playlist retrieval complete. Number of songs in list: {_autoPlaylist.Count}");
                        autoPlaylistHasSongs = _autoPlaylist.TryDequeue(out autoplayUrl);
                    }
                }
                // clear out stream if loop has stopped
                await _audioStream.ClearAsync(new CancellationToken());
                await _audioStream.DisposeAsync();
                await _audioStream.FlushAsync();
                _audioStream = null;
                IsPlaying = false;
            }
        }

        public void StopPlaying()
        {
            if (IsPlaying)
            {
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Stopping stream.");
                _tokenSource.Cancel();
                IsPlaying = false;
            }
        }

        public void SkipSong()
        {
            if (IsPlaying)
            {
                StopPlaying();
                _tokenSource = new CancellationTokenSource();
                _cancellationToken = _tokenSource.Token;
                IsPlaying = true;
            }
        }

        public async Task JoinAudioChannelAsync(IGuild guild, IVoiceChannel target)
        {
            IAudioClient client;
            if (_connectedChannels.TryGetValue(guild.Id, out client))
                return; // bot already connected to voice
            try
            {
                var audioClient = await target.ConnectAsync();
                if (_connectedChannels.TryAdd(guild.Id, audioClient))
                {
                    Console.WriteLine($"[ {DateTime.Now,0:t} ] Connected to voice in server: {guild.Name}; Voice Channel: {target.Name}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Exception while joining audio channel: {e}");
            }
        }

        public async Task LeaveAudioChannelAsync(IGuild guild)
        {
            IAudioClient client;
            if (_connectedChannels.TryRemove(guild.Id, out client))
            {
                if (IsPlaying)
                {
                    Console.WriteLine($"[ {DateTime.Now,0:t} ] Stopping Music");
                    StopPlaying();
                    IsPlaying = false;
                }
                await client.StopAsync();
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Audio Client stopped; Bot left voice");
            }
        }

        private async Task CheckVideoTitleAsync(string url)
        {
            if (await VideoExistsAsync(url))
            {
                var info = await _ytClient.Videos.GetAsync(url);
                CurrentlyPlaying = info.Title;
            }
            else
                CurrentlyPlaying = null;
        }

        private async Task<bool> VideoExistsAsync(string url)
        {
            try
            {
                var info = await _ytClient.Videos.GetAsync(url);
                if (info != null)
                    return true;
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Exception while checking if video exists: {e}");
                return false;
            }
        }

        private async Task SendAudioAsync(IGuild guild, string url)
        {
            IAudioClient client;
            if (_connectedChannels.TryGetValue(guild.Id, out client))
            {
                try
                {
                    Console.WriteLine($"[ {DateTime.Now,0:t} ] Playing: {CurrentlyPlaying}");
                    var output = CreateStream(url).StandardOutput.BaseStream;
                    await output.CopyToAsync(_audioStream, _cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[ {DateTime.Now,0:t} ] Audio Stream stopped");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ {DateTime.Now,0:t} ] Exception while trying to send audio: {ex.Message}");
                }
                finally
                {
                    await _audioStream.FlushAsync();
                }
            }
        }

        private Process CreateStream(string url)
        {
            Process currentSong = new Process();
            currentSong.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C youtube-dl.exe -o - {url} | ffmpeg -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            currentSong.Start();
            return currentSong;
        }
    }
}
