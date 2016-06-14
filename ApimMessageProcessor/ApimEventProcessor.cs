﻿
namespace ApimMessageProcessor
{
    using Microsoft.ServiceBus.Messaging;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;

    /// <summary>
    /// Accepts EventData from EventHubs, converts to a HttpMessage instances and forwards it to a IHttpMessageProcessor
    /// </summary>
    public class ApimEventProcessor : IEventProcessor
    {
        Stopwatch checkpointStopWatch;
        private ILogger _Logger;
        private IHttpMessageProcessor _MessageContentProcessor;

        public ApimEventProcessor(IHttpMessageProcessor messageContentProcessor, ILogger logger)
        {
            _MessageContentProcessor = messageContentProcessor;
            _Logger = logger;
        }


        async Task IEventProcessor.ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {

            foreach (EventData eventData in messages)
            {
                _Logger.LogInfo(string.Format("Event received from partition: {0} - {1}", context.Lease.PartitionId, eventData.PartitionKey));

                try
                {
                    var httpMessage = HttpMessage.Parse(eventData.GetBodyStream());
                    await _MessageContentProcessor.ProcessHttpMessage(httpMessage);
                }
                catch (Exception ex)
                {
                    _Logger.LogError(ex.Message);
                }
            }

            //Call checkpoint every 5 minutes, so that worker can resume processing from the 5 minutes back if it restarts.
            if (this.checkpointStopWatch.Elapsed > TimeSpan.FromMinutes(5))
            {
                _Logger.LogInfo("Checkpointing");
                await context.CheckpointAsync();
                this.checkpointStopWatch.Restart();
            }
        }



        async Task IEventProcessor.CloseAsync(PartitionContext context, CloseReason reason)
        {
            _Logger.LogInfo("Processor Shutting Down. Partition '{0}', Reason: '{1}'.", context.Lease.PartitionId, reason);
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
            }
        }

        Task IEventProcessor.OpenAsync(PartitionContext context)
        {
            _Logger.LogInfo("SimpleEventProcessor initialized.  Partition: '{0}', Offset: '{1}'", context.Lease.PartitionId, context.Lease.Offset);
            this.checkpointStopWatch = new Stopwatch();
            this.checkpointStopWatch.Start();
            return Task.FromResult<object>(null);
        }

    }
}
