using Quarry.Models.Persistence;
using System;

namespace Quarry.Interfaces
{
    public interface IPersistenceService : IDisposable
    {
        event Action AutoSave;

        Storage Get();

        void Save(int achievementTrackWindowLocationX, int achievementTrackWindowLocationY, bool showTrackWindow, int trackWindowCompactWidth, int trackWindowCompactHeight, int overviewWindowWidth = -1, int overviewWindowHeight = -1);

        void Reload();
    }
}