using System.Collections.Generic;
using OxyPlot;
using OxyPlot.Series;

namespace BeeSwarmGame
{
    public partial class VisualizerWpfWindow : System.Windows.Window
    {
        private readonly Dictionary<string, List<DataPoint>> _timeSeriesData;

        public VisualizerWpfWindow(Dictionary<string, List<DataPoint>> timeSeriesData)
        {
            InitializeComponent();
            _timeSeriesData = timeSeriesData;

            // Configure the OxyPlot chart
            var plotModel = new PlotModel { Title = "Metrics Visualization" };
            var lineSeries = new LineSeries { Title = "Example Metric" };

            // Add example data points
            foreach (var dataPoint in _timeSeriesData["example_metric"])
            {
                lineSeries.Points.Add(new OxyPlot.DataPoint(OxyPlot.Axes.DateTimeAxis.ToDouble(dataPoint.Timestamp), dataPoint.Value));
            }

            plotModel.Series.Add(lineSeries);
            MetricsPlot.Model = plotModel;
        }
    }
}
