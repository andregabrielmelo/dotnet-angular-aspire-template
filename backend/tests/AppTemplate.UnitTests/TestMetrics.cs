using System.Diagnostics.Metrics;
using AppTemplate.UseCases.Telemetry;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace AppTemplate.UnitTests;

/// <summary>
/// A real <see cref="ApplicationMetrics"/> on its own meter factory, so parallel tests never
/// see each other's measurements. <see cref="Collect{T}"/> records one instrument.
/// </summary>
public sealed class TestMetrics
{
    private readonly ScopedMeterFactory _meterFactory = new();

    public TestMetrics() => Application = new ApplicationMetrics(_meterFactory);

    public ApplicationMetrics Application { get; }

    public MetricCollector<T> Collect<T>(string instrument)
        where T : struct => new(_meterFactory, ApplicationMetrics.MeterName, instrument);

    /// <summary>Meters scoped to this factory, which is what MetricCollector filters on.</summary>
    private sealed class ScopedMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(
                new MeterOptions(options.Name)
                {
                    Version = options.Version,
                    Tags = options.Tags,
                    Scope = this,
                }
            );
            _meters.Add(meter);
            return meter;
        }

        public void Dispose() => _meters.ForEach(meter => meter.Dispose());
    }
}
