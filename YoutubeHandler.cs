using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace MusicBot
{
    public class YoutubeHandler : IYoutubeHandler
    {
        private YoutubeClient _ytClient;
        private readonly string _playlistId;

        public YoutubeHandler()
        {
            _ytClient = new YoutubeClient();
            _playlistId = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("config.json")).PlaylistId;
        }

        public async Task<ConcurrentQueue<string>> AddToPlaylistAsync(string url, ConcurrentQueue<string> playlist)
        {
            if (await IsValidVideoUrl(url))
            {
                playlist.Enqueue(url);
                Console.WriteLine($"[ {DateTime.Now,0:t} ] {url} added, {playlist.Count} songs in the user requests list.");
                return playlist;
            }
            Console.WriteLine($"[ {DateTime.Now,0:t} ] Error, could not find song at url: {url}");
            return null;
        }

        public async Task<ConcurrentQueue<string>> CreateAutoPlaylistAsync(string playlistId, ConcurrentQueue<string> autoPlaylist)
        {
            try
            {
                string id = string.Empty;
                if (!String.IsNullOrEmpty(playlistId) && await IsValidPlaylistId(playlistId))
                    id = playlistId;
                else if (!String.IsNullOrEmpty(_playlistId) && await IsValidPlaylistId(_playlistId))
                    id = _playlistId;
                if (!String.IsNullOrEmpty(id))
                {
                    // get list of videos in playlist and shuffle them
                    var videos = await _ytClient.Playlists.GetVideosAsync(id);
                    var rng = new Random();
                    foreach (var video in videos.OrderBy(x => rng.Next()))
                    {
                        autoPlaylist.Enqueue($"https://youtu.be/{video.Id}");
                    }
                    return autoPlaylist;
                }
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Error creating auto playlist. No valid playlist IDs found.");
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Exception while creating playlist from Youtube: {e}");
                return null;
            }
        }

        public async Task<string> GetVideoTitle(string url)
        {
            if (await IsValidVideoUrl(url))
            {
                var info = await _ytClient.Videos.GetAsync(url);
                return info.Title;
            }
            return null;
        }

        public async Task<bool> IsValidVideoUrl(string url)
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

        public async Task<bool> IsValidPlaylistId(string playlistId)
        {
            try
            {
                var info = await _ytClient.Playlists.GetAsync(playlistId);
                if (info != null)
                    return true;
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ {DateTime.Now,0:t} ] Exception while checking if playlist exists: {e}");
                return false;
            }
        }
    }
}
