﻿namespace Microsoft.ApplicationInsights.Metrics
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Metrics.Extensibility;

    /// <summary>ToDo: Complete documentation before stable release.</summary>
    public static class TelemetryConfigurationExtensions
    {
        private static MetricManager defaultMetricManager = null;
        private static ConditionalWeakTable<TelemetryConfiguration, MetricManager> metricManagers = null;

        /// <summary>ToDo: Complete documentation before stable release.</summary>
        /// <param name="telemetryPipeline">ToDo: Complete documentation before stable release.</param>
        /// <returns>ToDo: Complete documentation before stable release.</returns>
        public static MetricManager GetMetricManager(this TelemetryConfiguration telemetryPipeline)
        {
            if (telemetryPipeline == null)
            {
                return null;
            }

            // Fast path for the default configuration:
            if (telemetryPipeline == TelemetryConfiguration.Active)
            {
                MetricManager manager = defaultMetricManager;
                if (manager == null)
                {
                    var pipelineAdapter = new ApplicationInsightsTelemetryPipeline(telemetryPipeline);
                    MetricManager newManager = new MetricManager(pipelineAdapter);
                    MetricManager prevManager = Interlocked.CompareExchange(ref defaultMetricManager, newManager, null);

                    if (prevManager == null)
                    {
                        return newManager;
                    }
                    else
                    {
                        Task fireAndForget = newManager.StopDefaultAggregationCycleAsync();
                        return prevManager;
                    }
                }

                return manager;
            }

            // Ok, we have a non-default config. Get the table:

            ConditionalWeakTable<TelemetryConfiguration, MetricManager> managers = metricManagers;
            if (managers == null)
            {
                ConditionalWeakTable<TelemetryConfiguration, MetricManager> newTable = new ConditionalWeakTable<TelemetryConfiguration, MetricManager>();
                ConditionalWeakTable<TelemetryConfiguration, MetricManager> prevTable = Interlocked.CompareExchange(ref metricManagers, newTable, null);
                managers = prevTable ?? newTable;
            }

            // Get the manager from the table:
            {
                MetricManager manager = GetOrGreateFromTable(telemetryPipeline, managers);
                return manager;
            }
        }

        private static MetricManager GetOrGreateFromTable(
                                                TelemetryConfiguration telemetryPipeline,
                                                ConditionalWeakTable<TelemetryConfiguration, MetricManager> metricManagers)
        {
            MetricManager createdManager = null;

            MetricManager chosenManager = metricManagers.GetValue(
                                                            telemetryPipeline,
                                                            (tp) =>
                                                            {
                                                                createdManager = new MetricManager(new ApplicationInsightsTelemetryPipeline(tp));
                                                                return createdManager;
                                                            });

            // If there was a race and we did not end up returning the manager we just created, we will notify it to give up its agregation cycle thread.
            if (createdManager != null && false == Object.ReferenceEquals(createdManager, chosenManager))
            {
                Task fireAndForget = createdManager.StopDefaultAggregationCycleAsync();
            }

            return chosenManager;
        }
    }
}
