﻿#define LOG_MEMORY_PERF_COUNTERS 
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;


namespace Orleans.Runtime
{
    internal class ClientTableStatistics : IClientPerformanceMetrics, IDisposable
    {
        private readonly IMessageCenter mc;
        private readonly IClientMetricsDataPublisher metricsDataPublisher;
        private TimeSpan reportFrequency;

        private readonly IntValueStatistic connectedGatewayCount;
        private readonly RuntimeStatisticsGroup runtimeStats;

        private AsyncTaskSafeTimer reportTimer;
        private readonly ILogger logger;
        private readonly ILogger timerLogger;
        internal ClientTableStatistics(IMessageCenter mc, IClientMetricsDataPublisher metricsDataPublisher, RuntimeStatisticsGroup runtime, ILoggerFactory loggerFactory)
        {
            this.mc = mc;
            this.metricsDataPublisher = metricsDataPublisher;
            this.logger = loggerFactory.CreateLogger<ClientTableStatistics>();
            //async timer created through current class all share this logger for perf reasons
            this.timerLogger = loggerFactory.CreateLogger<AsyncTaskSafeTimer>();
            runtimeStats = runtime;
            reportFrequency = TimeSpan.Zero;
            connectedGatewayCount = IntValueStatistic.Find(StatisticNames.CLIENT_CONNECTED_GATEWAY_COUNT);
        }

        #region IClientPerformanceMetrics Members

        public float CpuUsage
        {
            get { return runtimeStats.CpuUsage; }
        }

        public long AvailablePhysicalMemory
        {
            get { return runtimeStats.AvailableMemory; }
        }

        public long MemoryUsage
        {
            get { return runtimeStats.MemoryUsage; }
        }
        public long TotalPhysicalMemory
        {
            get { return runtimeStats.TotalPhysicalMemory; }
        }

        public int SendQueueLength
        {
            get { return mc.SendQueueLength; }
        }

        public int ReceiveQueueLength
        {
            get { return mc.ReceiveQueueLength; }
        }

        public long SentMessages
        {
            get { return MessagingStatisticsGroup.MessagesSentTotal.GetCurrentValue(); }
        }

        public long ReceivedMessages
        {
            get { return MessagingStatisticsGroup.MessagesReceived.GetCurrentValue(); }
        }

        public long ConnectedGatewayCount
        {
            get { return connectedGatewayCount.GetCurrentValue(); }
        }

        public TimeSpan MetricsTableWriteInterval
        {
            get { return reportFrequency; }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    if (reportFrequency > TimeSpan.Zero)
                    {
                        logger.Info(ErrorCode.PerfMetricsStoppingTimer, "Stopping performance metrics reporting with reportFrequency={0}", reportFrequency);
                        if (reportTimer != null)
                        {
                            reportTimer.Dispose();
                            reportTimer = null;
                        }
                    }
                    reportFrequency = TimeSpan.Zero;
                }
                else
                {
                    reportFrequency = value;
                    logger.Info(ErrorCode.PerfMetricsStartingTimer, "Starting performance metrics reporting with reportFrequency={0}", reportFrequency);
                    if (reportTimer != null)
                    {
                        reportTimer.Dispose();
                    }
                    reportTimer = new AsyncTaskSafeTimer(this.timerLogger, this.Reporter, null, reportFrequency, reportFrequency); // Start a new fresh timer. 
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private async Task Reporter(object context)
        {
            try
            {
                if (metricsDataPublisher != null)
                {
                    await metricsDataPublisher.ReportMetrics(this);
                }
            }
            catch (Exception exc)
            {
                var e = exc.GetBaseException();
                logger.Error(ErrorCode.Runtime_Error_100101, String.Format("Exception occurred during metrics reporter."), exc);
            }
        }

        #endregion

        public void Dispose()
        {
            if (reportTimer != null)
                reportTimer.Dispose();
            reportTimer = null;
        }
    }
}
