using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace commons.services.Saga
{
    /**
     * CollaboratorSagaWorker的后台定时任务
     */
    public class CollaboratorSagaWorkerBgTask : BackgroundService
    {
        private readonly ILogger<CollaboratorSagaWorkerBgTask> _logger;

        private Timer _timer;
        private CollaboratorSagaWorker _worker;

        public CollaboratorSagaWorkerBgTask(ILogger<CollaboratorSagaWorkerBgTask> logger,
            CollaboratorSagaWorker worker)
        {
            this._logger = logger;
            this._worker = worker;
        }

        protected void DoWork(object state)
        {
            var limit = 100;
            ThreadPool.QueueUserWorkItem(async (s) =>
            {
                await _worker.DoWork();
            });
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _timer = new Timer(DoWork, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            }
            catch (Exception e)
            {
                _logger.LogError($"execute collaborator saga worker error {e.Message}");
            }
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Dispose();
        }
    }
}
