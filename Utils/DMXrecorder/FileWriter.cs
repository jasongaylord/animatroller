﻿using System;
using System.Linq;
using System.IO;

namespace Animatroller.DMXrecorder
{
    public abstract class FileWriter : IDisposable
    {
        protected FileStream fileStream;

        public FileWriter(string fileName)
        {
            this.fileStream = File.Create(fileName);
        }

        public abstract void Dispose();

        public virtual void Header(int universeId)
        {
        }

        public abstract void Output(OutputDmxData dmxData);

        public virtual void Footer(int universeId)
        {
        }
    }
}
