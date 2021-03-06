﻿using System;
using System.Linq;
using System.IO;

namespace Animatroller.Common
{
    public interface IFileReader
    {
        bool DataAvailable { get; }

        void Rewind();

        DmxData ReadFrame();
    }
}
