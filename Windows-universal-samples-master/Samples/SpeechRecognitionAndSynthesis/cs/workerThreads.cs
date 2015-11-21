using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

//Code from AzureSBLite,
//https://github.com/ppatierno/azuresblite-examples/blob/master/Scenarios/Common/WorkerThread.cs
namespace SpeechAndTTS
{
    public delegate void ParameterizedWorkerThreadStart(object status);

    public class workerThread
    {
        private ParameterizedWorkerThreadStart workerCallback;

        private object status;

        public workerThread(ParameterizedWorkerThreadStart workerThread)
        {
            this.workerCallback = workerThread;
        }

        public void Start(object status)
        {
            Task.Run(() =>
            {
                this.status = status;
                this.InternalThread();
            });
        }

        private void InternalThread()
        {
            this.workerCallback(this.status);
        }
    }
}
