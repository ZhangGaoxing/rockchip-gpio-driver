using Iot.Device.Gpio.Drivers;
using System;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Threading;

namespace RockchipGpio.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            using GpioController controller = new GpioController(PinNumberingScheme.Board, new NanoPiR2S());
            
            foreach (var item in NanoPiR2S._pinNumberConverter)
            {
                if (item == -1)
                {
                    continue;
                }

                Console.WriteLine(item);
                int i = Array.IndexOf(NanoPiR2S._pinNumberConverter, item);

                controller.OpenPin(i);
                Console.WriteLine(controller.Read(i));
                Thread.Sleep(1000);
                controller.SetPinMode(i, PinMode.InputPullUp);
                Console.WriteLine(controller.Read(i));
                Thread.Sleep(1000);
                controller.SetPinMode(i, PinMode.InputPullDown);
                Console.WriteLine(controller.Read(i));
                Thread.Sleep(1000);

                //controller.OpenPin(i, PinMode.Output);
                //Console.WriteLine(controller.Read(i));
                //controller.Write(i, 0);
                //Console.WriteLine(controller.Read(i));
                //controller.Write(i, 1);
                //Console.WriteLine(controller.Read(i));

                controller.ClosePin(i);
            }
        }
    }
}