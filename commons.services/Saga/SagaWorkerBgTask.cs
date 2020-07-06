using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace commons.services.Sagas
{
    public class SagaWorkerBgTask : BackgroundService
    {
        private readonly ILogger<SagaWorkerBgTask> _logger;

        private Timer _timer;
        private string lastProcessSagaId;
        private SagaWorker _worker;

        public SagaWorkerBgTask(ILogger<SagaWorkerBgTask> logger, SagaWorker worker)
        {
            this._logger = logger;
            this._worker = worker;
        }

        protected void DoWork(object state)
        {
            var limit = 100;
            ThreadPool.QueueUserWorkItem(async (s) =>
            {
                lastProcessSagaId = await _worker.ProcessSomeUnfinishedSagasAsync(limit, lastProcessSagaId);
            });
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            }
            catch(Exception e)
            {
                _logger.LogError($"execute saga worker error {e.Message}");
            }
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Dispose();
        }
        }
}
