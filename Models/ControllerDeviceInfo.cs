using System;

namespace RoverExplorer1NodoMandoPC.Models
{
    public class ControllerDeviceInfo
    {
        public string Name { get; set; } = "";
        public Guid InstanceGuid { get; set; }
        public override string ToString() => Name;
    }
}
