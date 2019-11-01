using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;

namespace Examples.Bootstrapper
{
    public class MainViewModel : ViewModelBase
    {
        public int ExitCode { get; private set; }
        private Dispatcher _dispatcher;
        private readonly bool _applyAutomatically;

        //constructor
        public MainViewModel(Dispatcher dispatcher, BootstrapperApplication bootstrapper, bool applyAutomatically)
        {
            _dispatcher = dispatcher;
            _applyAutomatically = applyAutomatically;
            this.IsThinking = false;

            this.Bootstrapper = bootstrapper;
            this.Bootstrapper.ApplyComplete += this.OnApplyCompleteUiUpdate;
            this.Bootstrapper.DetectPackageComplete += this.OnDetectPackageCompleteUiUpdate;
            this.Bootstrapper.PlanComplete += this.OnPlanCompleteUiUpdate;
        }

        #region Properties

        private bool installEnabled;
        public bool InstallEnabled
        {
            get { return installEnabled; }
            set
            {
                installEnabled = value;
                OnPropertyChanged();
            }
        }


        private bool uninstallEnabled;
        public bool UninstallEnabled
        {
            get { return uninstallEnabled; }
            set
            {
                uninstallEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool isThinking;
        public bool IsThinking
        {
            get { return isThinking; }
            set
            {
                isThinking = value;
                OnPropertyChanged();
            }
        }

        public BootstrapperApplication Bootstrapper { get; private set; }

        #endregion //Properties

        #region Methods

        internal void DetectExecute()
        {
            this.IsThinking = true;
            Bootstrapper.Engine.Detect();
        }

        private void PlanExecute(LaunchAction action)
        {
            IsThinking = true;
            Bootstrapper.Engine.Plan(action);
        }

        private void ApplyExecute()
        {
            IsThinking = true;
            Bootstrapper.Engine.Apply(IntPtr.Zero);
        }

        internal Task<IEnumerable<DetectPackageCompleteEventArgs>> DetectAsync()
        {
            var packages = new List<DetectPackageCompleteEventArgs>();
            var detectCompleted = new TaskCompletionSource<IEnumerable<DetectPackageCompleteEventArgs>>();
            var packCount = Bootstrapper.Engine.PackageCount;

            void EventHandler(object sender, DetectPackageCompleteEventArgs e)
            {
                packages.Add(e);
                if (packages.Count >= packCount)
                {
                    this.Bootstrapper.DetectPackageComplete -= EventHandler;
                    detectCompleted.SetResult(packages);
                }
            }

            this.Bootstrapper.DetectPackageComplete += EventHandler;
            DetectExecute();

            return detectCompleted.Task;
        }

        internal Task<PlanCompleteEventArgs> PlanAsync(LaunchAction action)
        {
            var planCompleted = new TaskCompletionSource<PlanCompleteEventArgs>();

            void EventHandler(object sender, PlanCompleteEventArgs e)
            {
                this.Bootstrapper.PlanComplete -= EventHandler;
                planCompleted.SetResult(e);
            }

            this.Bootstrapper.PlanComplete += EventHandler;
            PlanExecute(action);

            return planCompleted.Task;
        }

        internal Task<ApplyCompleteEventArgs> ApplyAsync()
        {
            var applyCompleted = new TaskCompletionSource<ApplyCompleteEventArgs>();

            void EventHandler(object sender, ApplyCompleteEventArgs e)
            {
                this.Bootstrapper.ApplyComplete -= EventHandler;
                applyCompleted.SetResult(e);
            }

            this.Bootstrapper.ApplyComplete += EventHandler;
            ApplyExecute();

            return applyCompleted.Task;
        }


        internal void ExitExecute()
        {
            _dispatcher.InvokeShutdown();
        }

        /// <summary>
        /// Method that gets invoked when the Bootstrapper ApplyComplete event is fired.
        /// This is called after a bundle installation has completed. Make sure we updated the view.
        /// </summary>
        private void OnApplyCompleteUiUpdate(object sender, ApplyCompleteEventArgs e)
        {
            ExitCode = e.Status;
            IsThinking = false;
            InstallEnabled = false;
            UninstallEnabled = false;
        }

        /// <summary>
        /// Method that gets invoked when the Bootstrapper DetectPackageComplete event is fired.
        /// Checks the PackageId and sets the installation scenario. The PackageId is the ID
        /// specified in one of the package elements (msipackage, exepackage, msppackage,
        /// msupackage) in the WiX bundle.
        /// </summary>
        private void OnDetectPackageCompleteUiUpdate(object sender, DetectPackageCompleteEventArgs e)
        {
            //if (e.PackageId == "DummyInstallationPackageId")
            //{
                if (e.State == PackageState.Absent)
                    InstallEnabled = true;

                else if (e.State == PackageState.Present)
                    UninstallEnabled = true;
            //}
        }

        /// <summary>
        /// Method that gets invoked when the Bootstrapper PlanComplete event is fired.
        /// If the planning was successful, it instructs the Bootstrapper Engine to 
        /// install the packages.
        /// </summary>
        private void OnPlanCompleteUiUpdate(object sender, PlanCompleteEventArgs e)
        {
            if (e.Status >= 0)
            {
                if (_applyAutomatically)
                {
                    ApplyExecute();
                }
            }
            else
            {
                IsThinking = false;
                ExitCode = e.Status;
            }
        }

        #endregion //Methods

        #region RelayCommands

        private RelayCommand installCommand;
        public RelayCommand InstallCommand
        {
            get
            {
                if (installCommand == null)
                    installCommand = new RelayCommand(() => PlanExecute(LaunchAction.Install), () => InstallEnabled == true);

                return installCommand;
            }
        }

        private RelayCommand uninstallCommand;
        public RelayCommand UninstallCommand
        {
            get
            {
                if (uninstallCommand == null)
                    uninstallCommand = new RelayCommand(() => PlanExecute(LaunchAction.Uninstall), () => UninstallEnabled == true);

                return uninstallCommand;
            }
        }

        private RelayCommand exitCommand;

        public RelayCommand ExitCommand
        {
            get
            {
                if (exitCommand == null)
                    exitCommand = new RelayCommand(() => ExitExecute());

                return exitCommand;
            }
        }
        
        #endregion //RelayCommands

        public void SetError(Exception exception)
        {
            Bootstrapper.Engine.Log(LogLevel.Error, exception.ToString());
            ExitCode = -1;
        }
    }
}