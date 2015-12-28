﻿using System;
using System.Linq;
using System.IO;

namespace Animatroller.DMXrecorder
{
    public class CsvFileWriter : FileWriter
    {
        private StreamWriter streamWriter;

        public CsvFileWriter(string fileName)
            : base(fileName)
        {
            this.streamWriter = new StreamWriter(this.fileStream);
        }

        public override void Dispose()
        {
            this.streamWriter.Flush();
            this.streamWriter.Dispose();
        }

        public override void Output(OutputDmxData dmxData)
        {
            switch (dmxData.DataType)
            {
                case OutputDmxData.DataTypes.NoChange:
                    this.streamWriter.WriteLine("{0},{1},{2},NoChange", dmxData.Sequence, dmxData.Timestamp, dmxData.Universe);
                    break;

                case OutputDmxData.DataTypes.FullFrame:
                    this.streamWriter.WriteLine("{0},{1},{2},Full,{3}",
                        dmxData.Sequence, dmxData.Timestamp, dmxData.Universe, string.Join(",", dmxData.Data.Select(x => x.ToString())));
                    break;
            }
        }
    }
}
