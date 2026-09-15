using System;
using System.Windows;
using System.Threading.Tasks;

namespace UltraYoutubeDownloader
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ── Globalni hendler za nepredviđene greške ──────────────────────
            // Isti princip kao u UltraVideoEditor-u: bez ovoga, ako nešto neočekivano
            // pukne, aplikacija se tiho ugasi — sighted korisnik bar vidi da je prozor
            // nestao, ali blind korisnik nema NIKAKAV signal da se bilo šta desilo.
            // Ovo makar pokuša da prikaže grešku i nastavi rad, umesto potpune tišine.
            DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    string msg = $"An unexpected error occurred: {args.Exception.Message}\n\n" +
                                 "The application will try to keep running.";
                    System.Windows.MessageBox.Show(msg, "Unexpected error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { /* i sam MessageBox teoretski može da pukne — ne dozvoli da to sakrije originalnu grešku */ }

                // Handled = true znači "ne gasi aplikaciju".
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                try
                {
                    var ex = args.ExceptionObject as Exception;
                    System.Windows.MessageBox.Show(
                        $"A serious error occurred: {ex?.Message}\n\nThe application may need to close.",
                        "Serious error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                // Greške iz "fire and forget" async zadataka koje niko nije čekao (await) —
                // bez ovoga nestaju potpuno bez traga.
                args.SetObserved();
            };
        }
    }
}
