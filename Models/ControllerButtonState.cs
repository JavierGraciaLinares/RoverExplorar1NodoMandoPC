using CommunityToolkit.Mvvm.ComponentModel;

namespace RoverExplorer1NodoMandoPC.Models
{
    public class ControllerButtonState : ObservableObject
    {
        public int Index { get; }
        public string Name { get; }

        private bool _pressed;
        public bool Pressed
        {
            get => _pressed;
            set => SetProperty(ref _pressed, value);
        }

        public ControllerButtonState(int index, string name)
        {
            Index = index;
            Name = name;
        }
    }
}
