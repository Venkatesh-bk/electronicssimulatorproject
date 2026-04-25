using CommunityToolkit.Mvvm.ComponentModel;
using EdaSimulator.Engines.Simulation;

namespace EdaSimulator.UI.ViewModels
{
    public partial class SimulationConfigViewModel : ObservableObject
    {
        private readonly SimulationConfiguration _coreConfig;

        public SimulationConfigViewModel(SimulationConfiguration coreConfig)
        {
            _coreConfig = coreConfig;
        }

        public string AnalysisType
        {
            get => _coreConfig.AnalysisType;
            set
            {
                if (_coreConfig.AnalysisType != value)
                {
                    _coreConfig.AnalysisType = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TStep
        {
            get => _coreConfig.TStep;
            set
            {
                if (_coreConfig.TStep != value)
                {
                    _coreConfig.TStep = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TStop
        {
            get => _coreConfig.TStop;
            set
            {
                if (_coreConfig.TStop != value)
                {
                    _coreConfig.TStop = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
