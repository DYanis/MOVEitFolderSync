using Core;

namespace Application
{
    namespace Application
    {
        public class ApplicationRunner(FileSyncWatcher fileWatcher)
        {
            public async Task RunAsync()
            {
                await fileWatcher.Start();

                Console.WriteLine("Monitoring started. Press 'q' to quit.");

                while (Console.Read() != 'q')
                {
                    await Task.Delay(100);
                }

                fileWatcher.StopWatching();
            }
        }
    }
}
