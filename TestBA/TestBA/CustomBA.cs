using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;

namespace Examples.Bootstrapper
{
    public class CustomBA : BootstrapperApplication
    {
        private MainViewModel ViewModel { get; set; }

        // entry point for our custom UI
        protected override void Run()
        {
            var args = Command.GetCommandLineArgs();
            this.Engine.Log(LogLevel.Verbose, $"Launching CustomBA UX -- \"{string.Join("\" \"", args)}\"");

            // Default implementation of the standard bootstrapper also evaluates if Overridable is true
            SetCommandLineVariables(args);

            var dispatcher = Dispatcher.CurrentDispatcher;

            switch (Command.Display)
            {
                case Display.Embedded:
                case Display.None:
                    Task.Run(() => RunHeadless(dispatcher));
                    break;
                case Display.Unknown:
                case Display.Passive:
                case Display.Full:
                    RunWithUi(dispatcher);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Dispatcher.Run();

            this.Engine.Quit(ViewModel.ExitCode);
        }

        private void SetCommandLineVariables(IEnumerable<string> args)
        {
            foreach (var arg in args)
            {
                var splits = arg.Split('=');
                if (splits.Length > 1)
                {
                    var varname = splits[0];
                    var value = string.Join("=", splits.Skip(1));
                    Engine.StringVariables[varname] = value;
                }
                else
                {
                    Engine.Log(LogLevel.Error, $"Ignoring unknown command line argument '{arg}'");
                }
            }
        }

        private async Task RunHeadless(Dispatcher dispatcher)
        {
            MainViewModel viewModel = new MainViewModel(dispatcher, this, false);
            ViewModel = viewModel;
            try
            {
                var detect = await viewModel.DetectAsync();
                var plan = await viewModel.PlanAsync(Command.Action);
                var apply = await viewModel.ApplyAsync();
            }
            catch (Exception e)
            {
                viewModel.SetError(e);
            }

            viewModel.ExitExecute();
        }

        private void RunWithUi(Dispatcher dispatcher)
        {
            MainViewModel viewModel = new MainViewModel(dispatcher, this, true);
            ViewModel = viewModel;
            viewModel.DetectExecute();

            MainView view = new MainView();
            view.DataContext = viewModel;
            view.Closed += (sender, e) => dispatcher.InvokeShutdown();
            view.Show();
        }
    }
}
