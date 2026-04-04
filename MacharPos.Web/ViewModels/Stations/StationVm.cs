using System;

namespace MacharPos.Web.ViewModels.Stations
{
    public class StationVm
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }
}
