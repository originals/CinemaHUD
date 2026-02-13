using System;
using System.Collections.Generic;
using Blish_HUD;
using Newtonsoft.Json;

namespace CinemaModule.Models
{
    public class SavedLocation
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public WorldPosition3D Position { get; set; }

        public float ScreenWidth { get; set; } = 10f;

        public DateTime CreatedAt { get; set; }

        public SavedLocation(string name, WorldPosition3D position, float screenWidth)
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            CreatedAt = DateTime.UtcNow;
            Name = name;
            Position = position;
            ScreenWidth = screenWidth;
        }
    }

    public class SavedLocationCollection
    {
        public List<SavedLocation> Locations { get; set; } = new List<SavedLocation>();
    }
}
