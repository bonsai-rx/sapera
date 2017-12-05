using DALSA.SaperaLT.SapClassBasic;
using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bonsai.Sapera
{
    public class AcquisitionDevice : Source<IplImage>
    {
        IObservable<IplImage> source;
        readonly object captureLock = new object();

        public AcquisitionDevice()
        {
            source = Observable.Create<IplImage>((observer, cancellationToken) =>
            {
                return Task.Factory.StartNew(() =>
                {
                    lock (captureLock)
                    {
                        using (var location = new SapLocation(ServerName, DeviceIndex))
                        using (var device = new SapAcqDevice(location, ConfigFileName))
                        using (var buffer = new SapBufferWithTrash(2, device, SapBuffer.MemoryType.ScatterGather))
                        using (var transfer = new SapAcqDeviceToBuf(device, buffer))
                        using (var waitEvent = new AutoResetEvent(false))
                        {
                            if (!device.Create())
                            {
                                throw new InvalidOperationException("Error opening device.");
                            }

                            transfer.Pairs[0].EventType = SapXferPair.XferEventType.EndOfFrame;
                            transfer.XferNotify += (sender, e) =>
                            {
                                int channels;
                                IplDepth depth;
                                SapFormat outputFormat;
                                GetImageDepth(buffer.Format, out depth, out channels, out outputFormat);
                                var image = new IplImage(new Size(buffer.Width, buffer.Height), depth, channels);
                                buffer.Write(0, buffer.Width * buffer.Height, image.ImageData);
                                observer.OnNext(image);
                                waitEvent.Set();
                            };

                            if (!buffer.Create())
                            {
                                throw new InvalidOperationException("Error creating buffer.");
                            }

                            if (!transfer.Create())
                            {
                                throw new InvalidOperationException("Error creating transfer.");
                            }

                            while (!cancellationToken.IsCancellationRequested)
                            {
                                waitEvent.WaitOne();
                            }
                        }
                    }
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            })
            .PublishReconnectable()
            .RefCount();
        }

        [TypeConverter(typeof(ServerNameConverter))]
        public string ServerName { get; set; }

        public int DeviceIndex { get; set; }

        [Editor("Bonsai.Design.OpenFileNameEditor, Bonsai.Design", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        public string ConfigFileName { get; set; }

        static void GetImageDepth(SapFormat format, out IplDepth depth, out int channels, out SapFormat outputFormat)
        {
            switch (format)
            {
                case SapFormat.Binary:
                case SapFormat.Mono8:
                    channels = 1;
                    depth = IplDepth.U8;
                    outputFormat = SapFormat.Mono8;
                    break;
                case SapFormat.RGB888:
                    channels = 3;
                    depth = IplDepth.U8;
                    outputFormat = SapFormat.RGB888;
                    break;

                case SapFormat.AYU2:
                case SapFormat.BICOLOR1616:
                case SapFormat.BICOLOR88:
                case SapFormat.ColorI10:
                case SapFormat.ColorI11:
                case SapFormat.ColorI12:
                case SapFormat.ColorI13:
                case SapFormat.ColorI14:
                case SapFormat.ColorI15:
                case SapFormat.ColorI16:
                case SapFormat.ColorI8:
                case SapFormat.ColorI9:
                case SapFormat.ColorNI10:
                case SapFormat.ColorNI11:
                case SapFormat.ColorNI12:
                case SapFormat.ColorNI13:
                case SapFormat.ColorNI14:
                case SapFormat.ColorNI15:
                case SapFormat.ColorNI16:
                case SapFormat.ColorNI8:
                case SapFormat.ColorNI9:
                case SapFormat.Complex:
                case SapFormat.FPoint:
                case SapFormat.Float:
                case SapFormat.HSI:
                case SapFormat.HSIP16:
                case SapFormat.HSIP8:
                case SapFormat.HSV:
                case SapFormat.HSVP16:
                case SapFormat.HSVP8:
                case SapFormat.IYU1:
                case SapFormat.IYU2:
                case SapFormat.Int10:
                case SapFormat.Int11:
                case SapFormat.Int12:
                case SapFormat.Int13:
                case SapFormat.Int14:
                case SapFormat.Int15:
                case SapFormat.Int16:
                case SapFormat.Int24:
                case SapFormat.Int32:
                case SapFormat.Int64:
                case SapFormat.Int8:
                case SapFormat.Int9:
                case SapFormat.LAB:
                case SapFormat.LAB101010:
                case SapFormat.LAB16161616:
                case SapFormat.Mono10:
                case SapFormat.Mono11:
                case SapFormat.Mono12:
                case SapFormat.Mono13:
                case SapFormat.Mono14:
                case SapFormat.Mono15:
                case SapFormat.Mono16:
                case SapFormat.Mono16P2:
                case SapFormat.Mono16P3:
                case SapFormat.Mono16P4:
                case SapFormat.Mono24:
                case SapFormat.Mono32:
                case SapFormat.Mono64:
                case SapFormat.Mono8P2:
                case SapFormat.Mono8P3:
                case SapFormat.Mono8P4:
                case SapFormat.Mono9:
                case SapFormat.Point:
                case SapFormat.RGB101010:
                case SapFormat.RGB161616:
                case SapFormat.RGB16161616:
                case SapFormat.RGB161616_MONO16:
                case SapFormat.RGB5551:
                case SapFormat.RGB565:
                case SapFormat.RGB8888:
                case SapFormat.RGB888_MONO8:
                case SapFormat.RGBP16:
                case SapFormat.RGBP8:
                case SapFormat.RGBR888:
                case SapFormat.UYVY:
                case SapFormat.Unknown:
                case SapFormat.Y211:
                case SapFormat.YUVP16:
                case SapFormat.YUVP8:
                case SapFormat.YUY2:
                case SapFormat.YUYV:
                case SapFormat.YVYU:
                default: throw new ArgumentException("Unsupported pixel type.", "format");
            }
        }

        public override IObservable<IplImage> Generate()
        {
            return source;
        }

        class ServerNameConverter : StringConverter
        {
            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return true;
            }

            public override TypeConverter.StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                var serverNames = new string[SapManager.GetServerCount(SapManager.ResourceType.AcqDevice)];
                for (int i = 0; i < serverNames.Length; i++)
                {
                    serverNames[i] = SapManager.GetServerName(i, SapManager.ResourceType.AcqDevice);
                }

                return new StandardValuesCollection(serverNames);
            }
        }
    }
}
