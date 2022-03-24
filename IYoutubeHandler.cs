using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace MusicBot
{
    public interface IYoutubeHandler
    {
        Task<ConcurrentQueue<string>> AddToPlaylistAsync(string url, ConcurrentQueue<string> playlist);
        Task<ConcurrentQueue<string>> CreateAutoPlaylistAsync(string playlistId, ConcurrentQueue<string> autoPlaylist);
        Task<string> GetVideoTitle(string url);
        Task<bool> IsValidPlaylistId(string playlistId);
        Task<bool> IsValidVideoUrl(string url);
    }
}