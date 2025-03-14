namespace Serein.CloudWorkbench.Services
{
    public class CounterService
    {
        public int Count { get; private set; } = 0;

        public event Action? OnCountChanged;

        public void Increment()
        {
            Count++;
            OnCountChanged?.Invoke();
        }

        public void Decrement()
        {
            Count--;
            OnCountChanged?.Invoke();
        }
    }
}
