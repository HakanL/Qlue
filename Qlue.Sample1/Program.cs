using Microsoft.Practices.Unity;
using Qlue.Logging;
using Qlue.Transport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Qlue.Sample1
{
    public class Program
    {
        private const int RequestSizeInBytes = 100 * 1024;
        private const int ResponseSizeInBytes = 5 * 1024;
        private const int NotifySizeInBytes = 1 * 1024;

        private ILog logConsumer;
        private ILog logService;
        private ILog logNotify;

        private static IUnityContainer container;


        public static void Main(string[] args)
        {
            var app = new Program();
            app.Test();
        }

        private void Test2()
        {
            using (this.logService.Context())
            {
                this.logService.Info("Test2 is executing");
            }
        }

        private void Test()
        {
            container = new UnityContainer();
            container.RegisterInstance<ICloudCredentials>(Shared.AzureCredentials.GetCloudCredentials());
            container.RegisterType<IBlobClient, AzureBlobClient>(
                new ContainerControlledLifetimeManager(),
                new InjectionConstructor(typeof(ICloudCredentials), "Qlue-Overflow"));
            container.RegisterType<ILogFactory, NLogFactoryProvider>(new ContainerControlledLifetimeManager());
            container.RegisterType<IBusTransportFactory, AzureBusTransportFactory>(new ContainerControlledLifetimeManager());

            this.logConsumer = container.Resolve<ILogFactory>().GetLogger("Consumer");
            this.logService = container.Resolve<ILogFactory>().GetLogger("Service");
            this.logNotify = container.Resolve<ILogFactory>().GetLogger("Notify");

            logConsumer.Info("Starting Qlue.Sample/Service on console thread {0}", System.Threading.Thread.CurrentThread.ManagedThreadId);
            logService.Info("Starting Qlue.Sample/Consumer on console thread {0}", System.Threading.Thread.CurrentThread.ManagedThreadId);
            logNotify.Info("Starting Qlue.Sample/Notify on console thread {0}", System.Threading.Thread.CurrentThread.ManagedThreadId);

            IServiceChannel serviceChannel;
            INotifyChannel notifyChannel;

            using (this.logService.Context("Service.Init"))
            {
                Test2();

                // Simulate service
                serviceChannel = new ServiceChannel(
                    container.Resolve<ILogFactory>(),
                    "qlue-service",
                    container.Resolve<IBusTransportFactory>(),
                    container.Resolve<IBlobClient>());

                serviceChannel.RegisterAsyncDispatch<NewAccountRequest, NewAccountResponse>((request, ctx) =>
                {
                    logService.Info("Received *NewAccountRequest* on thread {0}",
                        System.Threading.Thread.CurrentThread.ManagedThreadId);
                    logService.Debug("Customer Name: {0}", request.CustomerName);
                    logService.Debug("Account Number: {0}", request.AccountNumber);
                    logService.Debug("Aux payload size: {0} bytes", request.Data.Length);

                    var response = new NewAccountResponse
                    {
                        ProcessResult = "Good to go"
                    };

                    response.Data = new byte[ResponseSizeInBytes];
                    var random = new Random();
                    random.NextBytes(response.Data);

                    logService.Debug("Response Aux payload size: {0} bytes", response.Data.Length);

                    var notify = new NewAccountNotify
                    {
                        CustomerName = request.CustomerName
                    };
                    ctx.SendNotify(notify);

                    return Task.FromResult(response);
                });

                serviceChannel.RegisterDispatch<NoResponseTestRequest>((request, ctx) =>
                {
                    logService.Info("Received *NoResponseTestRequest* on thread {0}",
                        System.Threading.Thread.CurrentThread.ManagedThreadId);
                    logService.Debug("Customer Name: {0}", request.CustomerName);
                    logService.Debug("Account Number: {0}", request.AccountNumber);
                    logService.Debug("Aux payload size: {0} bytes", request.Data.Length);
                });

                serviceChannel.StartReceiving();
            }


            using (this.logNotify.Context("Notify.Init"))
            {
                // Simulate notify
                notifyChannel = new NotifyChannel(
                    container.Resolve<ILogFactory>(),
                    "qlue-service",
                    container.Resolve<IBusTransportFactory>(),
                    container.Resolve<IBlobClient>(),
                    serviceChannel);

                notifyChannel.RegisterAsyncDispatch<NewAccountNotify>((notify, ctx) =>
                {
                    logNotify.Info("Received *NewAccountNotify* on thread {0}",
                        System.Threading.Thread.CurrentThread.ManagedThreadId);
                    logNotify.Debug("Customer Name: {0}", notify.CustomerName);

                    var notify2 = new NewAccount2Notify
                    {
                        CustomerName2 = notify.CustomerName + "2"
                    };
                    ctx.SendNotify(notify2);

                    return Task.FromResult(false);
                });

                notifyChannel.RegisterAsyncDispatch<NewAccount2Notify>((notify, ctx) =>
                {
                    logNotify.Info("Received *NewAccount2Notify* on thread {0}",
                        System.Threading.Thread.CurrentThread.ManagedThreadId);
                    logNotify.Debug("Customer Name: {0}", notify.CustomerName2);

                    return Task.FromResult(false);
                });

                notifyChannel.StartReceiving();
            }


            using (this.logConsumer.Context("Consumer.Run"))
            {
                // Simulate consumer (web site)
                var consumerChannel = new RequestChannel(
                    container.Resolve<ILogFactory>(),
                    "qlue-website",
                    "qlue-service", 5,
                    container.Resolve<IBusTransportFactory>(),
                    container.Resolve<IBlobClient>());

                // Create the new messages to send
                var newAcc = new NewAccountRequest()
                {
                    CustomerName = "John Jones",
                    AccountNumber = 111232432
                };
                newAcc.Data = new byte[RequestSizeInBytes];
                var randomReq = new Random();
                randomReq.NextBytes(newAcc.Data);

                logConsumer.Info("Start sync send (req/res)");
                var watch3 = Stopwatch.StartNew();
                var syncResponse = consumerChannel.SendWaitResponse<NewAccountResponse>(newAcc, TimeSpan.FromSeconds(60), null);
                watch3.Stop();
                logConsumer.Info("Sync Response = {0}   Took {1:N0} ms", syncResponse.ProcessResult, watch3.ElapsedMilliseconds);

                logConsumer.Info("Start async send (req/res)");
                var watch4 = Stopwatch.StartNew();
                var asyncTask = consumerChannel.SendWaitResponseAsync<NewAccountResponse>(newAcc, TimeSpan.FromSeconds(60), null);
                asyncTask.ContinueWith(response =>
                {
                    watch4.Stop();
                    logConsumer.Info("Sync Response = {0}   Took {1:N0} ms", response.Result.ProcessResult, watch4.ElapsedMilliseconds);
                });

                logConsumer.Info("Continue...");


                var notifyTest = new NoResponseTestRequest
                {
                    AccountNumber = 1234,
                    CustomerName = "safasd"
                };
                notifyTest.Data = new byte[NotifySizeInBytes];
                randomReq.NextBytes(notifyTest.Data);


                int testSize = 100;

                logConsumer.Info("Start async send (notify)");
                var watch1 = Stopwatch.StartNew();
                var allTasks = new List<Task>();
                for (int i = 0; i < testSize; i++)
                {
                    allTasks.Add(consumerChannel.SendOneWayAsync(notifyTest, null));
                }

                logConsumer.Info("Waiting");
                Task.WaitAll(allTasks.ToArray());
                watch1.Stop();
                logConsumer.Info("Done! Took {0:N0} ms", watch1.ElapsedMilliseconds);

                logConsumer.Info("Start sync send");
                var watch2 = Stopwatch.StartNew();
                for (int i = 0; i < testSize; i++)
                {
                    consumerChannel.SendOneWay(notifyTest, null);
                }
                watch2.Stop();
                logConsumer.Info("Done! Took {0:N0} ms", watch2.ElapsedMilliseconds);


                Console.WriteLine("Press ENTER to exit the application.");
                Console.Read();

                consumerChannel.Dispose();
            }

            serviceChannel.Dispose();
            notifyChannel.Dispose();
        }
    }

    public class NewAccountRequest
    {
        public string CustomerName { get; set; }
        public int AccountNumber { get; set; }
        public byte[] Data { get; set; }
    }

    public class NewAccountResponse
    {
        public string ProcessResult { get; set; }
        public byte[] Data { get; set; }
    }

    public class NewAccountNotify
    {
        public string CustomerName { get; set; }
    }

    public class NewAccount2Notify
    {
        public string CustomerName2 { get; set; }
    }

    public class NoResponseTestRequest
    {
        public string CustomerName { get; set; }
        public int AccountNumber { get; set; }
        public byte[] Data { get; set; }
    }
}
