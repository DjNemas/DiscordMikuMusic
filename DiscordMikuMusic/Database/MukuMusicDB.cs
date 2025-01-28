using Microsoft.EntityFrameworkCore;

namespace DiscordMikuMusic.Database
{
    internal class MukuMusicDB : DbContext
    {
        public MukuMusicDB(DbContextOptions options) : base(options)
        {
            
        }
    }
}
