using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ppatierno.AzureSBLite;
using ppatierno.AzureSBLite.Messaging;
using Amqp;
using System.Threading;
using System.Diagnostics;

namespace SpeechAndTTS
{
    class azureConnector
    {
        // private string SB_NAMESPACE = "onlineorder";
        private string SB_CONNECTION_STRING = "Endpoint=sb://onlineorder.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ELXXNeA+zNdYw5qVwe6qj8WqrX4AIhjSDW7s0vogPcM=";
        // private string SUBSCRIPTION = "";
        // private string SHARED_ACCESS_KEY_NAME = "RootManageSharedAccessKey";
        // private string SHARED_ACCESS_KEY = "ELXXNeA+zNdYw5qVwe6qj8WqrX4AIhjSDW7s0vogPcM=";
        private static Mutex q_m = new Mutex();
        private static Queue<string> msg_q = new Queue<string>();
        //Send the contents to the service bus's topic
        public void sendSBMessageToTopic(string content, string topic)
        {
            try
            {
                ServiceBusConnectionStringBuilder builder = new ServiceBusConnectionStringBuilder(SB_CONNECTION_STRING);
                builder.TransportType = TransportType.Amqp;
                MessagingFactory factory = MessagingFactory.CreateFromConnectionString(SB_CONNECTION_STRING);
                TopicClient client = factory.CreateTopicClient(topic);
                MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                BrokeredMessage message = new BrokeredMessage(stream);
                message.Properties["time"] = DateTime.UtcNow;
                client.Send(message);
                client.Close();
                factory.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR : " + ex.ToString() );
            }

        }


        class TopicSub { public string Topic; public string Sub; }

        //Still confused with this function, source from 
        //https://github.com/ppatierno/azuresblite-examples/blob/master/Scenarios/Common/Scenarios.cs

        public void runSubscriptionReceiver(string topic, string topicSubscription)
        {
            workerThread subThread = new workerThread(this.SubscriptionReceiver);
            subThread.Start(new azureConnector.TopicSub() { Topic = topic, Sub = topicSubscription });
        }

        //Receive from a specfic topic's subscription
        private void SubscriptionReceiver(object obj)
        {

            try {
                azureConnector.TopicSub topicsub = (azureConnector.TopicSub)obj;

                ServiceBusConnectionStringBuilder builder = new ServiceBusConnectionStringBuilder(SB_CONNECTION_STRING);
                builder.TransportType = TransportType.Amqp;
                MessagingFactory factory = MessagingFactory.CreateFromConnectionString(SB_CONNECTION_STRING);
                SubscriptionClient client = factory.CreateSubscriptionClient(topicsub.Topic, topicsub.Sub);
                string msg;
                while (true)
                {
                    BrokeredMessage request = client.Receive();
                    if (request != null && request.Properties != null)
                    {
                        msg = decodeMsg(request);
                        recieveMessage(msg);
                    }
                    request.Complete();
                }
            } 
            catch(Exception ex)
            {
                Debug.WriteLine("ERROR : " + ex.ToString());
            }

        }

        private string decodeMsg(BrokeredMessage request)
        {
            byte[] buffer;
            Char[] msg;
            buffer = request.GetBytes();
            Decoder utf8decoder = Encoding.UTF8.GetDecoder();
            int charCount = utf8decoder.GetCharCount(buffer, 0, buffer.Length);
            msg = new Char[charCount];
            utf8decoder.GetChars(buffer, 0, buffer.Length, msg, 0);
            return new string(msg);
        }

        public string getMessage()
        {
            string msg;
            q_m.WaitOne();
            if (msg_q.Count != 0)
            {
                msg = msg_q.Dequeue();
            } else
            {
                msg = null;
            }
            q_m.ReleaseMutex();
            return msg;
        }

        private void recieveMessage(string msg)
        {
            q_m.WaitOne();
            msg_q.Enqueue(msg);
            q_m.ReleaseMutex();
        }

        /*
        public static void SendSBMessage(string message)
        {
            try
            {
                //Endpoint=sb://onlineorder.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ELXXNeA+zNdYw5qVwe6qj8WqrX4AIhjSDW7s0vogPcM=
                string baseUri = "https://onlineorder.servicebus.windows.net";
                using (System.Net.Http.HttpClient client = new System.Net.Http.HttpClient())
                {
                    client.BaseAddress = new Uri(baseUri);
                    client.DefaultRequestHeaders.Accept.Clear();

                    string token = SASTokenHelper();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SharedAccessSignature", token);

                    string json = JsonConvert.SerializeObject(message);
                    HttpContent content = new StringContent(json, Encoding.UTF8);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    content.Headers.Add("led", message);
                    string path = "/ordermeal/messages";

                    var response = client.PostAsync(path, content).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        // Do something
                        Debug.WriteLine("Success!");
                    }
                    else
                    {
                        Debug.WriteLine("Failure!" + response);
                    }

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERORR!" + ex.ToString());
            }
        }

        private static string SASTokenHelper()
        {
            //Endpoint=sb://CustomNamespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=pWq4OwxD5Bjfrq14YYk0oZ6wird8LdIuitGZbTyop8Y=
            string keyName = "RootManageSharedAccessKey";
            string key = "ELXXNeA+zNdYw5qVwe6qj8WqrX4AIhjSDW7s0vogPcM=";
            string uri = "onlineorder.servicebus.windows.net";

            int expiry = (int)DateTime.UtcNow.AddMinutes(20).Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            string stringToSign = WebUtility.UrlEncode(uri) + "\n" + expiry.ToString();
            string signature = HmacSha256(key, stringToSign);
            string token = String.Format("sr={0}&sig={1}&se={2}&skn={3}", WebUtility.UrlEncode(uri), WebUtility.UrlEncode(signature), expiry, keyName);

            return token;
        }

        // Because Windows.Security.Cryptography.Core.MacAlgorithmNames.HmacSha256 doesn't
        // exist in WP8.1 context we need to do another implementation
        public static string HmacSha256(string key, string value)
        {
            var keyStrm = CryptographicBuffer.ConvertStringToBinary(key, BinaryStringEncoding.Utf8);
            var valueStrm = CryptographicBuffer.ConvertStringToBinary(value, BinaryStringEncoding.Utf8);

            var objMacProv = MacAlgorithmProvider.OpenAlgorithm(MacAlgorithmNames.HmacSha256);
            var hash = objMacProv.CreateHash(keyStrm);
            hash.Append(valueStrm);

            return CryptographicBuffer.EncodeToBase64String(hash.GetValueAndReset());
        }*/
    }
}
