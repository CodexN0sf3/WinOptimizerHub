namespace WinOptimizerHub.ViewModels
{
    public class PowerViewModel
    {
        public NetworkViewModel Network { get; }

        public PowerViewModel(NetworkViewModel network)
        {
            Network = network;
        }
    }
}
