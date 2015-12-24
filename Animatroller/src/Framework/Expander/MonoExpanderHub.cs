﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Animatroller.Framework.MonoExpanderMessages;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Newtonsoft.Json.Linq;
using NLog;

namespace Animatroller.Framework.Expander
{
    [HubName("MonoExpanderHub")]
    public class MonoExpanderHub : Hub
    {
        private IMonoExpanderServerRepository mainServer;
        private ILogger log;

        public MonoExpanderHub(IMonoExpanderServerRepository mainServer, ILogger log)
        {
            this.mainServer = mainServer;
            this.log = log;
        }

        private void UpdateInstance()
        {
            var instanceId = Context.QueryString["InstanceId"];

            this.mainServer.SetKnownInstanceId(instanceId, Context.ConnectionId);

            this.log.Debug("Instance {0} connected on {1}", instanceId, Context.ConnectionId);
        }

        public override Task OnReconnected()
        {
            UpdateInstance();

            return base.OnReconnected();
        }

        public override Task OnConnected()
        {
            UpdateInstance();

            return base.OnConnected();
        }

        public void HandleMessage(Type messageType, object message)
        {
            var jobject = message as JObject;
            if (jobject != null)
            {
                var messageObject = jobject.ToObject(messageType);

                // Check if we handle it here in the hub first
                var method = typeof(MonoExpanderHub).GetMethods()
                    .Where(x => x.Name == "Handle" && x.GetParameters().Any(p => p.ParameterType == messageType))
                    .ToList();

                if (method.Any())
                    method.Single().Invoke(this, new object[] { messageObject });
                else
                    this.mainServer.HandleMessage(Context.ConnectionId, messageObject);
            }
        }

        private byte[] CalculateSignatureSha1(string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Open))
            using (var bs = new BufferedStream(fs))
            {
                using (var sha1 = new System.Security.Cryptography.SHA1Managed())
                {
                    return sha1.ComputeHash(bs);
                }
            }
        }

        public void Handle(FileRequest message)
        {
            this.log.Info("Requested download file {1} of type {0}", message.Type, message.FileName);

            if (!string.IsNullOrEmpty(Path.GetDirectoryName(message.FileName)))
                throw new ArgumentException("FileName should be without path");

            string fileTypeFolder = Path.Combine(this.mainServer.ExpanderSharedFiles, message.Type.ToString());
            Directory.CreateDirectory(fileTypeFolder);

            string filePath = Path.Combine(fileTypeFolder, message.FileName);

            if (!File.Exists(filePath))
            {
                this.log.Warn("File {0} of type {1} doesn't exist", message.FileName, message.Type);

                Clients.Caller.HandleMessage(typeof(FileResponse), new FileResponse
                {
                    DownloadId = message.DownloadId,
                    Size = 0
                });

                return;
            }

            var fi = new FileInfo(filePath);

            Clients.Caller.HandleMessage(typeof(FileResponse), new FileResponse
            {
                DownloadId = message.DownloadId,
                Size = fi.Length,
                SignatureSha1 = CalculateSignatureSha1(filePath)
            });
        }

        public void Handle(FileChunkRequest message)
        {
            string filePath = Path.Combine(this.mainServer.ExpanderSharedFiles, message.Type.ToString(), message.FileName);

            long fileSize = new FileInfo(filePath).Length;
            int chunkId = (int)(message.ChunkStart / message.ChunkSize);
            int chunks = (int)(fileSize / message.ChunkSize);
            this.log.Info("Request for file {0} chunk {1}/{2} for {3:N0} bytes", message.FileName, chunkId, chunks, message.ChunkSize);

            using (var fs = File.OpenRead(filePath))
            {
                fs.Seek(message.ChunkStart, SeekOrigin.Begin);

                int bytesToRead = Math.Min(message.ChunkSize, (int)(fs.Length - message.ChunkStart));
                if (bytesToRead <= 0)
                    return;

                byte[] chunk = new byte[bytesToRead];
                fs.Read(chunk, 0, chunk.Length);

                Clients.Caller.HandleMessage(typeof(FileChunkResponse), new FileChunkResponse
                {
                    DownloadId = message.DownloadId,
                    ChunkStart = message.ChunkStart,
                    Chunk = chunk
                });
            }
        }
    }
}